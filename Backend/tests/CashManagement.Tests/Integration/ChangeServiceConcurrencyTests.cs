using Microsoft.Data.SqlClient;

namespace CashManagement.Tests.Integration;

/// <summary>
/// Prueba de concurrencia contra SQL Server real. Se ejecuta solo si está definida la variable
/// CASHMANAGEMENT_TEST_SQL con la cadena de conexión a una base creada con los scripts, por ejemplo:
/// Server=(localdb)\MSSQLLocalDB;Database=CashManagementDb;Integrated Security=True;TrustServerCertificate=True
/// </summary>
public class ChangeServiceConcurrencyTests
{
    private static readonly string? ConnectionString = Environment.GetEnvironmentVariable("CASHMANAGEMENT_TEST_SQL");

    [Fact]
    public async Task Cambios_de_servicio_simultaneos_crean_un_solo_contrato_nuevo()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ConnectionString), "Defina CASHMANAGEMENT_TEST_SQL para ejecutar esta prueba.");

        var contractId = await ScalarAsync<int>("""
            INSERT INTO contract (startdate, enddate, service_serviceid, statuscontract_statusid, client_clientid, methodpayment_methodpaymentid)
            VALUES (SYSDATETIMEOFFSET(), DATEADD(YEAR, 1, SYSDATETIMEOFFSET()), 1, 'VIG', 3, 1);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """);

        try
        {
            // 8 cajeros piden a la vez cambiar el servicio del mismo contrato.
            var calls = Enumerable.Range(0, 8).Select(i => TryChangeServiceAsync(contractId, newServiceId: 2 + i % 3));
            var results = await Task.WhenAll(calls);

            var created = await ScalarAsync<int>($"SELECT COUNT(*) FROM contract WHERE previouscontractid = {contractId}");
            Assert.Equal(1, created);
            Assert.Equal(1, results.Count(ok => ok));
        }
        finally
        {
            await ScalarAsync<int>($"""
                DELETE FROM contract WHERE previouscontractid = {contractId};
                DELETE FROM contract WHERE contractid = {contractId};
                SELECT 0;
                """);
        }
    }

    private static async Task<bool> TryChangeServiceAsync(int contractId, int newServiceId)
    {
        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var cmd = new SqlCommand("dbo.sp_change_contract_service", connection) { CommandType = System.Data.CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@contractid", contractId);
            cmd.Parameters.AddWithValue("@newserviceid", newServiceId);
            cmd.Parameters.AddWithValue("@userid", 1);
            await cmd.ExecuteScalarAsync();
            return true;
        }
        catch (SqlException ex) when (ex.Number >= 50000)
        {
            return false; // el procedimiento rechazó el cambio porque el contrato ya no estaba vigente
        }
    }

    private static async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var cmd = new SqlCommand(sql, connection);
        return (T)Convert.ChangeType((await cmd.ExecuteScalarAsync())!, typeof(T));
    }
}
