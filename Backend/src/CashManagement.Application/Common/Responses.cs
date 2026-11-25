namespace CashManagement.Application.Common;

/// <summary>Envoltorio estándar de todas las respuestas de la API.</summary>
public class ApiResponse<T>
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }
    public IDictionary<string, string[]>? Errors { get; init; }

    public static ApiResponse<T> Ok(T? data, string message = "Operación realizada correctamente.") =>
        new() { Success = true, Message = message, Data = data };

    public static ApiResponse<T> Fail(string message, IDictionary<string, string[]>? errors = null) =>
        new() { Success = false, Message = message, Errors = errors };
}

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
}

public record PageQuery
{
    public string? Search { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;

    public int SafePage => Page < 1 ? 1 : Page;
    public int SafePageSize => PageSize is < 1 or > 100 ? 10 : PageSize;
}

/// <summary>Resultado de una carga masiva desde Excel / CSV.</summary>
public record BulkUploadResult(int Total, int Created, int Failed, IReadOnlyList<BulkRowError> Errors);

public record BulkRowError(int Row, string Identifier, IReadOnlyList<string> Messages);
