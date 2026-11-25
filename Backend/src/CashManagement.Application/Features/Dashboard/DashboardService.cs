using CashManagement.Application.Features.Auth;
using CashManagement.Application.Features.Cashes;
using CashManagement.Application.Features.Users;
using CashManagement.Application.Interfaces;
using CashManagement.Domain.Common;
using CashManagement.Domain.Constants;
using CashManagement.Domain.Exceptions;

namespace CashManagement.Application.Features.Dashboard;

// Las clases de indicadores se mapean con Dapper desde sp_dashboard_indicators.
public class UserIndicators
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int PendingUsers { get; set; }
    public int BlockedUsers { get; set; }
    public int InactiveUsers { get; set; }
    public int ActiveSessions { get; set; }
    public int InactiveSessions { get; set; }
    public int Administrators { get; set; }
    public int Managers { get; set; }
    public int Cashiers { get; set; }
}

public class CashIndicator
{
    public int CashId { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool Active { get; set; }
    public int AssignedCashiers { get; set; }
    public string? CurrentUser { get; set; }
    public int TurnsGenerated { get; set; }
    public int TurnsAttended { get; set; }
}

public class CashierIndicator
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool SessionActive { get; set; }
    public int AssignedCashes { get; set; }
    public int TurnsAttended { get; set; }
    public decimal PaymentsAmount { get; set; }
}

public class ManagerIndicator
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int TurnsAssigned { get; set; }
    public int TurnsAttended { get; set; }
    public int UsersCreated { get; set; }
    public int UsersPending { get; set; }
}

public class DailyIndicator
{
    public DateTime Day { get; set; }
    public int TurnsGenerated { get; set; }
    public int TurnsAttended { get; set; }
}

public record DashboardIndicatorsDto(
    DateOnly From,
    DateOnly To,
    UserIndicators Users,
    IReadOnlyList<CashIndicator> Cashes,
    IReadOnlyList<CashierIndicator> Cashiers,
    IReadOnlyList<ManagerIndicator> Managers,
    IReadOnlyList<DailyIndicator> Daily);

public record WelcomeDto(
    UserProfileDto User,
    int AttendedTurnsToday,
    string AttendedTurnsScope,
    IReadOnlyList<UserDto> PendingApprovals,
    CashSessionDto? CurrentCashSession,
    IReadOnlyList<CashDto> AssignedCashes);

public interface IDashboardService
{
    Task<WelcomeDto> GetWelcomeAsync(CancellationToken ct = default);
    Task<DashboardIndicatorsDto> GetIndicatorsAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default);
}

public class DashboardService(
    IAuthService authService,
    IUserService userService,
    ICashService cashService,
    IStoredProcedureService storedProcedures) : IDashboardService
{
    public async Task<WelcomeDto> GetWelcomeAsync(CancellationToken ct = default)
    {
        var profile = await authService.GetProfileAsync(ct);
        var role = profile.Role;

        // Función fn_count_attended_turns: total del día según el rol.
        var attended = await storedProcedures.CountAttendedTurnsAsync(profile.Id, role, BusinessTime.Today, ct);

        var scope = role switch
        {
            Roles.Administrator => "Total de turnos atendidos hoy en todas las cajas",
            Roles.Manager => "Turnos atendidos hoy de los asignados por usted a los cajeros",
            _ => "Turnos atendidos hoy por usted"
        };

        var pending = role is Roles.Manager or Roles.Administrator
            ? await userService.GetPendingApprovalAsync(ct)
            : [];

        CashSessionDto? session = null;
        IReadOnlyList<CashDto> cashes = [];
        if (role == Roles.Cashier)
        {
            session = await cashService.GetCurrentSessionAsync(ct);
            cashes = await cashService.GetMyCashesAsync(ct);
        }

        return new WelcomeDto(profile, attended, scope, pending, session, cashes);
    }

    public async Task<DashboardIndicatorsDto> GetIndicatorsAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var end = to ?? BusinessTime.Today;
        var start = from ?? end.AddDays(-6);

        if (start > end) throw new BusinessException("La fecha inicial no puede ser mayor que la fecha final.");
        if (end.DayNumber - start.DayNumber > 365) throw new BusinessException("El rango de fechas no puede superar un año.");

        return await storedProcedures.GetDashboardIndicatorsAsync(start, end, ct);
    }
}
