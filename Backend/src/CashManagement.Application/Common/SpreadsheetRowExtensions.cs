using System.Globalization;
using System.Text;

namespace CashManagement.Application.Common;

public static class SpreadsheetRowExtensions
{
    /// <summary>Obtiene el valor de la primera cabecera encontrada (admite nombres en español o inglés).</summary>
    public static string Value(this IReadOnlyDictionary<string, string> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (row.TryGetValue(NormalizeHeader(key), out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    /// <summary>Normaliza una cabecera: minúsculas, sin tildes, sin espacios, guiones ni guiones bajos.</summary>
    public static string NormalizeHeader(string header)
    {
        var decomposed = header.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (c is ' ' or '_' or '-' or '.') continue;
            builder.Append(c);
        }

        return builder.ToString();
    }
}
