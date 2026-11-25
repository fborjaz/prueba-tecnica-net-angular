using CashManagement.Application.Common;
using CashManagement.Application.Interfaces;
using CashManagement.Domain.Entities;
using CashManagement.Domain.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CashManagement.Application.Features.Catalogs;

public record CatalogItemDto(string Id, string Description);

public record MethodPaymentDto(int Id, string Description, bool Active);

public record SaveMethodPaymentRequest(string Description, bool Active = true);

public record MenuItemDto(int Id, string Name, string Route, string Icon, int SortOrder, IReadOnlyList<MenuItemDto> Children);

public class SaveMethodPaymentRequestValidator : AbstractValidator<SaveMethodPaymentRequest>
{
    public SaveMethodPaymentRequestValidator()
    {
        RuleFor(x => x.Description).NotEmpty().WithMessage("La descripción es obligatoria.")
            .MaximumLength(50).WithMessage("La descripción no debe superar 50 caracteres.");
    }
}

public interface ICatalogService
{
    Task<IReadOnlyList<CatalogItemDto>> GetRolesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CatalogItemDto>> GetUserStatusesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CatalogItemDto>> GetAttentionTypesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CatalogItemDto>> GetAttentionStatusesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CatalogItemDto>> GetContractStatusesAsync(CancellationToken ct = default);

    Task<IReadOnlyList<MethodPaymentDto>> GetMethodPaymentsAsync(bool onlyActive, CancellationToken ct = default);
    Task<MethodPaymentDto> CreateMethodPaymentAsync(SaveMethodPaymentRequest request, CancellationToken ct = default);
    Task<MethodPaymentDto> UpdateMethodPaymentAsync(int id, SaveMethodPaymentRequest request, CancellationToken ct = default);
    Task DeleteMethodPaymentAsync(int id, CancellationToken ct = default);

    /// <summary>Menú de navegación del rol del usuario autenticado, leído desde la base de datos.</summary>
    Task<IReadOnlyList<MenuItemDto>> GetMenuAsync(CancellationToken ct = default);
}

public class CatalogService(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IValidator<SaveMethodPaymentRequest> methodValidator) : ICatalogService
{
    public async Task<IReadOnlyList<CatalogItemDto>> GetRolesAsync(CancellationToken ct = default) =>
        await db.Roles.AsNoTracking().OrderBy(r => r.Id)
            .Select(r => new CatalogItemDto(r.Id.ToString(), r.Name)).ToListAsync(ct);

    public async Task<IReadOnlyList<CatalogItemDto>> GetUserStatusesAsync(CancellationToken ct = default) =>
        await db.UserStatuses.AsNoTracking()
            .Select(s => new CatalogItemDto(s.Id, s.Description)).ToListAsync(ct);

    public async Task<IReadOnlyList<CatalogItemDto>> GetAttentionTypesAsync(CancellationToken ct = default) =>
        await db.AttentionTypes.AsNoTracking()
            .Select(s => new CatalogItemDto(s.Id, s.Description)).ToListAsync(ct);

    public async Task<IReadOnlyList<CatalogItemDto>> GetAttentionStatusesAsync(CancellationToken ct = default) =>
        await db.AttentionStatuses.AsNoTracking().OrderBy(s => s.Id)
            .Select(s => new CatalogItemDto(s.Id.ToString(), s.Description)).ToListAsync(ct);

    public async Task<IReadOnlyList<CatalogItemDto>> GetContractStatusesAsync(CancellationToken ct = default) =>
        await db.StatusContracts.AsNoTracking()
            .Select(s => new CatalogItemDto(s.Id, s.Description)).ToListAsync(ct);

    public async Task<IReadOnlyList<MethodPaymentDto>> GetMethodPaymentsAsync(bool onlyActive, CancellationToken ct = default)
    {
        var query = db.MethodPayments.AsNoTracking();
        if (onlyActive) query = query.Where(m => m.Active);
        return await query.OrderBy(m => m.Id).Select(m => new MethodPaymentDto(m.Id, m.Description, m.Active)).ToListAsync(ct);
    }

    public async Task<MethodPaymentDto> CreateMethodPaymentAsync(SaveMethodPaymentRequest request, CancellationToken ct = default)
    {
        await methodValidator.EnsureValidAsync(request, ct);
        var method = new MethodPayment { Description = request.Description.Trim(), Active = request.Active };
        db.MethodPayments.Add(method);
        await db.SaveChangesAsync(ct);
        return new MethodPaymentDto(method.Id, method.Description, method.Active);
    }

    public async Task<MethodPaymentDto> UpdateMethodPaymentAsync(int id, SaveMethodPaymentRequest request, CancellationToken ct = default)
    {
        await methodValidator.EnsureValidAsync(request, ct);
        var method = await db.MethodPayments.FirstOrDefaultAsync(m => m.Id == id, ct)
                     ?? throw new NotFoundException($"No existe la forma de pago con id {id}.");
        method.Description = request.Description.Trim();
        method.Active = request.Active;
        await db.SaveChangesAsync(ct);
        return new MethodPaymentDto(method.Id, method.Description, method.Active);
    }

    public async Task DeleteMethodPaymentAsync(int id, CancellationToken ct = default)
    {
        var method = await db.MethodPayments.FirstOrDefaultAsync(m => m.Id == id, ct)
                     ?? throw new NotFoundException($"No existe la forma de pago con id {id}.");

        if (await db.Contracts.AnyAsync(c => c.MethodPaymentId == id && (c.StatusId == "VIG" || c.StatusId == "REN"), ct))
        {
            throw new ConflictException("La forma de pago está en uso por contratos vigentes; solo puede desactivarse.");
        }

        db.MethodPayments.Remove(method); // eliminación lógica
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<MenuItemDto>> GetMenuAsync(CancellationToken ct = default)
    {
        var role = currentUser.Role ?? throw new UnauthorizedException("Sesión no válida.");

        var menus = await db.RolMenus.AsNoTracking()
            .Where(rm => rm.Rol!.Name == role && rm.Menu!.Active)
            .Select(rm => rm.Menu!)
            .OrderBy(m => m.SortOrder)
            .ToListAsync(ct);

        return menus.Where(m => m.ParentId == null)
            .Select(m => Build(m, menus))
            .ToList();
    }

    private static MenuItemDto Build(Menu menu, IReadOnlyList<Menu> all) => new(
        menu.Id, menu.Name, menu.Route, menu.Icon, menu.SortOrder,
        all.Where(c => c.ParentId == menu.Id).OrderBy(c => c.SortOrder).Select(c => Build(c, all)).ToList());
}
