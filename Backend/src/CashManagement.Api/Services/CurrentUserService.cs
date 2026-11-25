using CashManagement.Application.Interfaces;
using CashManagement.Domain.Exceptions;

namespace CashManagement.Api.Services;

/// <summary>Lee el usuario autenticado desde las claims del JWT ya descifrado.</summary>
public class CurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
{
    private System.Security.Claims.ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public int? UserId => int.TryParse(Principal?.FindFirst("sub")?.Value, out var id) ? id : null;

    public string? Username => Principal?.FindFirst("unique_name")?.Value;

    public string? Role => Principal?.FindFirst("role")?.Value;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public int RequireUserId() => UserId ?? throw new UnauthorizedException("Sesión no válida.");
}
