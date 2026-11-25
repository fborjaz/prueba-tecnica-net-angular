using CashManagement.Application.Common;
using CashManagement.Application.Features.Auth;
using CashManagement.Domain.Constants;
using FluentValidation;

namespace CashManagement.Application.Features.Users;

public record UserDto(
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
    string? CreatedBy,
    string? ApprovedBy,
    DateTimeOffset? DateApproval,
    bool SessionActive,
    DateTimeOffset? LastLogin,
    int FailedAttempts);

public record UserQuery : PageQuery
{
    public int? RolId { get; init; }
    public string? StatusId { get; init; }
}

public record CreateUserRequest(
    string Username,
    string Names,
    string LastNames,
    string Email,
    string Identification,
    string Password,
    int RolId);

/// <summary>El nombre de usuario no se modifica. La contraseña es opcional al actualizar.</summary>
public record UpdateUserRequest(
    string Names,
    string LastNames,
    string Email,
    string Identification,
    int RolId,
    string? Password);

public record ChangeUserStatusRequest(string StatusId);

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Username).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("El nombre de usuario es obligatorio.")
            .Length(8, 20).WithMessage("El nombre de usuario debe tener entre 8 y 20 caracteres.")
            .Matches(ValidationRules.UsernameAllowedChars).WithMessage("El nombre de usuario no debe tener caracteres especiales.")
            .Matches(ValidationRules.UsernameLettersAndNumber).WithMessage("El nombre de usuario debe tener letras y al menos un número.");

        RuleFor(x => x.Password).ApplyPasswordRules();
        RuleFor(x => x.Names).ApplyPersonNameRules("Los nombres");
        RuleFor(x => x.LastNames).ApplyPersonNameRules("Los apellidos");
        RuleFor(x => x.Email).ApplyEmailRules();
        RuleFor(x => x.Identification).ApplyIdentificationRules();
        RuleFor(x => x.RolId).ApplyManageableRoleRules();
    }
}

public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.Names).ApplyPersonNameRules("Los nombres");
        RuleFor(x => x.LastNames).ApplyPersonNameRules("Los apellidos");
        RuleFor(x => x.Email).ApplyEmailRules();
        RuleFor(x => x.Identification).ApplyIdentificationRules();
        RuleFor(x => x.RolId).ApplyManageableRoleRules();
        RuleFor(x => x.Password!).ApplyPasswordRules().When(x => !string.IsNullOrWhiteSpace(x.Password));
    }
}

public class ChangeUserStatusRequestValidator : AbstractValidator<ChangeUserStatusRequest>
{
    public ChangeUserStatusRequestValidator()
    {
        RuleFor(x => x.StatusId)
            .Must(s => s is UserStatuses.Active or UserStatuses.Inactive or UserStatuses.Blocked)
            .WithMessage("El estado debe ser ACT (activo), INA (inactivo) o BLO (bloqueado).");
    }
}

public static class PersonRuleExtensions
{
    public static IRuleBuilderOptions<T, string> ApplyPersonNameRules<T>(this IRuleBuilderInitial<T, string> rule, string field) =>
        rule.Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage($"{field} son obligatorios.")
            .MaximumLength(50).WithMessage($"{field} no deben superar 50 caracteres.")
            .Matches(ValidationRules.PersonName).WithMessage($"{field} solo deben contener letras.");

    public static IRuleBuilderOptions<T, string> ApplyEmailRules<T>(this IRuleBuilderInitial<T, string> rule, int maxLength = 100) =>
        rule.Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("El correo es obligatorio.")
            .MaximumLength(maxLength).WithMessage($"El correo no debe superar {maxLength} caracteres.")
            .EmailAddress().WithMessage("El correo no tiene un formato válido.");

    public static IRuleBuilderOptions<T, string> ApplyIdentificationRules<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("La identificación es obligatoria.")
            .Matches(ValidationRules.Identification)
            .WithMessage("La identificación debe tener solo números, entre 10 y 13 dígitos.");

    public static IRuleBuilderOptions<T, int> ApplyManageableRoleRules<T>(this IRuleBuilderInitial<T, int> rule) =>
        rule.Must(r => Roles.ManageableRoleIds.Contains(r))
            .WithMessage("Solo se pueden registrar usuarios con rol GESTOR o CAJERO.");
}
