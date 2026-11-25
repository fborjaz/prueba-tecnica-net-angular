using System.Data;
using CashManagement.Application.Features.Dashboard;
using CashManagement.Application.Interfaces;
using CashManagement.Domain.Exceptions;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CashManagement.Infrastructure.Persistence;

/// <summary>
/// Ejecuta los procedimientos almacenados y funciones con Dapper, sobre la misma conexión de EF Core.
/// Los errores de negocio lanzados con THROW 50xxx se traducen a <see cref="BusinessException"/>.
/// </summary>
public class StoredProcedureService(AppDbContext context) : IStoredProcedureService
{
    private IDbConnection Connection => context.Database.GetDbConnection();

    public Task<GeneratedTurn> GenerateTurnAsync(int cashId, string attentionTypeId, int gestorId, CancellationToken ct = default) =>
        ExecuteAsync(() => Connection.QuerySingleAsync<GeneratedTurn>(new CommandDefinition(
            "dbo.sp_generate_turn",
            new { cashid = cashId, attentiontypeid = attentionTypeId, gestorid = gestorId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct)));

    public Task<int> ChangeContractServiceAsync(int contractId, int newServiceId, int userId, CancellationToken ct = default) =>
        ExecuteAsync(() => Connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "dbo.sp_change_contract_service",
            new { contractid = contractId, newserviceid = newServiceId, userid = userId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct)));

    public Task<int> CountAttendedTurnsAsync(int userId, string roleName, DateOnly date, CancellationToken ct = default) =>
        ExecuteAsync(() => Connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT dbo.fn_count_attended_turns(@userid, @rolname, @date)",
            new { userid = userId, rolname = roleName, date = date.ToDateTime(TimeOnly.MinValue) },
            cancellationToken: ct)));

    public Task<DashboardIndicatorsDto> GetDashboardIndicatorsAsync(DateOnly from, DateOnly to, CancellationToken ct = default) =>
        ExecuteAsync(async () =>
        {
            using var grid = await Connection.QueryMultipleAsync(new CommandDefinition(
                "dbo.sp_dashboard_indicators",
                new { from = from.ToDateTime(TimeOnly.MinValue), to = to.ToDateTime(TimeOnly.MinValue) },
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct));

            var users = await grid.ReadSingleAsync<UserIndicators>();
            var cashes = (await grid.ReadAsync<CashIndicator>()).ToList();
            var cashiers = (await grid.ReadAsync<CashierIndicator>()).ToList();
            var managers = (await grid.ReadAsync<ManagerIndicator>()).ToList();
            var daily = (await grid.ReadAsync<DailyIndicator>()).ToList();

            return new DashboardIndicatorsDto(from, to, users, cashes, cashiers, managers, daily);
        });

    private static async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (SqlException ex) when (ex.Number >= 50000)
        {
            throw new BusinessException(ex.Message);
        }
    }
}
