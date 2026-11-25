using CashManagement.Application.Common;
using CashManagement.Application.Features.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CashManagement.Api.Controllers;

/// <summary>
/// CRUD de usuarios. El administrador crea gestores y cajeros activos; el gestor puede crearlos
/// pero quedan pendientes hasta que un administrador los apruebe.
/// </summary>
[Route("api/users")]
[Authorize(Roles = AdminOrManager)]
public class UsersController(IUserService userService) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<UserDto>>>> GetAll([FromQuery] UserQuery query, CancellationToken ct) =>
        Success(await userService.GetAllAsync(query, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<UserDto>>> GetById(int id, CancellationToken ct) =>
        Success(await userService.GetByIdAsync(id, ct));

    /// <summary>Usuarios pendientes de aprobación (el gestor ve solo los que él creó).</summary>
    [HttpGet("pending")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<UserDto>>>> GetPending(CancellationToken ct) =>
        Success(await userService.GetPendingApprovalAsync(ct));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<UserDto>>> Create(CreateUserRequest request, CancellationToken ct)
    {
        var user = await userService.CreateAsync(request, ct);
        return Created(user, user.StatusId == "PEN"
            ? "Usuario creado. Queda pendiente de aprobación por un administrador."
            : "Usuario creado correctamente.");
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Admin)]
    public async Task<ActionResult<ApiResponse<UserDto>>> Update(int id, UpdateUserRequest request, CancellationToken ct) =>
        Success(await userService.UpdateAsync(id, request, ct), "Usuario actualizado correctamente.");

    /// <summary>Cambia el estado: ACT (activo), INA (inactivo) o BLO (bloqueado).</summary>
    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = Admin)]
    public async Task<ActionResult<ApiResponse<UserDto>>> ChangeStatus(int id, ChangeUserStatusRequest request, CancellationToken ct) =>
        Success(await userService.ChangeStatusAsync(id, request, ct), "Estado actualizado correctamente.");

    [HttpPost("{id:int}/approve")]
    [Authorize(Roles = Admin)]
    public async Task<ActionResult<ApiResponse<UserDto>>> Approve(int id, CancellationToken ct) =>
        Success(await userService.ApproveAsync(id, ct), "Usuario aprobado correctamente.");

    [HttpPost("{id:int}/reject")]
    [Authorize(Roles = Admin)]
    public async Task<ActionResult<ApiResponse<UserDto>>> Reject(int id, CancellationToken ct) =>
        Success(await userService.RejectAsync(id, ct), "Usuario rechazado.");

    /// <summary>Eliminación lógica.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Admin)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id, CancellationToken ct)
    {
        await userService.DeleteAsync(id, ct);
        return Done("Usuario eliminado correctamente.");
    }

    /// <summary>
    /// Carga masiva de gestores y cajeros desde .xlsx o .csv.
    /// Columnas: usuario, nombres, apellidos, correo, identificacion, contrasena, rol (GESTOR/CAJERO).
    /// </summary>
    [HttpPost("bulk")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<BulkUploadResult>>> Bulk(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var result = await userService.BulkCreateAsync(stream, file.FileName, ct);
        return Success(result, $"Carga finalizada: {result.Created} creados, {result.Failed} con errores.");
    }
}
