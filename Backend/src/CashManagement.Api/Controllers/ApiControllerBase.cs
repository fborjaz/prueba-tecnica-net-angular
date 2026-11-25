using CashManagement.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CashManagement.Api.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
public abstract class ApiControllerBase : ControllerBase
{
    protected const string Admin = "ADMINISTRADOR";
    protected const string Manager = "GESTOR";
    protected const string Cashier = "CAJERO";
    protected const string AdminOrManager = "ADMINISTRADOR,GESTOR";

    protected ActionResult<ApiResponse<T>> Success<T>(T data, string message = "Operación realizada correctamente.") =>
        Ok(ApiResponse<T>.Ok(data, message));

    protected ActionResult<ApiResponse<T>> Created<T>(T data, string message = "Registro creado correctamente.") =>
        StatusCode(StatusCodes.Status201Created, ApiResponse<T>.Ok(data, message));

    protected ActionResult<ApiResponse<object>> Done(string message) =>
        Ok(ApiResponse<object>.Ok(null, message));
}
