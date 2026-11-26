using System.Text.RegularExpressions;
using CashManagement.Application.Common;
using CashManagement.Application.Features.Cashes;
using CashManagement.Application.Interfaces;
using CashManagement.Domain.Common;
using CashManagement.Domain.Constants;
using CashManagement.Domain.Entities;
using CashManagement.Domain.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CashManagement.Application.Features.Turns;

public record TurnDto(
    int Id,
    string Code,
    DateTimeOffset Date,
    int CashId,
    string CashDescription,
    int GestorId,
    string GestorUsername,
    string AttentionTypeId,
    string AttentionType,
    int AttentionId,
    int StatusId,
    string Status,
    int? ClientId,
    string? ClientName,
    string? ClientIdentification,
    string? AttendedBy,
    DateTimeOffset? AttendedAt,
    string? Notes);

public record TurnQuery
{
    public DateOnly? Date { get; init; }
    public int? CashId { get; init; }
    public int? StatusId { get; init; }
    public string? AttentionTypeId { get; init; }
}

public record CreateTurnRequest(int CashId, string AttentionTypeId);

public record UpdateTurnRequest(int CashId);

public record CompleteAttentionRequest(int ClientId, string? Notes);

public record CancelAttentionRequest(string? Notes);

public class CreateTurnRequestValidator : AbstractValidator<CreateTurnRequest>
{
    public CreateTurnRequestValidator()
    {
        RuleFor(x => x.CashId).GreaterThan(0).WithMessage("Debe seleccionar una caja.");
        RuleFor(x => x.AttentionTypeId).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Debe seleccionar el tipo de atención.")
            .Matches("^[A-Z]{2}$").WithMessage("El tipo de atención debe ser un código de 2 letras mayúsculas.");
    }
}

public class CompleteAttentionRequestValidator : AbstractValidator<CompleteAttentionRequest>
{
    public CompleteAttentionRequestValidator()
    {
        RuleFor(x => x.ClientId).GreaterThan(0).WithMessage("Debe identificar al cliente atendido.");
        RuleFor(x => x.Notes).MaximumLength(250).WithMessage("Las observaciones no deben superar 250 caracteres.");
    }
}

public interface ITurnService
{
    Task<IReadOnlyList<TurnDto>> GetAllAsync(TurnQuery query, CancellationToken ct = default);
    Task<TurnDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<TurnDto> CreateAsync(CreateTurnRequest request, CancellationToken ct = default);
    Task<TurnDto> UpdateAsync(int id, UpdateTurnRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);

    // Procesos del cajero sobre la atención del turno
    Task<IReadOnlyList<TurnDto>> GetQueueAsync(CancellationToken ct = default);
    Task<TurnDto> StartAttentionAsync(int turnId, CancellationToken ct = default);
    Task<TurnDto> CompleteAttentionAsync(int turnId, CompleteAttentionRequest request, CancellationToken ct = default);
    Task<TurnDto> CancelAttentionAsync(int turnId, CancelAttentionRequest request, CancellationToken ct = default);
}

public partial class TurnService(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IStoredProcedureService storedProcedures,
    ICashService cashService,
    IValidator<CreateTurnRequest> createValidator,
    IValidator<CompleteAttentionRequest> completeValidator,
    ILoggerFactory loggerFactory) : ITurnService
{
    // El turno se crea con un procedimiento almacenado (fuera del ChangeTracker de EF), por eso se audita aquí.
    private readonly ILogger _audit = loggerFactory.CreateLogger(LogCategories.Audit);

    [GeneratedRegex(ValidationRules.TurnCode)]
    private static partial Regex TurnCodeRegex();

    public async Task<IReadOnlyList<TurnDto>> GetAllAsync(TurnQuery query, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var date = query.Date ?? BusinessTime.Today;
        var turns = BaseQuery().Where(t => t.TurnDate == date);

        // El gestor ve los turnos que asignó; el cajero los de sus cajas; el administrador todos.
        turns = currentUser.Role switch
        {
            Roles.Manager => turns.Where(t => t.GestorId == userId),
            Roles.Cashier => turns.Where(t => t.Cash!.Users.Any(uc => uc.UserId == userId && uc.Active)),
            _ => turns
        };

        if (query.CashId is not null) turns = turns.Where(t => t.CashId == query.CashId);
        if (query.StatusId is not null) turns = turns.Where(t => t.Attention!.StatusId == query.StatusId);
        if (!string.IsNullOrWhiteSpace(query.AttentionTypeId)) turns = turns.Where(t => t.AttentionTypeId == query.AttentionTypeId);

        var list = await turns.OrderByDescending(t => t.Date).ToListAsync(ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<TurnDto> GetByIdAsync(int id, CancellationToken ct = default) =>
        ToDto(await BaseQuery().FirstOrDefaultAsync(t => t.Id == id, ct)
              ?? throw new NotFoundException($"No existe el turno con id {id}."));

    public async Task<TurnDto> CreateAsync(CreateTurnRequest request, CancellationToken ct = default)
    {
        await createValidator.EnsureValidAsync(request, ct);

        if (!await db.UserCashes.AnyAsync(uc => uc.CashId == request.CashId && uc.Active, ct))
        {
            throw new BusinessException("La caja no tiene cajeros asignados. Asigne un cajero antes de generar turnos.");
        }

        await EnsureCashIsOpenAsync(request.CashId, ct);

        // El código correlativo (AC0001, PS0001...) lo genera el procedimiento almacenado sp_generate_turn.
        var generated = await storedProcedures.GenerateTurnAsync(
            request.CashId, request.AttentionTypeId, currentUser.RequireUserId(), ct);

        if (!TurnCodeRegex().IsMatch(generated.Description))
        {
            throw new BusinessException("El código de turno generado no cumple el formato de 2 letras y 4 números.");
        }

        _audit.LogInformation("CREACION Turn [Id={EntityId}] por {User}. Description={Code}, CashId={CashId}, AttentionTypeId={Type} (sp_generate_turn)",
            generated.TurnId, currentUser.Username, generated.Description, request.CashId, request.AttentionTypeId);

        return await GetByIdAsync(generated.TurnId, ct);
    }

    public async Task<TurnDto> UpdateAsync(int id, UpdateTurnRequest request, CancellationToken ct = default)
    {
        var turn = await FindPendingAsync(id, ct);

        var cash = await db.Cashes.FirstOrDefaultAsync(c => c.Id == request.CashId && c.Active, ct)
                   ?? throw new BusinessException("La caja no existe o está inactiva.");
        await EnsureCashIsOpenAsync(cash.Id, ct);

        turn.CashId = cash.Id;
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var turn = await FindPendingAsync(id, ct);
        turn.Attention!.StatusId = AttentionStatuses.Cancelled;
        db.Turns.Remove(turn); // eliminación lógica
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<TurnDto>> GetQueueAsync(CancellationToken ct = default)
    {
        var session = await cashService.RequireOpenSessionAsync(ct);
        var today = BusinessTime.Today;

        var turns = await BaseQuery()
            .Where(t => t.CashId == session.CashId && t.TurnDate == today)
            .Where(t => t.Attention!.StatusId == AttentionStatuses.Pending
                        || (t.Attention.StatusId == AttentionStatuses.InProgress && t.Attention.UserId == session.UserId))
            .OrderBy(t => t.Date)
            .ToListAsync(ct);

        return turns.Select(ToDto).ToList();
    }

    public async Task<TurnDto> StartAttentionAsync(int turnId, CancellationToken ct = default)
    {
        var session = await cashService.RequireOpenSessionAsync(ct);
        var turn = await FindForCashAsync(turnId, session.CashId, ct);

        if (await db.Attentions.AnyAsync(a => a.UserId == session.UserId && a.StatusId == AttentionStatuses.InProgress, ct))
        {
            throw new ConflictException("Ya tiene un turno en atención. Finalícelo antes de llamar al siguiente.");
        }

        turn.Attention!.Start(session.UserId);
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(turnId, ct);
    }

    public async Task<TurnDto> CompleteAttentionAsync(int turnId, CompleteAttentionRequest request, CancellationToken ct = default)
    {
        await completeValidator.EnsureValidAsync(request, ct);
        var session = await cashService.RequireOpenSessionAsync(ct);
        var turn = await FindForCashAsync(turnId, session.CashId, ct);
        var attention = turn.Attention!;

        if (!await db.Clients.AnyAsync(c => c.Id == request.ClientId, ct))
        {
            throw new NotFoundException("El cliente indicado no existe.");
        }

        attention.Complete(session.UserId, request.ClientId, request.Notes, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(turnId, ct);
    }

    public async Task<TurnDto> CancelAttentionAsync(int turnId, CancelAttentionRequest request, CancellationToken ct = default)
    {
        var session = await cashService.RequireOpenSessionAsync(ct);
        var turn = await FindForCashAsync(turnId, session.CashId, ct);
        var attention = turn.Attention!;

        attention.Cancel(session.UserId, request.Notes, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(turnId, ct);
    }

    // ----------------------------------------------------------------- helpers

    /// <summary>Los turnos solo se asignan a cajas que un cajero tiene abiertas; si no, nadie los atendería.</summary>
    private async Task EnsureCashIsOpenAsync(int cashId, CancellationToken ct)
    {
        if (!await db.CashSessions.AnyAsync(s => s.CashId == cashId && s.ClosedAt == null, ct))
        {
            var name = await db.Cashes.Where(c => c.Id == cashId).Select(c => c.Description).FirstOrDefaultAsync(ct);
            throw new BusinessException($"{name ?? "La caja"} está cerrada. Solo se asignan turnos a cajas abiertas por un cajero.");
        }
    }

    private async Task<Turn> FindPendingAsync(int id, CancellationToken ct)
    {
        var turn = await db.Turns.Include(t => t.Attention).FirstOrDefaultAsync(t => t.Id == id, ct)
                   ?? throw new NotFoundException($"No existe el turno con id {id}.");

        if (currentUser.Role == Roles.Manager && turn.GestorId != currentUser.RequireUserId())
        {
            throw new ForbiddenException("Solo puede modificar los turnos que usted asignó.");
        }

        if (turn.Attention?.StatusId != AttentionStatuses.Pending)
        {
            throw new BusinessException("Solo se pueden modificar o eliminar turnos pendientes.");
        }

        return turn;
    }

    private async Task<Turn> FindForCashAsync(int turnId, int cashId, CancellationToken ct)
    {
        var turn = await db.Turns.Include(t => t.Attention).FirstOrDefaultAsync(t => t.Id == turnId, ct)
                   ?? throw new NotFoundException($"No existe el turno con id {turnId}.");

        if (turn.CashId != cashId)
        {
            throw new ForbiddenException("El turno no pertenece a la caja que tiene abierta.");
        }

        return turn;
    }

    private IQueryable<Turn> BaseQuery() => db.Turns
        .Include(t => t.Cash)
        .Include(t => t.Gestor)
        .Include(t => t.AttentionType)
        .Include(t => t.Attention).ThenInclude(a => a!.Status)
        .Include(t => t.Attention).ThenInclude(a => a!.Client)
        .Include(t => t.Attention).ThenInclude(a => a!.User)
        .AsNoTracking()
        .AsSplitQuery();

    private static TurnDto ToDto(Turn t)
    {
        var a = t.Attention;
        return new TurnDto(
            t.Id,
            t.Description,
            t.Date,
            t.CashId,
            t.Cash?.Description ?? string.Empty,
            t.GestorId,
            t.Gestor?.Username ?? string.Empty,
            t.AttentionTypeId,
            t.AttentionType?.Description ?? t.AttentionTypeId,
            a?.Id ?? 0,
            a?.StatusId ?? 0,
            a?.Status?.Description ?? string.Empty,
            a?.ClientId,
            a?.Client is null ? null : $"{a.Client.Name} {a.Client.LastName}",
            a?.Client?.Identification,
            a?.User?.Username,
            a?.AttendedAt,
            a?.Notes);
    }
}
