namespace CashManagement.Domain.Common;

/// <summary>Zona horaria del negocio (Ecuador, UTC-05:00).</summary>
public static class BusinessTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(-5);

    public static DateTimeOffset Now => DateTimeOffset.UtcNow.ToOffset(Offset);

    public static DateOnly Today => DateOnly.FromDateTime(Now.DateTime);

    public static DateOnly ToBusinessDate(DateTimeOffset value) => DateOnly.FromDateTime(value.ToOffset(Offset).DateTime);
}
