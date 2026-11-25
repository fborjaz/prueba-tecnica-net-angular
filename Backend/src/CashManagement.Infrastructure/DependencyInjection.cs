using CashManagement.Application.Features.Auth;
using CashManagement.Application.Interfaces;
using CashManagement.Infrastructure.Persistence;
using CashManagement.Infrastructure.Security;
using CashManagement.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CashManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.Section));
        services.Configure<EncryptionOptions>(configuration.GetSection(EncryptionOptions.Section));
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.Section));
        services.Configure<AppOptions>(configuration.GetSection(AppOptions.Section));

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sql => sql.EnableRetryOnFailure(3));
            options.ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IStoredProcedureService, StoredProcedureService>();

        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<IFieldEncryptor, AesFieldEncryptor>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<ISpreadsheetReader, SpreadsheetReader>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        return services;
    }
}
