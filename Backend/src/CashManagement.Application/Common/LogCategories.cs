namespace CashManagement.Application.Common;

/// <summary>
/// Categorías de log dedicadas. Serilog las enruta a archivos separados:
/// <list type="bullet">
/// <item><c>Audit</c>: creación, actualización y eliminación de registros.</item>
/// <item><c>Security</c>: inicios de sesión, bloqueos, recuperación de contraseña.</item>
/// </list>
/// </summary>
public static class LogCategories
{
    public const string Audit = "Audit";
    public const string Security = "Security";
}
