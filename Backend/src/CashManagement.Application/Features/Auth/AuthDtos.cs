using CashManagement.Application.Common;
using FluentValidation;

namespace CashManagement.Application.Features.Auth;

public record LoginRequest(string Username, string Password);

public record LoginResponse(string Token, DateTimeOffset ExpiresAt, UserProfileDto User);

public record UserProfileDto(
    int Id,
    string Username,
    string Names,
    string LastNames,
    string FullName,
    string Email,
    string Identification,
    int RolId,
    string Role,
    string StatusId,
    string Status,
    DateTimeOffset CreationDate,
    DateTimeOffset? LastLogin);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string Token, string NewPassword, string ConfirmPassword);

/// <summary>Configuración de la aplicación usada por la capa de aplicación.</summary>
public class AppOptions
{
    public const string Section = "App";

    /// <summary>URL pública del frontend (para el enlace de recuperación de contraseña).</summary>
    public string FrontendUrl { get; set; } = "http://localhost:4200";

    /// <summary>Minutos de validez del enlace de recuperación de contraseña.</summary>
    public int PasswordResetMinutes { get; set; } = 30;
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().WithMessage("El usuario es obligatorio.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("La contraseña es obligatoria.");
    }
}

public class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo es obligatorio.")
            .EmailAddress().WithMessage("El correo no tiene un formato válido.");
    }
}

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty().WithMessage("El token de recuperación es obligatorio.");
        RuleFor(x => x.NewPassword).ApplyPasswordRules();
        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.NewPassword).WithMessage("Las contraseñas no coinciden.");
    }
}

public static class PasswordRuleExtensions
{
    /// <summary>Contraseña: al menos un número, una mayúscula, entre 8 y 30 caracteres.</summary>
    public static IRuleBuilderOptions<T, string> ApplyPasswordRules<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("La contraseña es obligatoria.")
            .Length(8, 30).WithMessage("La contraseña debe tener entre 8 y 30 caracteres.")
            .Matches(ValidationRules.PasswordHasNumber).WithMessage("La contraseña debe tener al menos un número.")
            .Matches(ValidationRules.PasswordHasUpper).WithMessage("La contraseña debe tener al menos una letra mayúscula.");
}
