using CashManagement.Application.Features.Auth;
using CashManagement.Application.Features.Cashes;
using CashManagement.Application.Features.Catalogs;
using CashManagement.Application.Features.Clients;
using CashManagement.Application.Features.Contracts;
using CashManagement.Application.Features.Dashboard;
using CashManagement.Application.Features.Payments;
using CashManagement.Application.Features.Turns;
using CashManagement.Application.Features.Users;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CashManagement.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<ICashService, CashService>();
        services.AddScoped<ITurnService, TurnService>();
        services.AddScoped<IContractService, ContractService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IServiceCatalogService, ServiceCatalogService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<IDashboardService, DashboardService>();

        return services;
    }
}
