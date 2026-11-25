namespace CashManagement.Domain.Constants;

public static class Roles
{
    public const string Administrator = "ADMINISTRADOR";
    public const string Manager = "GESTOR";
    public const string Cashier = "CAJERO";
    public const string Client = "CLIENTE";

    public const int AdministratorId = 1;
    public const int ManagerId = 2;
    public const int CashierId = 3;
    public const int ClientId = 4;

    /// <summary>Roles que pueden ser creados desde la administración de usuarios.</summary>
    public static readonly int[] ManageableRoleIds = [ManagerId, CashierId];

    public static string NameOf(int rolId) => rolId switch
    {
        AdministratorId => Administrator,
        ManagerId => Manager,
        CashierId => Cashier,
        ClientId => Client,
        _ => "DESCONOCIDO"
    };
}
