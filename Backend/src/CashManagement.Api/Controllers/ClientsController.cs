using CashManagement.Application.Common;
using CashManagement.Application.Features.Clients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CashManagement.Api.Controllers;

/// <summary>Mantenimiento de clientes (cajero). Un cliente se registra una sola vez por identificación.</summary>
[Route("api/clients")]
[Authorize(Roles = $"{Cashier},{Admin}")]
public class ClientsController(IClientService clientService) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<ClientDto>>>> GetAll([FromQuery] ClientQuery query, CancellationToken ct) =>
        Success(await clientService.GetAllAsync(query, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<ClientDto>>> GetById(int id, CancellationToken ct) =>
        Success(await clientService.GetByIdAsync(id, ct));

    /// <summary>Busca un cliente por su número de identificación.</summary>
    [HttpGet("identification/{identification}")]
    public async Task<ActionResult<ApiResponse<ClientDto>>> GetByIdentification(string identification, CancellationToken ct) =>
        Success(await clientService.GetByIdentificationAsync(identification, ct));

    [HttpPost]
    [Authorize(Roles = Cashier)]
    public async Task<ActionResult<ApiResponse<ClientDto>>> Create(SaveClientRequest request, CancellationToken ct) =>
        Created(await clientService.CreateAsync(request, ct), "Cliente registrado correctamente.");

    [HttpPut("{id:int}")]
    [Authorize(Roles = Cashier)]
    public async Task<ActionResult<ApiResponse<ClientDto>>> Update(int id, SaveClientRequest request, CancellationToken ct) =>
        Success(await clientService.UpdateAsync(id, request, ct), "Cliente actualizado correctamente.");

    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = Cashier)]
    public async Task<ActionResult<ApiResponse<ClientDto>>> ChangeStatus(int id, ChangeClientStatusRequest request, CancellationToken ct) =>
        Success(await clientService.ChangeStatusAsync(id, request, ct), "Estado del cliente actualizado.");

    /// <summary>Eliminación lógica.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Cashier)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id, CancellationToken ct)
    {
        await clientService.DeleteAsync(id, ct);
        return Done("Cliente eliminado correctamente.");
    }

    /// <summary>
    /// Carga masiva desde .xlsx o .csv.
    /// Columnas: nombres, apellidos, identificacion, correo, telefono, direccion, referencia.
    /// </summary>
    [HttpPost("bulk")]
    [Authorize(Roles = Cashier)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<BulkUploadResult>>> Bulk(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var result = await clientService.BulkCreateAsync(stream, file.FileName, ct);
        return Success(result, $"Carga finalizada: {result.Created} creados, {result.Failed} con errores.");
    }
}
