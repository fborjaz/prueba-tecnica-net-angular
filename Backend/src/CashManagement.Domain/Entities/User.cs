using CashManagement.Domain.Common;

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
