using CashManagement.Application.Common;
using Serilog;
using Serilog.Events;
using Serilog.Filters;

namespace CashManagement.Api.Extensions;

/// <summary>
/// Configuración de Serilog:
/// <list type="bullet">
/// <item><c>logs/app-.log</c>: log general (peticiones HTTP, alertas y errores).</item>
/// <item><c>logs/audit-.log</c>: creación, actualización y eliminación de registros.</item>
/// <item><c>logs/security-.log</c>: inicios/cierres de sesión, intentos fallidos, bloqueos.</item>
/// <item><c>logs/errors-.log</c>: solo errores (Error y Fatal).</item>
/// </list>
/// </summary>
public static class SerilogSetup
{
    private const string Template =
        "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    public static Serilog.ILogger CreateBootstrapLogger() => new LoggerConfiguration()
        .WriteTo.Console()
        .CreateBootstrapLogger();

    public static void Configure(LoggerConfiguration logger, IConfiguration configuration, IServiceProvider services)
    {
        var path = configuration.GetValue("Logging:FilePath", "logs")!;

        logger
            .ReadFrom.Configuration(configuration)
            .ReadFrom.Services(services)
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "CashManagement.Api")
            .WriteTo.Console(outputTemplate: Template)
            .WriteTo.File(Path.Combine(path, "app-.log"), rollingInterval: RollingInterval.Day,
                outputTemplate: Template, retainedFileCountLimit: 30)
            .WriteTo.Logger(l => l
                .Filter.ByIncludingOnly(Matching.FromSource(LogCategories.Audit))
                .WriteTo.File(Path.Combine(path, "audit-.log"), rollingInterval: RollingInterval.Day,
                    outputTemplate: Template, retainedFileCountLimit: 90))
            .WriteTo.Logger(l => l
                .Filter.ByIncludingOnly(Matching.FromSource(LogCategories.Security))
                .WriteTo.File(Path.Combine(path, "security-.log"), rollingInterval: RollingInterval.Day,
                    outputTemplate: Template, retainedFileCountLimit: 90))
            .WriteTo.Logger(l => l
                .Filter.ByIncludingOnly(e => e.Level >= LogEventLevel.Error)
                .WriteTo.File(Path.Combine(path, "errors-.log"), rollingInterval: RollingInterval.Day,
                    outputTemplate: Template, retainedFileCountLimit: 90));
    }
}
