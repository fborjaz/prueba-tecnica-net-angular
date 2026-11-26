using CashManagement.Application.Features.Cashes;
using CashManagement.Application.Features.Turns;
using CashManagement.Application.Interfaces;
using CashManagement.Domain.Common;
using CashManagement.Domain.Constants;
using CashManagement.Domain.Entities;
using CashManagement.Domain.Exceptions;
using CashManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CashManagement.Tests.Application;

/// <summary>
/// Reglas de negocio de los servicios contra una base en memoria: cada prueba arma su propia
/// base con lo mínimo que necesita.
/// </summary>
public class ServiceTests
{
    private const int GestorId = 2, Cajero1 = 3, Cajero2 = 4, Cajero3 = 5;

    private sealed class FakeCurrentUser(int userId) : ICurrentUserService
    {
        public int? UserId { get; set; } = userId;
        public string? Username => $"user{UserId}";
        public string? Role => null;
        public bool IsAuthenticated => true;
        public int RequireUserId() => UserId ?? throw new UnauthorizedException("Sesión no válida.");
    }

    /// <summary>Simula sp_generate_turn: registra la llamada y devuelve un turno ya sembrado.</summary>
    private sealed class FakeStoredProcedures : IStoredProcedureService
    {
        public int Calls { get; private set; }
        public Task<GeneratedTurn> GenerateTurnAsync(int cashId, string attentionTypeId, int gestorId, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(new GeneratedTurn(1, "PS0001"));
        }
        public Task<int> ChangeContractServiceAsync(int contractId, int newServiceId, int userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CashManagement.Application.Features.Dashboard.DashboardIndicatorsDto> GetDashboardIndicatorsAsync(DateOnly from, DateOnly to, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> CountAttendedTurnsAsync(int userId, string roleName, DateOnly date, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private static AppDbContext NewDb(FakeCurrentUser user)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options, user, NullLoggerFactory.Instance);

        db.Users.AddRange(
            NewUser(GestorId, "gestor2026", Roles.ManagerId),
            NewUser(Cajero1, "cajero01", Roles.CashierId),
            NewUser(Cajero2, "cajero02", Roles.CashierId),
            NewUser(Cajero3, "cajero03", Roles.CashierId));
        db.Cashes.Add(new Cash { Id = 1, Description = "Caja 01 - Matriz", Active = true });
        db.UserCashes.AddRange(
            new UserCash { UserId = Cajero1, CashId = 1, Active = true },
            new UserCash { UserId = Cajero2, CashId = 1, Active = true });
        db.SaveChanges();
        return db;
    }

    private static User NewUser(int id, string username, int rolId) => new()
    {
        Id = id, Username = username, Names = "Test", LastNames = "User", RolId = rolId,
        StatusId = UserStatuses.Active, Email = "x", EmailHash = $"e{id}", Identification = "x",
        IdentificationHash = $"i{id}", Password = "x"
    };

    private static CashService NewCashService(AppDbContext db, FakeCurrentUser user) =>
        new(db, user, new SaveCashRequestValidator());

    [Fact]
    public async Task Un_cajero_no_puede_abrir_una_caja_que_otro_tiene_abierta()
    {
        var user = new FakeCurrentUser(Cajero1);
        await using var db = NewDb(user);
        var cash = NewCashService(db, user);

        await cash.OpenAsync(1);

        user.UserId = Cajero2;
        var error = await Assert.ThrowsAsync<ConflictException>(() => cash.OpenAsync(1));
        Assert.Contains("cajero01", error.Message);
    }

    [Fact]
    public async Task No_se_asigna_un_tercer_cajero_a_la_misma_caja()
    {
        var user = new FakeCurrentUser(GestorId);
        await using var db = NewDb(user);

        await Assert.ThrowsAsync<ConflictException>(() =>
            NewCashService(db, user).AssignCashierAsync(1, new AssignCashierRequest(Cajero3)));
    }

    [Fact]
    public async Task No_se_cierra_la_caja_con_un_turno_en_atencion()
    {
        var user = new FakeCurrentUser(Cajero1);
        await using var db = NewDb(user);
        var cash = NewCashService(db, user);
        await cash.OpenAsync(1);

        db.Turns.Add(new Turn
        {
            Id = 10, Description = "PS0001", CashId = 1, GestorId = GestorId, AttentionTypeId = "PS",
            Attention = new Attention { AttentionTypeId = "PS", StatusId = AttentionStatuses.InProgress, UserId = Cajero1 }
        });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ConflictException>(() => cash.CloseAsync());
    }

    [Fact]
    public async Task No_se_generan_turnos_para_una_caja_cerrada()
    {
        var user = new FakeCurrentUser(GestorId);
        await using var db = NewDb(user);
        var procedures = new FakeStoredProcedures();
        var turns = NewTurnService(db, user, procedures);

        var error = await Assert.ThrowsAsync<BusinessException>(() =>
            turns.CreateAsync(new CreateTurnRequest(1, "PS")));

        Assert.Contains("cerrada", error.Message);
        Assert.Equal(0, procedures.Calls);
    }

    [Fact]
    public async Task Con_la_caja_abierta_el_turno_se_genera_con_el_procedimiento()
    {
        var user = new FakeCurrentUser(Cajero1);
        await using var db = NewDb(user);
        await NewCashService(db, user).OpenAsync(1);

        db.AttentionStatuses.Add(new AttentionStatus { Id = AttentionStatuses.Pending, Description = "Pendiente" });
        db.AttentionTypes.Add(new AttentionType { Id = "PS", Description = "Pago de servicio" });
        db.Turns.Add(new Turn
        {
            Id = 1, Description = "PS0001", CashId = 1, GestorId = GestorId, AttentionTypeId = "PS",
            TurnDate = BusinessTime.Today,
            Attention = new Attention { AttentionTypeId = "PS", StatusId = AttentionStatuses.Pending }
        });
        await db.SaveChangesAsync();

        user.UserId = GestorId;
        var procedures = new FakeStoredProcedures();
        var turn = await NewTurnService(db, user, procedures).CreateAsync(new CreateTurnRequest(1, "PS"));

        Assert.Equal(1, procedures.Calls);
        Assert.Equal("PS0001", turn.Code);
    }

    private static TurnService NewTurnService(AppDbContext db, FakeCurrentUser user, FakeStoredProcedures procedures) =>
        new(db, user, procedures, NewCashService(db, user),
            new CreateTurnRequestValidator(), new CompleteAttentionRequestValidator(), NullLoggerFactory.Instance);
}
