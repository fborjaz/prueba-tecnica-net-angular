using System.Text.RegularExpressions;
using CashManagement.Application.Interfaces;
using CashManagement.Domain.Constants;
using CashManagement.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CashManagement.Infrastructure.Persistence;

/// <summary>
/// Inicializa la base de datos al arrancar la API:
/// 1. Crea la base si no existe (con reintentos: en Docker SQL Server tarda en iniciar).
/// 2. Ejecuta 01_schema.sql y 03_seed.sql si las tablas no existen.
/// 3. Ejecuta siempre 02_functions_procedures.sql (CREATE OR ALTER).
/// 4. Crea los usuarios de demostración (contraseña con BCrypt, datos sensibles cifrados).
/// </summary>
public static partial class DbInitializer
{
    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex GoSeparator();

    private static readonly TimeSpan InitTimeout = TimeSpan.FromMinutes(3);

    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");
        var context = provider.GetRequiredService<AppDbContext>();

        // En el primer arranque (sobre todo en Docker) SQL Server todavía está creciendo los
        // archivos de la base: se da más margen que los 30 s por defecto.
        context.Database.SetCommandTimeout(InitTimeout);

        var connectionString = context.Database.GetConnectionString()
                               ?? throw new InvalidOperationException("No se configuró la cadena de conexión.");
        var scriptsPath = Path.Combine(AppContext.BaseDirectory, "database");

        await EnsureDatabaseAsync(connectionString, logger, ct);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);

        var schemaExists = await ScalarAsync<int>(connection, "SELECT CASE WHEN OBJECT_ID('dbo.rol') IS NULL THEN 0 ELSE 1 END", ct) == 1;
        if (!schemaExists)
        {
            logger.LogInformation("Creando esquema de base de datos");
            await RunScriptAsync(connection, Path.Combine(scriptsPath, "01_schema.sql"), ct);
        }

        await RunScriptAsync(connection, Path.Combine(scriptsPath, "02_functions_procedures.sql"), ct);

        if (!schemaExists)
        {
            logger.LogInformation("Cargando datos iniciales");
            await RunScriptAsync(connection, Path.Combine(scriptsPath, "03_seed.sql"), ct);
        }

        await SeedUsersAsync(provider, context, logger, ct);
    }

    private static async Task EnsureDatabaseAsync(string connectionString, ILogger logger, CancellationToken ct)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var database = builder.InitialCatalog;
        builder.InitialCatalog = "master";

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var master = new SqlConnection(builder.ConnectionString);
                await master.OpenAsync(ct);
                await using var cmd = master.CreateCommand();
                cmd.CommandText = $"IF DB_ID(@name) IS NULL CREATE DATABASE [{database.Replace("]", "]]")}]";
                cmd.Parameters.AddWithValue("@name", database);
                await cmd.ExecuteNonQueryAsync(ct);
                return;
            }
            catch (SqlException ex) when (attempt < 20)
            {
                logger.LogWarning("SQL Server no disponible (intento {Attempt}/20): {Message}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
        }
    }

    private static async Task RunScriptAsync(SqlConnection connection, string path, CancellationToken ct)
    {
        var script = await File.ReadAllTextAsync(path, ct);
        foreach (var batch in GoSeparator().Split(script).Where(b => !string.IsNullOrWhiteSpace(b)))
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = batch;
            cmd.CommandTimeout = (int)InitTimeout.TotalSeconds;
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    private static async Task<T> ScalarAsync<T>(SqlConnection connection, string sql, CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        return (T)Convert.ChangeType((await cmd.ExecuteScalarAsync(ct))!, typeof(T));
    }

    private static async Task SeedUsersAsync(IServiceProvider provider, AppDbContext context, ILogger logger, CancellationToken ct)
    {
        if (await context.Users.IgnoreQueryFilters().AnyAsync(ct)) return;

        var hasher = provider.GetRequiredService<IPasswordHasher>();
        var encryptor = provider.GetRequiredService<IFieldEncryptor>();

        // Todo o nada: si falla a mitad, al reiniciar se vuelven a crear los 4 usuarios. La
        // transacción va dentro de la estrategia de reintentos que tiene configurada EF Core.
        var strategy = context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            context.ChangeTracker.Clear();
            await using var transaction = await context.Database.BeginTransactionAsync(ct);

            User Create(string username, string password, string names, string lastNames, string email, string identification, int rolId, int? creatorId) => new()
            {
                Username = username,
                Password = hasher.Hash(password),
                Names = names,
                LastNames = lastNames,
                Email = encryptor.Encrypt(email),
                EmailHash = encryptor.Hash(email),
                Identification = encryptor.Encrypt(identification),
                IdentificationHash = encryptor.Hash(identification),
                RolId = rolId,
                StatusId = UserStatuses.Active,
                CreationDate = DateTimeOffset.UtcNow,
                UserCreateId = creatorId,
                UserApprovalId = creatorId,
                DateApproval = creatorId is null ? null : DateTimeOffset.UtcNow
            };

            // Credenciales de demostración documentadas en Backend/README.md
            var admin = Create("admin2026", "Admin2026", "Andrea", "Morales", "admin@cashmanagement.local", "0912345678", Roles.AdministratorId, null);
            context.Users.Add(admin);
            await context.SaveChangesAsync(ct);

            var gestor = Create("gestor2026", "Gestor2026", "Carlos", "Vera", "gestor@cashmanagement.local", "0923456789", Roles.ManagerId, admin.Id);
            var cajero1 = Create("cajero01", "Cajero2026", "María", "Paredes", "cajero1@cashmanagement.local", "0934567890", Roles.CashierId, admin.Id);
            var cajero2 = Create("cajero02", "Cajero2026", "Luis", "Andrade", "cajero2@cashmanagement.local", "0945678901", Roles.CashierId, admin.Id);
            context.Users.AddRange(gestor, cajero1, cajero2);
            await context.SaveChangesAsync(ct);

            // Caja 01 con sus 2 cajeros (máximo permitido) y Caja 02 con un cajero.
            context.UserCashes.AddRange(
                new UserCash { UserId = cajero1.Id, CashId = 1, AssignedById = gestor.Id, AssignedAt = DateTimeOffset.UtcNow },
                new UserCash { UserId = cajero2.Id, CashId = 1, AssignedById = gestor.Id, AssignedAt = DateTimeOffset.UtcNow },
                new UserCash { UserId = cajero2.Id, CashId = 2, AssignedById = gestor.Id, AssignedAt = DateTimeOffset.UtcNow });
            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });

        logger.LogInformation("Usuarios de demostración creados");
    }
}
