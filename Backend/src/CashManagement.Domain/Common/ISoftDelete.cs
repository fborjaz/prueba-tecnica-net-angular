namespace CashManagement.Domain.Common;

/// <summary>Entidad con eliminación lógica.</summary>
public interface ISoftDelete
{
    bool IsDeleted { get; set; }
}
