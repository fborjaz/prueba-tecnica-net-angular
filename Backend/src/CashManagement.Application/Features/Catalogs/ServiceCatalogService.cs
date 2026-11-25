using CashManagement.Application.Common;
using CashManagement.Application.Interfaces;
using CashManagement.Domain.Entities;
using CashManagement.Domain.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CashManagement.Application.Features.Catalogs;

public record ServiceDto(int Id, string Name, string Description, int SpeedMbps, decimal Price, bool Active, IReadOnlyList<string> Devices);

public record SaveServiceRequest(string Name, string Description, int SpeedMbps, decimal Price, IReadOnlyList<string> Devices, bool Active = true);

public class SaveServiceRequestValidator : AbstractValidator<SaveServiceRequest>
{
    public SaveServiceRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("El nombre del servicio es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre no debe superar 100 caracteres.");
        RuleFor(x => x.Description).NotEmpty().WithMessage("La descripción es obligatoria.")
            .MaximumLength(150).WithMessage("La descripción no debe superar 150 caracteres.");
        RuleFor(x => x.SpeedMbps).InclusiveBetween(1, 10000).WithMessage("La velocidad debe estar entre 1 y 10000 Mbps.");
        RuleFor(x => x.Price).GreaterThan(0).WithMessage("El precio debe ser mayor a cero.")
            .PrecisionScale(10, 2, true).WithMessage("El precio admite máximo 2 decimales.");
        RuleFor(x => x.Devices).NotEmpty().WithMessage("Debe registrar al menos un equipo.");
        RuleForEach(x => x.Devices).NotEmpty().MaximumLength(50).WithMessage("El nombre del equipo no debe superar 50 caracteres.");
    }
}

public interface IServiceCatalogService
{
    Task<IReadOnlyList<ServiceDto>> GetAllAsync(bool onlyActive, CancellationToken ct = default);
    Task<ServiceDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ServiceDto> CreateAsync(SaveServiceRequest request, CancellationToken ct = default);
    Task<ServiceDto> UpdateAsync(int id, SaveServiceRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public class ServiceCatalogService(IApplicationDbContext db, IValidator<SaveServiceRequest> validator) : IServiceCatalogService
{
    public async Task<IReadOnlyList<ServiceDto>> GetAllAsync(bool onlyActive, CancellationToken ct = default)
    {
        var query = db.Services.Include(s => s.Devices).AsNoTracking();
        if (onlyActive) query = query.Where(s => s.Active);
        var list = await query.OrderBy(s => s.SpeedMbps).ToListAsync(ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<ServiceDto> GetByIdAsync(int id, CancellationToken ct = default) =>
        ToDto(await db.Services.Include(s => s.Devices).AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct)
              ?? throw new NotFoundException($"No existe el servicio con id {id}."));

    public async Task<ServiceDto> CreateAsync(SaveServiceRequest request, CancellationToken ct = default)
    {
        await validator.EnsureValidAsync(request, ct);
        var service = new Service();
        Map(request, service);
        foreach (var device in request.Devices) service.Devices.Add(new Device { Name = device.Trim() });

        db.Services.Add(service);
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(service.Id, ct);
    }

    public async Task<ServiceDto> UpdateAsync(int id, SaveServiceRequest request, CancellationToken ct = default)
    {
        await validator.EnsureValidAsync(request, ct);
        var service = await db.Services.Include(s => s.Devices).FirstOrDefaultAsync(s => s.Id == id, ct)
                      ?? throw new NotFoundException($"No existe el servicio con id {id}.");

        Map(request, service);

        // Sincroniza equipos: los que ya no vienen se eliminan lógicamente, los nuevos se agregan.
        var names = request.Devices.Select(d => d.Trim()).ToList();
        foreach (var device in service.Devices.Where(d => !names.Contains(d.Name)).ToList())
        {
            db.Devices.Remove(device);
        }
        foreach (var name in names.Where(n => service.Devices.All(d => d.Name != n)))
        {
            service.Devices.Add(new Device { Name = name });
        }

        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var service = await db.Services.FirstOrDefaultAsync(s => s.Id == id, ct)
                      ?? throw new NotFoundException($"No existe el servicio con id {id}.");

        if (await db.Contracts.AnyAsync(c => c.ServiceId == id && (c.StatusId == "VIG" || c.StatusId == "REN"), ct))
        {
            throw new ConflictException("El servicio tiene contratos vigentes; solo puede desactivarse.");
        }

        db.Services.Remove(service); // eliminación lógica
        await db.SaveChangesAsync(ct);
    }

    private static void Map(SaveServiceRequest request, Service service)
    {
        service.Name = request.Name.Trim();
        service.Description = request.Description.Trim();
        service.SpeedMbps = request.SpeedMbps;
        service.Price = request.Price;
        service.Active = request.Active;
    }

    private static ServiceDto ToDto(Service s) => new(
        s.Id, s.Name, s.Description, s.SpeedMbps, s.Price, s.Active,
        s.Devices.Where(d => !d.IsDeleted).Select(d => d.Name).ToList());
}
