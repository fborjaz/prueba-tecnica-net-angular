using CashManagement.Application.Features.Dashboard;
using CashManagement.Domain.Entities;

namespace CashManagement.Application.Interfaces;

/// <summary>Usuario autenticado de la petición actual (leído desde el JWT).</summary>
public interface ICurrentUserService
{
    int? UserId { get; }
    string? Username { get; }
    string? Role { get; }
    bool IsAuthenticated { get; }

    /// <summary>Id del usuario autenticado o excepción 401 si no existe.</summary>
    int RequireUserId();
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

/// <summary>Cifrado de campos sensibles (correo e identificación de usuarios).</summary>
public interface IFieldEncryptor
{
    /// <summary>Cifra con AES-256-CBC e IV aleatorio. Resultado en Base64.</summary>
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
    /// <summary>HMAC-SHA256 determinístico para búsquedas e índices únicos.</summary>
    string Hash(string value);
}

public record TokenResult(string Token, DateTimeOffset ExpiresAt);

public interface ITokenService
{
    /// <summary>Genera un JWT firmado y cifrado (JWE): las claims viajan encriptadas.</summary>
    TokenResult CreateToken(User user, string roleName);
}

public record GeneratedTurn(int TurnId, string Description);

/// <summary>Acceso a procedimientos almacenados y funciones de SQL Server (Dapper).</summary>
public interface IStoredProcedureService
{
    Task<GeneratedTurn> GenerateTurnAsync(int cashId, string attentionTypeId, int gestorId, CancellationToken ct = default);
    Task<int> ChangeContractServiceAsync(int contractId, int newServiceId, int userId, CancellationToken ct = default);
    Task<DashboardIndicatorsDto> GetDashboardIndicatorsAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<int> CountAttendedTurnsAsync(int userId, string roleName, DateOnly date, CancellationToken ct = default);
}

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}

/// <summary>Lector de archivos .xlsx / .csv para cargas masivas.</summary>
public interface ISpreadsheetReader
{
    /// <summary>Devuelve una fila por registro con las cabeceras normalizadas en minúsculas.</summary>
    IReadOnlyList<IReadOnlyDictionary<string, string>> Read(Stream stream, string fileName);
}
