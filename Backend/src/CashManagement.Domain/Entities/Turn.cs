using CashManagement.Domain.Common;

namespace CashManagement.Domain.Entities;

public class AttentionType
{
    /// <summary>Código de 2 letras; es el prefijo del turno (AC, PS...).</summary>
    public string Id { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class AttentionStatus
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class Turn : ISoftDelete
{
    public int Id { get; set; }
    /// <summary>Código del turno: 2 letras mayúsculas + 4 números (AC0001).</summary>
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset Date { get; set; }
    public DateOnly TurnDate { get; set; }
    public int CashId { get; set; }
    public Cash? Cash { get; set; }
    public int GestorId { get; set; }
    public User? Gestor { get; set; }
    public string AttentionTypeId { get; set; } = string.Empty;
    public AttentionType? AttentionType { get; set; }
    public bool IsDeleted { get; set; }

    public Attention? Attention { get; set; }
}

public class Attention
{
    public int Id { get; set; }
    public int TurnId { get; set; }
    public Turn? Turn { get; set; }
    public int? ClientId { get; set; }
    public Client? Client { get; set; }
    public string AttentionTypeId { get; set; } = string.Empty;
    public AttentionType? AttentionType { get; set; }
    public int StatusId { get; set; }
    public AttentionStatus? Status { get; set; }
    /// <summary>Cajero que atendió el turno.</summary>
    public int? UserId { get; set; }
    public User? User { get; set; }
    public DateTimeOffset? AttendedAt { get; set; }
    public string? Notes { get; set; }
}
