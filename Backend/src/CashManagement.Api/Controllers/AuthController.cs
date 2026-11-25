using CashManagement.Application.Common;
using CashManagement.Application.Features.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CashManagement.Api.Controllers;

/// <summary>Autenticación: login, cierre de sesion, perfil y recuperacion de contraseña.</summary>
[Route("api/auth")]
public class AuthController(IAuthService authService) : ApiControllerBase
{
    /// <summary>Inicia sesión y devuelve un JWT. Bloquea al usuario tras 3 intentos.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login(LoginRequest request, CancellationToken ct) =>
        Success(await authService.LoginAsync(request, ct), "Bienvenido.");

    /// <summary>Cierra la sesión del usuario (y su caja abierta, si es que es cajero).</summary>
    [HttpPost("logout")]
    public async Task<ActionResult<ApiResponse<object>>> Logout(CancellationToken ct)
    {
        await authService.LogoutAsync(ct);
        return Done("Sesión cerrada correctamente.");
    }

    /// <summary>Datos del usuario autenticado.</summary>
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> Me(CancellationToken ct) =>
        Success(await authService.GetProfileAsync(ct));

    /// <summary>Envía al correo del usuario un enlace para Recuperacion de la contraseña.</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        await authService.ForgotPasswordAsync(request, ct);
        return Done("Si el correo está registrado, recibirá un enlace para restablecer su contraseña.");
    }

    /// <summary>Restablece la contraseña con el token recibido por correo.</summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        await authService.ResetPasswordAsync(request, ct);
        return Done("La contraseña fue restablecida. Ya puede iniciar sesión.");
    }
}
