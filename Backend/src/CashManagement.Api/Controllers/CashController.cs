using CashManagement.Application.Common;
using CashManagement.Application.Features.Cashes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CashManagement.Api.Controllers;

/// <summary>Cajas, asignación de cajeros (2 por caja) y apertura/cierre de caja.</summary>
[Route("api/cash")]
public class CashController(ICashService cashService) : ApiControllerBase
{
    [HttpGet]
    [Authorize(Roles = AdminOrManager)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CashDto>>>> GetAll(CancellationToken ct) =>
        Success(await cashService.GetAllAsync(ct));

    [HttpGet("{id:int}")]
    [Authorize(Roles = AdminOrManager)]
    public async Task<ActionResult<ApiResponse<CashDto>>> GetById(int id, CancellationToken ct) =>
        Success(await cashService.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = Admin)]
    public async Task<ActionResult<ApiResponse<CashDto>>> Create(SaveCashRequest request, CancellationToken ct) =>
        Created(await cashService.CreateAsync(request, ct), "Caja creada correctamente.");

    [HttpPut("{id:int}")]
    [Authorize(Roles = Admin)]
    public async Task<ActionResult<ApiResponse<CashDto>>> Update(int id, SaveCashRequest request, CancellationToken ct) =>
        Success(await cashService.UpdateAsync(id, request, ct), "Caja actualizada correctamente.");

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Admin)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id, CancellationToken ct)
    {
        await cashService.DeleteAsync(id, ct);
        return Done("Caja eliminada correctamente.");
    }

    /// <summary>Asigna un cajero a la caja. Máximo 2</summary>
    [HttpPost("{id:int}/cashiers")]
    [Authorize(Roles = AdminOrManager)]
    public async Task<ActionResult<ApiResponse<CashDto>>> AssignCashier(int id, AssignCashierRequest request, CancellationToken ct) =>
        Success(await cashService.AssignCashierAsync(id, request, ct), "Cajero asignado correctamente.");

    [HttpDelete("{id:int}/cashiers/{userId:int}")]
    [Authorize(Roles = AdminOrManager)]
    public async Task<ActionResult<ApiResponse<CashDto>>> UnassignCashier(int id, int userId, CancellationToken ct) =>
        Success(await cashService.UnassignCashierAsync(id, userId, ct), "Asignación retirada correctamente.");

    /// <summary>Cajas asignadas al cajero autenticado.</summary>
    [HttpGet("mine")]
    [Authorize(Roles = Cashier)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CashDto>>>> Mine(CancellationToken ct) =>
        Success(await cashService.GetMyCashesAsync(ct));

    /// <summary>Sesión de caja abierta del cajero autenticado (null si no tiene).</summary>
    [HttpGet("session")]
    [Authorize(Roles = Cashier)]
    public async Task<ActionResult<ApiResponse<CashSessionDto?>>> CurrentSession(CancellationToken ct) =>
        Success(await cashService.GetCurrentSessionAsync(ct));

    /// <summary>Abre la caja. Falla si el otro cajero asignado la tiene abierta.</summary>
    [HttpPost("{id:int}/open")]
    [Authorize(Roles = Cashier)]
    public async Task<ActionResult<ApiResponse<CashSessionDto>>> Open(int id, CancellationToken ct) =>
        Success(await cashService.OpenAsync(id, ct), "Caja abierta. Ya puede atender turnos.");

    [HttpPost("session/close")]
    [Authorize(Roles = Cashier)]
    public async Task<ActionResult<ApiResponse<object>>> Close(CancellationToken ct)
    {
        await cashService.CloseAsync(ct);
        return Done("Caja cerrada correctamente.");
    }
}
