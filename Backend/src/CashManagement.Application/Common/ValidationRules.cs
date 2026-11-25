using CashManagement.Domain.Exceptions;
using FluentValidation;

namespace CashManagement.Application.Common;

/// <summary>Reglas de validación de la prueba centralizadas en un solo lugar.</summary>
public static class ValidationRules
{
    /// <summary>Usuario: solo letras y números (sin caracteres especiales).</summary>
    public const string UsernameAllowedChars = @"^[A-Za-z0-9]+$";
    /// <summary>Usuario: al menos una letra y al menos un número.</summary>
    public const string UsernameLettersAndNumber = @"^(?=.*[A-Za-z])(?=.*\d).+$";

    public const string PasswordHasNumber = @"\d";
    public const string PasswordHasUpper = @"[A-Z]";

    /// <summary>Identificación: solo números, de 10 a 13 dígitos.</summary>
    public const string Identification = @"^\d{10,13}$";

    /// <summary>Teléfono: solo números, inicia con 09, mínimo 10 dígitos (máximo 13 por la columna).</summary>
    public const string Phone = @"^09\d{8,11}$";

    /// <summary>Código de turno: 2 letras mayúsculas + 4 números.</summary>
    public const string TurnCode = @"^[A-Z]{2}\d{4}$";

    /// <summary>Nombres y apellidos: letras (incluye tildes y ñ) y espacios.</summary>
    public const string PersonName = @"^[A-Za-zÁÉÍÓÚáéíóúÑñÜü ]+$";

    /// <summary>Ejecuta el validador y lanza <see cref="ValidationFailedException"/> si hay errores.</summary>
    public static async Task EnsureValidAsync<T>(this IValidator<T> validator, T instance, CancellationToken ct = default)
    {
        var result = await validator.ValidateAsync(instance, ct);
        if (!result.IsValid)
        {
            // Claves en camelCase para que coincidan con los nombres de campo del frontend.
            var errors = result.ToDictionary().ToDictionary(
                e => string.IsNullOrEmpty(e.Key) ? e.Key : char.ToLowerInvariant(e.Key[0]) + e.Key[1..],
                e => e.Value);
            throw new ValidationFailedException(errors);
        }
    }

    /// <summary>Devuelve los mensajes de error sin lanzar excepción (útil en cargas masivas).</summary>
    public static async Task<IReadOnlyList<string>> CollectErrorsAsync<T>(this IValidator<T> validator, T instance, CancellationToken ct = default)
    {
        var result = await validator.ValidateAsync(instance, ct);
        return result.Errors.Select(e => e.ErrorMessage).ToList();
    }
}
