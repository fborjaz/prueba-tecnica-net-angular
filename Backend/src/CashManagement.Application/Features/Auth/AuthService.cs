using System.Security.Cryptography;
using System.Text;
using CashManagement.Application.Common;
using CashManagement.Application.Interfaces;
using CashManagement.Domain.Common;
using CashManagement.Domain.Constants;
using CashManagement.Domain.Entities;
using CashManagement.Domain.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CashManagement.Application.Features.Auth;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task LogoutAsync(CancellationToken ct = default);
    Task<UserProfileDto> GetProfileAsync(CancellationToken ct = default);
    Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default);
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);
}

public class AuthService(
    IApplicationDbContext db,
    IPasswordHasher hasher,
    IFieldEncryptor encryptor,
    ITokenService tokenService,
    ICurrentUserService currentUser,
    IEmailSender emailSender,
    IOptions<AppOptions> options,
    IValidator<LoginRequest> loginValidator,
    IValidator<ForgotPasswordRequest> forgotValidator,
    IValidator<ResetPasswordRequest> resetValidator,
    ILoggerFactory loggerFactory) : IAuthService
{
    private const string InvalidCredentials = "Usuario o contraseña incorrectos.";

    // Logger dedicado a eventos de seguridad (inicios de sesión, bloqueos, recuperación de contraseña).
    private readonly ILogger _security = loggerFactory.CreateLogger(LogCategories.Security);

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        await loginValidator.EnsureValidAsync(request, ct);

        var username = request.Username.Trim();
        var user = await db.Users
            .Include(u => u.Rol)
            .Include(u => u.Status)
            .FirstOrDefaultAsync(u => u.Username == username && !u.IsDeleted, ct);

        if (user is null)
        {
            _security.LogWarning("Inicio de sesión fallido: el usuario {Username} no existe", username);
            throw new UnauthorizedException(InvalidCredentials);
        }

        switch (user.StatusId)
        {
            case UserStatuses.Blocked:
                _security.LogWarning("Inicio de sesión rechazado: el usuario {Username} está bloqueado", username);
                throw new UnauthorizedException("El usuario está bloqueado. Contacte al administrador o recupere su contraseña.");
            case UserStatuses.Pending:
                _security.LogWarning("Inicio de sesión rechazado: el usuario {Username} está pendiente de aprobación", username);
                throw new UnauthorizedException("El usuario está pendiente de aprobación por un administrador.");
            case UserStatuses.Inactive:
                _security.LogWarning("Inicio de sesión rechazado: el usuario {Username} está inactivo", username);
                throw new UnauthorizedException("El usuario se encuentra inactivo.");
        }

        if (!hasher.Verify(request.Password, user.Password))
        {
            var remaining = user.RegisterFailedLogin();

            if (user.IsBlocked)
            {
                await db.SaveChangesAsync(ct);
                _security.LogWarning("Usuario {Username} bloqueado por {Attempts} intentos fallidos de inicio de sesión",
                    username, user.FailedAttempts);
                throw new UnauthorizedException("El usuario fue bloqueado por superar el número de intentos permitidos.");
            }

            await db.SaveChangesAsync(ct);
            _security.LogWarning("Inicio de sesión fallido: contraseña incorrecta para {Username} (intentos restantes: {Remaining})",
                username, remaining);
            throw new UnauthorizedException($"{InvalidCredentials} Intentos restantes: {remaining}.");
        }

        user.RegisterSuccessfulLogin(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(ct);

        var roleName = user.Rol?.Name ?? Roles.NameOf(user.RolId);
        var token = tokenService.CreateToken(user, roleName);

        _security.LogInformation("Inicio de sesión exitoso: {Username} ({Role})", username, roleName);

        return new LoginResponse(token.Token, token.ExpiresAt, ToProfile(user));
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct)
                   ?? throw new NotFoundException("Usuario no encontrado.");

        var now = DateTimeOffset.UtcNow;
        user.Logout(now);

        // Si el cajero cierra sesión, se libera la caja que tenga abierta.
        var openSessions = await db.CashSessions.Where(s => s.UserId == userId && s.ClosedAt == null).ToListAsync(ct);
        foreach (var session in openSessions)
        {
            session.Close(now);
        }

        await db.SaveChangesAsync(ct);
        _security.LogInformation("Cierre de sesión: {Username}", user.Username);
    }

    public async Task<UserProfileDto> GetProfileAsync(CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var user = await db.Users
            .Include(u => u.Rol)
            .Include(u => u.Status)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct)
            ?? throw new NotFoundException("Usuario no encontrado.");

        return ToProfile(user);
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default)
    {
        await forgotValidator.EnsureValidAsync(request, ct);

        var emailHash = encryptor.Hash(request.Email.Trim().ToLowerInvariant());
        var user = await db.Users.FirstOrDefaultAsync(u => u.EmailHash == emailHash && !u.IsDeleted, ct);

        // Respuesta idéntica exista o no el correo, para no revelar qué cuentas existen.
        if (user is null || user.StatusId is UserStatuses.Pending or UserStatuses.Inactive)
        {
            _security.LogWarning("Recuperación de contraseña solicitada para un correo no válido");
            return;
        }

        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        db.PasswordResets.Add(new PasswordReset
        {
            UserId = user.Id,
            TokenHash = Sha256(rawToken),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(options.Value.PasswordResetMinutes),
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);

        var link = $"{options.Value.FrontendUrl.TrimEnd('/')}/auth/reset-password?token={rawToken}";
        var body = $"""
            <p>Hola {user.Names},</p>
            <p>Recibimos una solicitud para restablecer tu contraseña. El enlace es válido por {options.Value.PasswordResetMinutes} minutos:</p>
            <p><a href="{link}">Restablecer contraseña</a></p>
            <p>Si no realizaste esta solicitud, ignora este correo.</p>
            """;

        await emailSender.SendAsync(encryptor.Decrypt(user.Email), "Recuperación de contraseña", body, ct);
        _security.LogInformation("Enlace de recuperación de contraseña generado para {Username}", user.Username);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        await resetValidator.EnsureValidAsync(request, ct);

        var tokenHash = Sha256(request.Token.Trim());
        var reset = await db.PasswordResets
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash && !r.Used, ct);

        if (reset?.User is null || reset.ExpiresAt < DateTimeOffset.UtcNow)
        {
            _security.LogWarning("Intento de restablecer contraseña con un token inválido o expirado");
            throw new BusinessException("El enlace de recuperación es inválido o ya expiró.");
        }

        var user = reset.User;
        user.ResetPassword(hasher.Hash(request.NewPassword));
        reset.Used = true;

        await db.SaveChangesAsync(ct);
        _security.LogInformation("Contraseña restablecida para {Username}", user.Username);
    }

    private UserProfileDto ToProfile(User user) => new(
        user.Id,
        user.Username,
        user.Names,
        user.LastNames,
        user.FullName,
        encryptor.Decrypt(user.Email),
        encryptor.Decrypt(user.Identification),
        user.RolId,
        user.Rol?.Name ?? Roles.NameOf(user.RolId),
        user.StatusId,
        user.Status?.Description ?? user.StatusId,
        user.CreationDate,
        user.LastLogin);

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
