using CashManagement.Application.Common;
using CashManagement.Application.Features.Turns;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CashManagement.Api.Controllers;

/// <summary>Turnos: el gestor los asigna a las cajas; el cajero los atiende.</summary>
[Route("api/turns")]
public class TurnsController(ITurnService turnService) : ApiControllerBase
{
    /// <summary>Turnos del día (o de la fecha indicada) según el rol del usuario.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TurnDto>>>> GetAll([FromQuery] TurnQuery query, CancellationToken ct) =>
        Success(await turnService.GetAllAsync(query, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<TurnDto>>> GetById(int id, CancellationToken ct) =>
        Success(await turnService.GetByIdAsync(id, ct));

    /// <summary>Genera un turno (código AC0001, PS0001...) con el procedimiento sp_generate_turn.</summary>
    [HttpPost]
    [Authorize(Roles = Manager)]
    public async Task<ActionResult<ApiResponse<TurnDto>>> Create(CreateTurnRequest request, CancellationToken ct)
    {
        var turn = await turnService.CreateAsync(request, ct);
        return Created(turn, $"Turno {turn.Code} asignado a {turn.CashDescription}.");
    }

    /// <summary>Reasigna un turno pendiente a otra caja.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = AdminOrManager)]
    public async Task<ActionResult<ApiResponse<TurnDto>>> Update(int id, UpdateTurnRequest request, CancellationToken ct) =>
        Success(await turnService.UpdateAsync(id, request, ct), "Turno actualizado correctamente.");

    /// <summary>Eliminación lógica de un turno pendiente.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = AdminOrManager)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id, CancellationToken ct)
    {
        await turnService.DeleteAsync(id, ct);
        return Done("Turno eliminado correctamente.");
    }

    /// <summary>Cola de turnos de la caja abierta por el cajero.</summary>
    [HttpGet("queue")]
    [Authorize(Roles = Cashier)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TurnDto>>>> Queue(CancellationToken ct) =>
        Success(await turnService.GetQueueAsync(ct));

    [HttpPost("{id:int}/start")]
    [Authorize(Roles = Cashier)]
    public async Task<ActionResult<ApiResponse<TurnDto>>> Start(int id, CancellationToken ct) =>
        Success(await turnService.StartAttentionAsync(id, ct), "Turno en atención.");

    [HttpPost("{id:int}/complete")]
    [Authorize(Roles = Cashier)]
    public async Task<ActionResult<ApiResponse<TurnDto>>> Complete(int id, CompleteAttentionRequest request, CancellationToken ct) =>
        Success(await turnService.CompleteAttentionAsync(id, request, ct), "Atención finalizada.");

    [HttpPost("{id:int}/cancel")]
    [Authorize(Roles = Cashier)]
    public async Task<ActionResult<ApiResponse<TurnDto>>> Cancel(int id, CancelAttentionRequest request, CancellationToken ct) =>
        Success(await turnService.CancelAttentionAsync(id, request, ct), "Turno anulado.");
}
