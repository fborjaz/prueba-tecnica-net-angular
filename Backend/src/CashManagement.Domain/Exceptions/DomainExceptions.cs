namespace CashManagement.Domain.Exceptions;

/// <summary>Regla de negocio incumplida (HTTP 400).</summary>
public class BusinessException(string message) : Exception(message);

/// <summary>Recurso inexistente (HTTP 404).</summary>
public class NotFoundException(string message) : Exception(message);

/// <summary>Conflicto con el estado actual, p. ej. duplicados o caja ocupada (HTTP 409).</summary>
public class ConflictException(string message) : Exception(message);

/// <summary>Operación no permitida para el rol del usuario (HTTP 403).</summary>
public class ForbiddenException(string message) : Exception(message);

/// <summary>Credenciales o sesión inválidas (HTTP 401).</summary>
public class UnauthorizedException(string message) : Exception(message);

/// <summary>Errores de validación de datos de entrada (HTTP 400).</summary>
public class ValidationFailedException(IDictionary<string, string[]> errors)
    : Exception("Los datos ingresados no son válidos.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;
}
