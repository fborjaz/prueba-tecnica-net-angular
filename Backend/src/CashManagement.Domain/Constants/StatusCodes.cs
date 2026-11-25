namespace CashManagement.Domain.Constants;

public static class UserStatuses
{
    public const string Active = "ACT";
    public const string Pending = "PEN";
    public const string Inactive = "INA";
    public const string Blocked = "BLO";

    public static readonly string[] All = [Active, Pending, Inactive, Blocked];
}

public static class AttentionStatuses
{
    public const int Pending = 1;
    public const int InProgress = 2;
    public const int Attended = 3;
    public const int Cancelled = 4;
}

public static class ContractStatuses
{
    public const string Active = "VIG";
    public const string Renewal = "REN";
    public const string Replaced = "SUS";
    public const string Cancelled = "CAN";

    /// <summary>Estados en los que el contrato sigue prestando servicio.</summary>
    public static readonly string[] InForce = [Active, Renewal];
}

public static class SecurityPolicy
{
    /// <summary>Intentos fallidos de login antes de bloquear al usuario.</summary>
    public const int MaxFailedAttempts = 3;

    /// <summary>Máximo de cajeros asignados a una caja.</summary>
    public const int MaxCashiersPerCash = 2;
}
