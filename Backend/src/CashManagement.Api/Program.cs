using CashManagement.Api.Extensions;
using CashManagement.Api.Middleware;
using CashManagement.Application;
using CashManagement.Infrastructure;
using CashManagement.Infrastructure.Persistence;
using Serilog;

Log.Logger = SerilogSetup.CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) =>
        SerilogSetup.Configure(configuration, context.Configuration, services));

    builder.Services
        .AddApplication()
        .AddInfrastructure(builder.Configuration)
        .AddApiServices(builder.Configuration);

    var app = builder.Build();

    if (app.Configuration.GetValue("Database:InitializeOnStartup", true))
    {
        await DbInitializer.InitializeAsync(app.Services);
    }

    // El registro de peticiones va por fuera del manejo de excepciones para que anote el
    // código real (400, 401, 409...) y no un 500 por cada error de negocio.
    app.UseSerilogRequestLogging(options =>
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} respondió {StatusCode} en {Elapsed:0.0} ms");
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Cash Management API v1");
        options.DocumentTitle = "Cash Management API";
    });

    app.UseCors(ApiServiceExtensions.CorsPolicy);
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTimeOffset.UtcNow })).AllowAnonymous();
    app.MapControllers();

    Log.Information("API iniciada");
    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "La API se detuvo por un error no controlado");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
