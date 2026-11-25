using CashManagement.Application.Common;
using CashManagement.Application.Features.Contracts;
using CashManagement.Application.Features.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CashManagement.Api.Controllers;

/// <summary>Contratos: contratación, cambio de servicio, cambio de forma de pago y cancelación.</summary>
[Route("api/contracts")]
public class ContractsController(IContractService contractService) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ContractDto>>>> GetAll([FromQuery] ContractQuery query, CancellationToken ct) =>
        Success(await contractService.GetAllAsync(query, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<ContractDto>>> GetById(int id, CancellationToken ct) =>
        Success(await contractService.GetByIdAsync(id, ct));

    /// <summary>Contrata un servicio de internet (estado VIG).</summary>
    [HttpPost]
    [Authorize(Roles = Cashier)]
    public async Task<ActionResult<ApiResponse<ContractDto>>> Create(CreateContractRequest request, CancellationToken ct) =>
        Created(await contractService.CreateAsync(request, ct), "Contrato registrado correctamente.");

    /// <summary>
    /// Cambio de servicio: el contrato actual pasa a SUS y se crea uno nuevo en REN con la misma
    /// fecha fin (procedimiento sp_change_contract_service).
    /// </summary>
    [HttpPut("{id:int}/service")]
    [Authorize(Roles = Cashier)]
    public async Task<ActionResult<ApiResponse<ContractDto>>> ChangeService(int id, ChangeServiceRequest request, CancellationToken ct) =>
        Success(await contractService.ChangeServiceAsync(id, request, ct), "Servicio cambiado. Se generó el nuevo contrato.");

    [HttpPut("{id:int}/payment-method")]
    [Authorize(Roles = Cashier)]
    public async Task<ActionResult<ApiResponse<ContractDto>>> ChangePaymentMethod(int id, ChangePaymentMethodRequest request, CancellationToken ct) =>
        Success(await contractService.ChangePaymentMethodAsync(id, request, ct), "Forma de pago actualizada.");

    /// <summary>Cancela el contrato: estado CAN y fecha fin = fecha actual.</summary>
    [HttpPut("{id:int}/cancel")]
    [Authorize(Roles = Cashier)]
    public async Task<ActionResult<ApiResponse<ContractDto>>> Cancel(int id, CancellationToken ct) =>
        Success(await contractService.CancelAsync(id, ct), "Contrato cancelado.");

    /// <summary>Eliminación lógica (solo contratos no vigentes).</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Admin)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id, CancellationToken ct)
    {
        await contractService.DeleteAsync(id, ct);
        return Done("Contrato eliminado correctamente.");
    }
}

/// <summary>Pagos de contratos.</summary>
[Route("api/payments")]
public class PaymentsController(IPaymentService paymentService) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PaymentDto>>>> GetAll([FromQuery] PaymentQuery query, CancellationToken ct) =>
        Success(await paymentService.GetAllAsync(query, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> GetById(int id, CancellationToken ct) =>
        Success(await paymentService.GetByIdAsync(id, ct));

    /// <summary>Registra un pago. Por defecto usa el precio del servicio y la forma de pago del contrato.</summary>
    [HttpPost]
    [Authorize(Roles = Cashier)]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> Create(CreatePaymentRequest request, CancellationToken ct) =>
        Created(await paymentService.CreateAsync(request, ct), "Pago registrado correctamente.");

    /// <summary>Anulación (eliminación lógica) de un pago.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Admin)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id, CancellationToken ct)
    {
        await paymentService.DeleteAsync(id, ct);
        return Done("Pago anulado correctamente.");
    }
}
