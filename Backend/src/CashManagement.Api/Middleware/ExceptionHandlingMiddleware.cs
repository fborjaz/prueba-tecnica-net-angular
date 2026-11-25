using CashManagement.Application.Common;
using CashManagement.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CashManagement.Api.Middleware;

/// <summary>
/// Traduce las excepciones a respuestas <see cref="ApiResponse{T}"/> con su código HTTP
/// y registra alertas (4xx) y errores (5xx) con Serilog.
/// </summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var (status, message, errors) = exception switch
        {
            ValidationFailedException v => (StatusCodes.Status400BadRequest, v.Message, v.Errors),
            BusinessException => (StatusCodes.Status400BadRequest, exception.Message, null),
            NotFoundException => (StatusCodes.Status404NotFound, exception.Message, null),
            ConflictException => (StatusCodes.Status409Conflict, exception.Message, null),
            ForbiddenException => (StatusCodes.Status403Forbidden, exception.Message, null),
            UnauthorizedException => (StatusCodes.Status401Unauthorized, exception.Message, null),
            DbUpdateException => (StatusCodes.Status409Conflict,
                "No se pudo guardar el registro porque entra en conflicto con datos existentes.", null),
            _ => (StatusCodes.Status500InternalServerError,
                "Ocurrió un error inesperado. Intente nuevamente o contacte al administrador.", (IDictionary<string, string[]>?)null)
        };

        var path = $"{context.Request.Method} {context.Request.Path}";
        if (status >= 500)
        {
            logger.LogError(exception, "Error no controlado en {Path}", path);
        }
        else if (exception is DbUpdateException)
        {
            logger.LogWarning(exception, "Conflicto de base de datos en {Path}", path);
        }
        else
        {
            logger.LogWarning("Alerta {Status} en {Path}: {Message}", status, path, exception.Message);
        }

        if (context.Response.HasStarted) return;

        context.Response.Clear();
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(message, errors));
    }
}
