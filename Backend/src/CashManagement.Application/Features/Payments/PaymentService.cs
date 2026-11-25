using CashManagement.Application.Common;
using CashManagement.Application.Features.Cashes;
using CashManagement.Application.Interfaces;
using CashManagement.Domain.Constants;
using CashManagement.Domain.Entities;
using CashManagement.Domain.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CashManagement.Application.Features.Payments;

public record PaymentDto(
    int Id,
    DateTimeOffset PaymentDate,
    int ClientId,
    string ClientName,
    string ClientIdentification,
    int ContractId,
    string ServiceName,
    int MethodPaymentId,
    string MethodPayment,
    decimal Amount,
    string? Cashier);

public record PaymentQuery
{
    public int? ClientId { get; init; }
    public int? ContractId { get; init; }
}

/// <summary>Si no se envían, el monto es el precio del servicio y la forma de pago la del contrato.</summary>
public record CreatePaymentRequest(int ContractId, decimal? Amount, int? MethodPaymentId);

public class CreatePaymentRequestValidator : AbstractValidator<CreatePaymentRequest>
{
    public CreatePaymentRequestValidator()
    {
        RuleFor(x => x.ContractId).GreaterThan(0).WithMessage("Debe seleccionar el contrato.");
        RuleFor(x => x.Amount).GreaterThan(0).When(x => x.Amount is not null).WithMessage("El monto debe ser mayor a cero.");
    }
}

public interface IPaymentService
{
    Task<IReadOnlyList<PaymentDto>> GetAllAsync(PaymentQuery query, CancellationToken ct = default);
    Task<PaymentDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<PaymentDto> CreateAsync(CreatePaymentRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public class PaymentService(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    ICashService cashService,
    IValidator<CreatePaymentRequest> validator) : IPaymentService
{
    public async Task<IReadOnlyList<PaymentDto>> GetAllAsync(PaymentQuery query, CancellationToken ct = default)
    {
        var payments = BaseQuery();
        if (query.ClientId is not null) payments = payments.Where(p => p.ClientId == query.ClientId);
        if (query.ContractId is not null) payments = payments.Where(p => p.ContractId == query.ContractId);

        var list = await payments.OrderByDescending(p => p.PaymentDate).Take(200).ToListAsync(ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<PaymentDto> GetByIdAsync(int id, CancellationToken ct = default) =>
        ToDto(await BaseQuery().FirstOrDefaultAsync(p => p.Id == id, ct)
              ?? throw new NotFoundException($"No existe el pago con id {id}."));

    public async Task<PaymentDto> CreateAsync(CreatePaymentRequest request, CancellationToken ct = default)
    {
        await validator.EnsureValidAsync(request, ct);
        await cashService.RequireOpenSessionAsync(ct);

        var contract = await db.Contracts.Include(c => c.Service)
                           .FirstOrDefaultAsync(c => c.Id == request.ContractId, ct)
                       ?? throw new NotFoundException("El contrato no existe.");

        if (!ContractStatuses.InForce.Contains(contract.StatusId))
        {
            throw new BusinessException("Solo se pueden registrar pagos de contratos vigentes.");
        }

        var methodPaymentId = request.MethodPaymentId ?? contract.MethodPaymentId;
        if (!await db.MethodPayments.AnyAsync(m => m.Id == methodPaymentId && m.Active, ct))
        {
            throw new BusinessException("La forma de pago no existe o está inactiva.");
        }

        var payment = new Payment
        {
            ContractId = contract.Id,
            ClientId = contract.ClientId,
            MethodPaymentId = methodPaymentId,
            Amount = request.Amount ?? contract.Service!.Price,
            PaymentDate = DateTimeOffset.UtcNow,
            UserId = currentUser.UserId
        };

        db.Payments.Add(payment);
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(payment.Id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var payment = await db.Payments.FirstOrDefaultAsync(p => p.Id == id, ct)
                      ?? throw new NotFoundException($"No existe el pago con id {id}.");
        db.Payments.Remove(payment); // eliminación lógica (anulación)
        await db.SaveChangesAsync(ct);
    }

    private IQueryable<Payment> BaseQuery() => db.Payments
        .Include(p => p.Client)
        .Include(p => p.Contract).ThenInclude(c => c!.Service)
        .Include(p => p.MethodPayment)
        .Include(p => p.User)
        .AsNoTracking();

    private static PaymentDto ToDto(Payment p) => new(
        p.Id,
        p.PaymentDate,
        p.ClientId,
        p.Client is null ? string.Empty : $"{p.Client.Name} {p.Client.LastName}",
        p.Client?.Identification ?? string.Empty,
        p.ContractId,
        p.Contract?.Service?.Name ?? string.Empty,
        p.MethodPaymentId,
        p.MethodPayment?.Description ?? string.Empty,
        p.Amount,
        p.User?.Username);
}
