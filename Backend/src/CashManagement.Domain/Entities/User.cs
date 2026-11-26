using CashManagement.Domain.Common;
using CashManagement.Domain.Constants;
using CashManagement.Domain.Exceptions;

namespace CashManagement.Domain.Entities;

public class Rol
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<RolMenu> Menus { get; set; } = [];
}

public class UserStatus
{
    public string Id { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class User : ISoftDelete
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Names { get; set; } = string.Empty;
    public string LastNames { get; set; } = string.Empty;

    /// <summary>Correo cifrado con AES-256.</summary>
    public string Email { get; set; } = string.Empty;
    /// <summary>HMAC-SHA256 del correo normalizado (búsqueda y unicidad).</summary>
    public string EmailHash { get; set; } = string.Empty;

    /// <summary>Identificación cifrada con AES-256.</summary>
    public string Identification { get; set; } = string.Empty;
    /// <summary>HMAC-SHA256 de la identificación (búsqueda y unicidad).</summary>
    public string IdentificationHash { get; set; } = string.Empty;

    /// <summary>Hash BCrypt de la contraseña.</summary>
    public string Password { get; set; } = string.Empty;

    public int RolId { get; set; }
    public Rol? Rol { get; set; }

    public DateTimeOffset CreationDate { get; set; }
    public int? UserCreateId { get; set; }
    public User? UserCreate { get; set; }
    public int? UserApprovalId { get; set; }
    public User? UserApproval { get; set; }
    public DateTimeOffset? DateApproval { get; set; }

    public string StatusId { get; set; } = string.Empty;
    public UserStatus? Status { get; set; }

    public bool SessionActive { get; set; }
    public DateTimeOffset? LastLogin { get; set; }
    public DateTimeOffset? LastLogout { get; set; }
    public int FailedAttempts { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<UserCash> Cashes { get; set; } = [];

    public string FullName => $"{Names} {LastNames}".Trim();

    public bool IsBlocked => StatusId == UserStatuses.Blocked;

    /// <summary>
    /// Suma un intento fallido de inicio de sesión. Al llegar al máximo, el usuario queda
    /// bloqueado. Devuelve cuántos intentos le quedan.
    /// </summary>
    public int RegisterFailedLogin()
    {
        FailedAttempts++;
        var remaining = SecurityPolicy.MaxFailedAttempts - FailedAttempts;
        if (remaining > 0) return remaining;

        StatusId = UserStatuses.Blocked;
        return 0;
    }

    public void RegisterSuccessfulLogin(DateTimeOffset now)
    {
        FailedAttempts = 0;
        SessionActive = true;
        LastLogin = now;
    }

    public void Logout(DateTimeOffset now)
    {
        SessionActive = false;
        LastLogout = now;
    }

    /// <summary>Recuperar la contraseña también desbloquea al usuario.</summary>
    public void ResetPassword(string passwordHash)
    {
        Password = passwordHash;
        FailedAttempts = 0;
        if (IsBlocked) StatusId = UserStatuses.Active;
    }

    /// <summary>Activar, inactivar o bloquear. Inactivar o bloquear cierra su sesión.</summary>
    public void ChangeStatus(string newStatus)
    {
        if (StatusId == UserStatuses.Pending)
            throw new BusinessException("El usuario está pendiente de aprobación. Use la opción aprobar o rechazar.");

        if (newStatus is not (UserStatuses.Active or UserStatuses.Inactive or UserStatuses.Blocked))
            throw new BusinessException("El estado debe ser ACT (activo), INA (inactivo) o BLO (bloqueado).");

        StatusId = newStatus;
        if (newStatus == UserStatuses.Active)
            FailedAttempts = 0;
        else
            SessionActive = false;
    }

    public void Approve(int approverId, DateTimeOffset now) => ResolvePending(UserStatuses.Active, approverId, now);

    public void Reject(int approverId, DateTimeOffset now) => ResolvePending(UserStatuses.Inactive, approverId, now);

    private void ResolvePending(string newStatus, int approverId, DateTimeOffset now)
    {
        if (StatusId != UserStatuses.Pending)
            throw new BusinessException("El usuario no está pendiente de aprobación.");

        StatusId = newStatus;
        UserApprovalId = approverId;
        DateApproval = now;
    }
}

public class PasswordReset
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public bool Used { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class Menu
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public int? ParentId { get; set; }
    public int SortOrder { get; set; }
    public bool Active { get; set; }

    public ICollection<RolMenu> Roles { get; set; } = [];
}

public class RolMenu
{
    public int RolId { get; set; }
    public Rol? Rol { get; set; }
    public int MenuId { get; set; }
    public Menu? Menu { get; set; }
}
