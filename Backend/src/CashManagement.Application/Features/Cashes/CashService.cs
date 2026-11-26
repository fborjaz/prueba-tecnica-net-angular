using CashManagement.Application.Interfaces;
using CashManagement.Domain.Constants;
using CashManagement.Domain.Entities;
using CashManagement.Domain.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using CashManagement.Application.Common;

namespace CashManagement.Application.Features.Cashes;

public record CashUserDto(int UserId, string Username, string FullName, DateTimeOffset AssignedAt);

public record CashSessionDto(int Id, int CashId, string CashDescription, int UserId, string Username, DateTimeOffset OpenedAt, DateTimeOffset? ClosedAt);

public record CashDto(int Id, string Description, bool Active, IReadOnlyList<CashUserDto> Cashiers, CashSessionDto? CurrentSession);

public record SaveCashRequest(string Description, bool Active = true);

public record AssignCashierRequest(int UserId);

public class SaveCashRequestValidator : AbstractValidator<SaveCashRequest>
{
    public SaveCashRequestValidator()
    {
        RuleFor(x => x.Description).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("La descripción de la caja es obligatoria.")
            .MaximumLength(50).WithMessage("La descripción no debe superar 50 caracteres.");
    }
}

public interface ICashService
{
    Task<IReadOnlyList<CashDto>> GetAllAsync(CancellationToken ct = default);
    Task<CashDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<CashDto> CreateAsync(SaveCashRequest request, CancellationToken ct = default);
    Task<CashDto> UpdateAsync(int id, SaveCashRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task<CashDto> AssignCashierAsync(int cashId, AssignCashierRequest request, CancellationToken ct = default);
    Task<CashDto> UnassignCashierAsync(int cashId, int userId, CancellationToken ct = default);
    Task<IReadOnlyList<CashDto>> GetMyCashesAsync(CancellationToken ct = default);
    Task<CashSessionDto?> GetCurrentSessionAsync(CancellationToken ct = default);
    Task<CashSessionDto> OpenAsync(int cashId, CancellationToken ct = default);
    Task CloseAsync(CancellationToken ct = default);

    /// <summary>Sesión de caja abierta del cajero actual; lanza excepción si no tiene una.</summary>
    Task<CashSession> RequireOpenSessionAsync(CancellationToken ct = default);
}

public class CashService(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IValidator<SaveCashRequest> validator) : ICashService
{
    public async Task<IReadOnlyList<CashDto>> GetAllAsync(CancellationToken ct = default)
    {
        var cashes = await LoadQuery().OrderBy(c => c.Id).ToListAsync(ct);
        return cashes.Select(ToDto).ToList();
    }

    public async Task<CashDto> GetByIdAsync(int id, CancellationToken ct = default) =>
        ToDto(await LoadQuery().FirstOrDefaultAsync(c => c.Id == id, ct)
              ?? throw new NotFoundException($"No existe la caja con id {id}."));

    public async Task<CashDto> CreateAsync(SaveCashRequest request, CancellationToken ct = default)
    {
        await validator.EnsureValidAsync(request, ct);
        var cash = new Cash { Description = request.Description.Trim(), Active = request.Active };
        db.Cashes.Add(cash);
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(cash.Id, ct);
    }

    public async Task<CashDto> UpdateAsync(int id, SaveCashRequest request, CancellationToken ct = default)
    {
        await validator.EnsureValidAsync(request, ct);
        var cash = await FindAsync(id, ct);

        if (!request.Active && await HasOpenSessionAsync(id, ct))
        {
            throw new ConflictException("No se puede desactivar una caja que está siendo utilizada.");
        }

        cash.Description = request.Description.Trim();
        cash.Active = request.Active;
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var cash = await FindAsync(id, ct);
        if (await HasOpenSessionAsync(id, ct))
        {
            throw new ConflictException("No se puede eliminar una caja que está siendo utilizada.");
        }

        db.Cashes.Remove(cash); // eliminación lógica
        var assignments = await db.UserCashes.Where(uc => uc.CashId == id && uc.Active).ToListAsync(ct);
        assignments.ForEach(a => a.Active = false);
        await db.SaveChangesAsync(ct);
    }

    public async Task<CashDto> AssignCashierAsync(int cashId, AssignCashierRequest request, CancellationToken ct = default)
    {
        var cash = await FindAsync(cashId, ct);
        if (!cash.Active) throw new BusinessException("La caja está inactiva.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId && !u.IsDeleted, ct)
                   ?? throw new NotFoundException($"No existe el usuario con id {request.UserId}.");

        if (user.RolId != Roles.CashierId) throw new BusinessException("Solo se pueden asignar usuarios con rol CAJERO.");
        if (user.StatusId != UserStatuses.Active) throw new BusinessException("El cajero debe estar activo para asignarlo a una caja.");

        var assignment = await db.UserCashes.FirstOrDefaultAsync(uc => uc.CashId == cashId && uc.UserId == request.UserId, ct);
        if (assignment is { Active: true })
        {
            throw new ConflictException($"El cajero {user.Username} ya está asignado a esta caja.");
        }

        Cash.EnsureRoomForCashier(await db.UserCashes.CountAsync(uc => uc.CashId == cashId && uc.Active, ct));

        if (assignment is null)
        {
            db.UserCashes.Add(new UserCash
            {
                CashId = cashId,
                UserId = request.UserId,
                AssignedById = currentUser.UserId,
                AssignedAt = DateTimeOffset.UtcNow,
                Active = true
            });
        }
        else
        {
            assignment.Active = true;
            assignment.AssignedById = currentUser.UserId;
            assignment.AssignedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(cashId, ct);
    }

    public async Task<CashDto> UnassignCashierAsync(int cashId, int userId, CancellationToken ct = default)
    {
        var assignment = await db.UserCashes.FirstOrDefaultAsync(uc => uc.CashId == cashId && uc.UserId == userId && uc.Active, ct)
                         ?? throw new NotFoundException("El cajero no está asignado a esta caja.");

        if (await db.CashSessions.AnyAsync(s => s.CashId == cashId && s.UserId == userId && s.ClosedAt == null, ct))
        {
            throw new ConflictException("El cajero tiene la caja abierta. Debe cerrarla antes de quitar la asignación.");
        }

        assignment.Active = false;
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(cashId, ct);
    }

    public async Task<IReadOnlyList<CashDto>> GetMyCashesAsync(CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var cashes = await LoadQuery()
            .Where(c => c.Users.Any(uc => uc.UserId == userId && uc.Active))
            .OrderBy(c => c.Id)
            .ToListAsync(ct);
        return cashes.Select(ToDto).ToList();
    }

    public async Task<CashSessionDto?> GetCurrentSessionAsync(CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var session = await db.CashSessions
            .Include(s => s.Cash).Include(s => s.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.ClosedAt == null, ct);
        return session is null ? null : ToSessionDto(session);
    }

    public async Task<CashSessionDto> OpenAsync(int cashId, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var cash = await FindAsync(cashId, ct);
        if (!cash.Active) throw new BusinessException("La caja está inactiva.");

        if (!await db.UserCashes.AnyAsync(uc => uc.CashId == cashId && uc.UserId == userId && uc.Active, ct))
        {
            throw new ForbiddenException("No está asignado a esta caja.");
        }

        // Regla: los 2 cajeros de una caja no pueden operarla al mismo tiempo.
        var occupied = await db.CashSessions.Include(s => s.User)
            .FirstOrDefaultAsync(s => s.CashId == cashId && s.ClosedAt == null, ct);
        if (occupied is not null)
        {
            if (occupied.UserId == userId) return ToSessionDto(occupied, cash);
            throw new ConflictException($"La caja está siendo utilizada por {occupied.User?.Username}. Espere a que cierre su sesión de caja.");
        }

        if (await db.CashSessions.AnyAsync(s => s.UserId == userId && s.ClosedAt == null, ct))
        {
            throw new ConflictException("Ya tiene otra caja abierta. Ciérrela antes de abrir una nueva.");
        }

        var session = new CashSession { CashId = cashId, UserId = userId, OpenedAt = DateTimeOffset.UtcNow };
        db.CashSessions.Add(session);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Índice único filtrado: otro cajero abrió la caja en el mismo instante.
            throw new ConflictException("La caja acaba de ser abierta por otro cajero.");
        }

        return (await GetCurrentSessionAsync(ct))!;
    }

    public async Task CloseAsync(CancellationToken ct = default)
    {
        var session = await RequireOpenSessionAsync(ct);

        if (await db.Attentions.AnyAsync(a => a.Turn!.CashId == session.CashId
                                              && a.UserId == session.UserId
                                              && a.StatusId == AttentionStatuses.InProgress, ct))
        {
            throw new ConflictException("Tiene una atención en curso. Finalícela antes de cerrar la caja.");
        }

        session.Close(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task<CashSession> RequireOpenSessionAsync(CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        return await db.CashSessions.Include(s => s.Cash)
                   .FirstOrDefaultAsync(s => s.UserId == userId && s.ClosedAt == null, ct)
               ?? throw new BusinessException("Debe abrir una caja antes de realizar procesos de caja.");
    }

    // ----------------------------------------------------------------- helpers

    private Task<bool> HasOpenSessionAsync(int cashId, CancellationToken ct) =>
        db.CashSessions.AnyAsync(s => s.CashId == cashId && s.ClosedAt == null, ct);

    private async Task<Cash> FindAsync(int id, CancellationToken ct) =>
        await db.Cashes.FirstOrDefaultAsync(c => c.Id == id, ct)
        ?? throw new NotFoundException($"No existe la caja con id {id}.");

    private IQueryable<Cash> LoadQuery() => db.Cashes
        .Include(c => c.Users.Where(uc => uc.Active)).ThenInclude(uc => uc.User)
        .Include(c => c.Sessions.Where(s => s.ClosedAt == null)).ThenInclude(s => s.User)
        .AsNoTracking()
        .AsSplitQuery();

    private static CashDto ToDto(Cash c)
    {
        var session = c.Sessions.FirstOrDefault(s => s.ClosedAt == null);
        return new CashDto(
            c.Id,
            c.Description,
            c.Active,
            c.Users.Where(uc => uc.Active && uc.User is not null)
                .Select(uc => new CashUserDto(uc.UserId, uc.User!.Username, uc.User.FullName, uc.AssignedAt))
                .ToList(),
            session is null ? null : ToSessionDto(session, c));
    }

    private static CashSessionDto ToSessionDto(CashSession s, Cash? cash = null) => new(
        s.Id,
        s.CashId,
        (cash ?? s.Cash)?.Description ?? string.Empty,
        s.UserId,
        s.User?.Username ?? string.Empty,
        s.OpenedAt,
        s.ClosedAt);
}
