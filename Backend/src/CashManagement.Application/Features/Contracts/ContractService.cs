using CashManagement.Application.Common;
using CashManagement.Application.Features.Cashes;
using CashManagement.Application.Interfaces;
using CashManagement.Domain.Constants;
using CashManagement.Domain.Entities;
using CashManagement.Domain.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CashManagement.Application.Features.Contracts;

public record ContractDto(
    int Id,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    int ServiceId,
    string ServiceName,
    int SpeedMbps,
    decimal Price,
    string StatusId,
    string Status,
    int ClientId,
    string ClientName,
    string ClientIdentification,
    int MethodPaymentId,
    string MethodPayment,
    int? PreviousContractId,
    DateTimeOffset CreatedAt);

public record ContractQuery
{
    public int? ClientId { get; init; }
    public string? Identification { get; init; }
    public string? StatusId { get; init; }
}

public record CreateContractRequest(int ClientId, int ServiceId, int MethodPaymentId, int Months = 12);

public record ChangeServiceRequest(int NewServiceId);

public record ChangePaymentMethodRequest(int MethodPaymentId);

public class CreateContractRequestValidator : AbstractValidator<CreateContractRequest>
{
    public CreateContractRequestValidator()
    {
        RuleFor(x => x.ClientId).GreaterThan(0).WithMessage("Debe seleccionar el cliente.");
        RuleFor(x => x.ServiceId).GreaterThan(0).WithMessage("Debe seleccionar el servicio de internet.");
        RuleFor(x => x.MethodPaymentId).GreaterThan(0).WithMessage("Debe seleccionar la forma de pago.");
        RuleFor(x => x.Months).InclusiveBetween(1, 60).WithMessage("El plazo del contrato debe estar entre 1 y 60 meses.");
    }
}

public interface IContractService
{
    Task<IReadOnlyList<ContractDto>> GetAllAsync(ContractQuery query, CancellationToken ct = default);
    Task<ContractDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ContractDto> CreateAsync(CreateContractRequest request, CancellationToken ct = default);
    Task<ContractDto> ChangeServiceAsync(int id, ChangeServiceRequest request, CancellationToken ct = default);
    Task<ContractDto> ChangePaymentMethodAsync(int id, ChangePaymentMethodRequest request, CancellationToken ct = default);
    Task<ContractDto> CancelAsync(int id, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public class ContractService(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    ICashService cashService,
    IStoredProcedureService storedProcedures,
    IValidator<CreateContractRequest> createValidator,
    ILoggerFactory loggerFactory) : IContractService
{
    // El cambio de servicio lo ejecuta un procedimiento almacenado (fuera de EF), por eso se audita aquí.
    private readonly ILogger _audit = loggerFactory.CreateLogger(LogCategories.Audit);

    public async Task<IReadOnlyList<ContractDto>> GetAllAsync(ContractQuery query, CancellationToken ct = default)
    {
        var contracts = BaseQuery();
        if (query.ClientId is not null) contracts = contracts.Where(c => c.ClientId == query.ClientId);
        if (!string.IsNullOrWhiteSpace(query.Identification)) contracts = contracts.Where(c => c.Client!.Identification == query.Identification.Trim());
        if (!string.IsNullOrWhiteSpace(query.StatusId)) contracts = contracts.Where(c => c.StatusId == query.StatusId);

        var list = await contracts.OrderByDescending(c => c.CreatedAt).Take(200).ToListAsync(ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<ContractDto> GetByIdAsync(int id, CancellationToken ct = default) =>
        ToDto(await BaseQuery().FirstOrDefaultAsync(c => c.Id == id, ct)
              ?? throw new NotFoundException($"No existe el contrato con id {id}."));

    public async Task<ContractDto> CreateAsync(CreateContractRequest request, CancellationToken ct = default)
    {
        await createValidator.EnsureValidAsync(request, ct);
        await cashService.RequireOpenSessionAsync(ct);

        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == request.ClientId, ct)
                     ?? throw new NotFoundException("El cliente no existe.");
        if (!client.Active) throw new BusinessException("El cliente está inactivo.");

        await EnsureServiceAsync(request.ServiceId, ct);
        await EnsureMethodPaymentAsync(request.MethodPaymentId, ct);

        if (await db.Contracts.AnyAsync(c => c.ClientId == request.ClientId
                                             && c.ServiceId == request.ServiceId
                                             && ContractStatuses.InForce.Contains(c.StatusId), ct))
        {
            throw new ConflictException("El cliente ya tiene un contrato vigente con este servicio.");
        }

        var contract = Contract.Create(request.ClientId, request.ServiceId, request.MethodPaymentId,
            request.Months, DateTimeOffset.UtcNow, currentUser.UserId);

        db.Contracts.Add(contract);
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(contract.Id, ct);
    }

    /// <summary>
    /// Cambio de servicio (mayor o menor velocidad): el contrato actual pasa a SUS y se crea uno
    /// nuevo en estado REN que conserva la fecha fin. Lo ejecuta sp_change_contract_service.
    /// </summary>
    public async Task<ContractDto> ChangeServiceAsync(int id, ChangeServiceRequest request, CancellationToken ct = default)
    {
        await cashService.RequireOpenSessionAsync(ct);
        _ = await GetByIdAsync(id, ct);

        var newContractId = await storedProcedures.ChangeContractServiceAsync(
            id, request.NewServiceId, currentUser.RequireUserId(), ct);

        _audit.LogInformation("ACTUALIZACION Contract [Id={EntityId}] por {User}. StatusId -> SUS (sp_change_contract_service)", id, currentUser.Username);
        _audit.LogInformation("CREACION Contract [Id={EntityId}] por {User}. StatusId=REN, ServiceId={ServiceId}, PreviousContractId={Previous} (sp_change_contract_service)",
            newContractId, currentUser.Username, request.NewServiceId, id);

        return await GetByIdAsync(newContractId, ct);
    }

    /// <summary>Cambio de forma de pago: se actualiza en el contrato vigente.</summary>
    public async Task<ContractDto> ChangePaymentMethodAsync(int id, ChangePaymentMethodRequest request, CancellationToken ct = default)
    {
        await cashService.RequireOpenSessionAsync(ct);
        var contract = await FindInForceAsync(id, ct);
        await EnsureMethodPaymentAsync(request.MethodPaymentId, ct);

        contract.ChangePaymentMethod(request.MethodPaymentId);
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    /// <summary>Cancelación: se actualiza el estado a CAN y la fecha fin a la fecha actual.</summary>
    public async Task<ContractDto> CancelAsync(int id, CancellationToken ct = default)
    {
        await cashService.RequireOpenSessionAsync(ct);
        var contract = await FindInForceAsync(id, ct);

        contract.Cancel(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var contract = await db.Contracts.FirstOrDefaultAsync(c => c.Id == id, ct)
                       ?? throw new NotFoundException($"No existe el contrato con id {id}.");

        if (ContractStatuses.InForce.Contains(contract.StatusId))
        {
            throw new ConflictException("No se puede eliminar un contrato vigente. Cancélelo primero.");
        }

        db.Contracts.Remove(contract); // eliminación lógica
        await db.SaveChangesAsync(ct);
    }

    // ----------------------------------------------------------------- helpers

    private async Task<Contract> FindInForceAsync(int id, CancellationToken ct)
    {
        var contract = await db.Contracts.FirstOrDefaultAsync(c => c.Id == id, ct)
                       ?? throw new NotFoundException($"No existe el contrato con id {id}.");

        if (!ContractStatuses.InForce.Contains(contract.StatusId))
        {
            throw new BusinessException("La operación solo aplica a contratos vigentes.");
        }

        return contract;
    }

    private async Task EnsureServiceAsync(int serviceId, CancellationToken ct)
    {
        if (!await db.Services.AnyAsync(s => s.Id == serviceId && s.Active, ct))
            throw new BusinessException("El servicio seleccionado no existe o está inactivo.");
    }

    private async Task EnsureMethodPaymentAsync(int methodPaymentId, CancellationToken ct)
    {
        if (!await db.MethodPayments.AnyAsync(m => m.Id == methodPaymentId && m.Active, ct))
            throw new BusinessException("La forma de pago seleccionada no existe o está inactiva.");
    }

    private IQueryable<Contract> BaseQuery() => db.Contracts
        .Include(c => c.Service)
        .Include(c => c.Status)
        .Include(c => c.Client)
        .Include(c => c.MethodPayment)
        .AsNoTracking();

    private static ContractDto ToDto(Contract c) => new(
        c.Id,
        c.StartDate,
        c.EndDate,
        c.ServiceId,
        c.Service?.Name ?? string.Empty,
        c.Service?.SpeedMbps ?? 0,
        c.Service?.Price ?? 0,
        c.StatusId,
        c.Status?.Description ?? c.StatusId,
        c.ClientId,
        c.Client is null ? string.Empty : $"{c.Client.Name} {c.Client.LastName}",
        c.Client?.Identification ?? string.Empty,
        c.MethodPaymentId,
        c.MethodPayment?.Description ?? string.Empty,
        c.PreviousContractId,
        c.CreatedAt);
}
