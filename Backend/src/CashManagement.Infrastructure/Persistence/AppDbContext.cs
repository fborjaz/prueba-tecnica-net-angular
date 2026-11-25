using System.Linq.Expressions;
using CashManagement.Application.Common;
using CashManagement.Application.Interfaces;
using CashManagement.Domain.Common;
using CashManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace CashManagement.Infrastructure.Persistence;

public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ICurrentUserService currentUser,
    ILoggerFactory loggerFactory) : DbContext(options), IApplicationDbContext
{
    /// <summary>Campos que nunca se escriben en los logs de auditoría.</summary>
    private static readonly HashSet<string> SensitiveProperties =
        ["Password", "Email", "EmailHash", "Identification", "IdentificationHash", "TokenHash"];

    private readonly ILogger _audit = loggerFactory.CreateLogger(LogCategories.Audit);

    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<UserStatus> UserStatuses => Set<UserStatus>();
    public DbSet<User> Users => Set<User>();
    public DbSet<PasswordReset> PasswordResets => Set<PasswordReset>();
    public DbSet<Menu> Menus => Set<Menu>();
    public DbSet<RolMenu> RolMenus => Set<RolMenu>();
    public DbSet<Cash> Cashes => Set<Cash>();
    public DbSet<UserCash> UserCashes => Set<UserCash>();
    public DbSet<CashSession> CashSessions => Set<CashSession>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<AttentionType> AttentionTypes => Set<AttentionType>();
    public DbSet<AttentionStatus> AttentionStatuses => Set<AttentionStatus>();
    public DbSet<Turn> Turns => Set<Turn>();
    public DbSet<Attention> Attentions => Set<Attention>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<MethodPayment> MethodPayments => Set<MethodPayment>();
    public DbSet<StatusContract> StatusContracts => Set<StatusContract>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<Payment> Payments => Set<Payment>();

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        Database.BeginTransactionAsync(cancellationToken);

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplySoftDelete();
        var auditEntries = CaptureAudit();

        var result = await base.SaveChangesAsync(cancellationToken);

        WriteAudit(auditEntries);
        return result;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Filtro global de eliminación lógica. Users se excluye a propósito: es referenciado por
        // relaciones obligatorias (gestor del turno, cajero, creador) y se filtra en cada consulta.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType) && entityType.ClrType != typeof(User))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var body = Expression.Equal(
                    Expression.Property(parameter, nameof(ISoftDelete.IsDeleted)),
                    Expression.Constant(false));
                entityType.SetQueryFilter(Expression.Lambda(body, parameter));
            }
        }
    }

    /// <summary>Convierte los DELETE en UPDATE IsDeleted = 1 (eliminación lógica).</summary>
    private void ApplySoftDelete()
    {
        foreach (var entry in ChangeTracker.Entries<ISoftDelete>().Where(e => e.State == EntityState.Deleted))
        {
            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
        }
    }

    private sealed record AuditEntry(EntityEntry Entry, string Action, string Entity, string Changes);

    private List<AuditEntry> CaptureAudit()
    {
        var entries = new List<AuditEntry>();

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;

            var entity = entry.Metadata.ClrType.Name;
            string action;
            IEnumerable<PropertyEntry> properties;

            if (entry.State == EntityState.Added)
            {
                action = "CREACION";
                properties = entry.Properties.Where(p => !p.Metadata.IsPrimaryKey());
            }
            else if (entry.State == EntityState.Deleted ||
                     entry.Entity is ISoftDelete && entry.Property(nameof(ISoftDelete.IsDeleted)) is { IsModified: true, CurrentValue: true })
            {
                action = "ELIMINACION";
                properties = [];
            }
            else
            {
                action = "ACTUALIZACION";
                properties = entry.Properties.Where(p => p.IsModified);
            }

            var changes = string.Join(", ", properties.Select(p =>
                SensitiveProperties.Contains(p.Metadata.Name)
                    ? $"{p.Metadata.Name}=***"
                    : entry.State == EntityState.Added
                        ? $"{p.Metadata.Name}={p.CurrentValue}"
                        : $"{p.Metadata.Name}: {p.OriginalValue} -> {p.CurrentValue}"));

            entries.Add(new AuditEntry(entry, action, entity, changes));
        }

        return entries;
    }

    private void WriteAudit(IEnumerable<AuditEntry> entries)
    {
        var user = currentUser.Username ?? "sistema";

        foreach (var e in entries)
        {
            // Las claves generadas por la BD (IDENTITY) ya están disponibles después de guardar.
            var key = string.Join("|", e.Entry.Properties.Where(p => p.Metadata.IsPrimaryKey()).Select(p => p.CurrentValue));
            _audit.LogInformation("{Action} {Entity} [Id={EntityId}] por {User}. {Changes}",
                e.Action, e.Entity, key, user, e.Changes);
        }
    }
}
