using CashManagement.Domain.Common;
using CashManagement.Domain.Constants;
using CashManagement.Domain.Exceptions;

namespace CashManagement.Domain.Entities;

public class Cash : ISoftDelete
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool Active { get; set; } = true;
    public bool IsDeleted { get; set; }

    public ICollection<UserCash> Users { get; set; } = [];
    public ICollection<CashSession> Sessions { get; set; } = [];

    /// <summary>Una caja admite como máximo 2 cajeros asignados.</summary>
    public static void EnsureRoomForCashier(int assignedCashiers)
    {
        if (assignedCashiers >= SecurityPolicy.MaxCashiersPerCash)
            throw new ConflictException($"La caja ya tiene el máximo de {SecurityPolicy.MaxCashiersPerCash} cajeros asignados.");
    }
}

/// <summary>Asignación de un cajero a una caja (máximo 2 activos por caja).</summary>
public class UserCash
{
    public int UserId { get; set; }
    public User? User { get; set; }
    public int CashId { get; set; }
    public Cash? Cash { get; set; }
    public int? AssignedById { get; set; }
    public DateTimeOffset AssignedAt { get; set; }
    public bool Active { get; set; } = true;
}

/// <summary>Sesión de trabajo de un cajero sobre una caja. Solo una abierta por caja.</summary>
public class CashSession
{
    public int Id { get; set; }
    public int CashId { get; set; }
    public Cash? Cash { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public DateTimeOffset OpenedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }

    public bool IsOpen => ClosedAt is null;

    public void Close(DateTimeOffset now)
    {
        if (!IsOpen)
            throw new BusinessException("La caja ya está cerrada.");

        ClosedAt = now;
    }
}
