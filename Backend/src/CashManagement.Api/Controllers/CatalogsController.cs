using CashManagement.Application.Common;
using CashManagement.Application.Features.Catalogs;
using CashManagement.Application.Features.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CashManagement.Api.Controllers;

/// <summary>Servicios de internet (planes) con sus equipos.</summary>
[Route("api/services")]
public class ServicesController(IServiceCatalogService serviceCatalog) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ServiceDto>>>> GetAll([FromQuery] bool onlyActive, CancellationToken ct) =>
        Success(await serviceCatalog.GetAllAsync(onlyActive, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<ServiceDto>>> GetById(int id, CancellationToken ct) =>
        Success(await serviceCatalog.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = Admin)]
    public async Task<ActionResult<ApiResponse<ServiceDto>>> Create(SaveServiceRequest request, CancellationToken ct) =>
        Created(await serviceCatalog.CreateAsync(request, ct), "Servicio creado correctamente.");

    [HttpPut("{id:int}")]
    [Authorize(Roles = Admin)]
    public async Task<ActionResult<ApiResponse<ServiceDto>>> Update(int id, SaveServiceRequest request, CancellationToken ct) =>
        Success(await serviceCatalog.UpdateAsync(id, request, ct), "Servicio actualizado correctamente.");

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Admin)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id, CancellationToken ct)
    {
        await serviceCatalog.DeleteAsync(id, ct);
        return Done("Servicio eliminado correctamente.");
    }
}

/// <summary>Catálogos (roles, estados, tipos de atención) y formas de pago.</summary>
[Route("api/catalogs")]
public class CatalogsController(ICatalogService catalogService) : ApiControllerBase
{
    [HttpGet("roles")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CatalogItemDto>>>> Roles(CancellationToken ct) =>
        Success(await catalogService.GetRolesAsync(ct));

    [HttpGet("user-statuses")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CatalogItemDto>>>> UserStatuses(CancellationToken ct) =>
        Success(await catalogService.GetUserStatusesAsync(ct));

    [HttpGet("attention-types")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CatalogItemDto>>>> AttentionTypes(CancellationToken ct) =>
        Success(await catalogService.GetAttentionTypesAsync(ct));

    [HttpGet("attention-statuses")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CatalogItemDto>>>> AttentionStatuses(CancellationToken ct) =>
        Success(await catalogService.GetAttentionStatusesAsync(ct));

    [HttpGet("contract-statuses")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CatalogItemDto>>>> ContractStatuses(CancellationToken ct) =>
        Success(await catalogService.GetContractStatusesAsync(ct));

    [HttpGet("method-payments")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MethodPaymentDto>>>> MethodPayments([FromQuery] bool onlyActive, CancellationToken ct) =>
        Success(await catalogService.GetMethodPaymentsAsync(onlyActive, ct));

    [HttpPost("method-payments")]
    [Authorize(Roles = Admin)]
    public async Task<ActionResult<ApiResponse<MethodPaymentDto>>> CreateMethodPayment(SaveMethodPaymentRequest request, CancellationToken ct) =>
        Created(await catalogService.CreateMethodPaymentAsync(request, ct), "Forma de pago creada correctamente.");

    [HttpPut("method-payments/{id:int}")]
    [Authorize(Roles = Admin)]
    public async Task<ActionResult<ApiResponse<MethodPaymentDto>>> UpdateMethodPayment(int id, SaveMethodPaymentRequest request, CancellationToken ct) =>
        Success(await catalogService.UpdateMethodPaymentAsync(id, request, ct), "Forma de pago actualizada correctamente.");

    [HttpDelete("method-payments/{id:int}")]
    [Authorize(Roles = Admin)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteMethodPayment(int id, CancellationToken ct)
    {
        await catalogService.DeleteMethodPaymentAsync(id, ct);
        return Done("Forma de pago eliminada correctamente.");
    }
}

/// <summary>Menú de navegación cargado desde la base de datos según el rol del usuario.</summary>
[Route("api/menu")]
public class MenuController(ICatalogService catalogService) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MenuItemDto>>>> Get(CancellationToken ct) =>
        Success(await catalogService.GetMenuAsync(ct));
}

/// <summary>Pantalla de bienvenida e indicadores del dashboard.</summary>
[Route("api/dashboard")]
public class DashboardController(IDashboardService dashboardService) : ApiControllerBase
{
    /// <summary>Datos del usuario, turnos atendidos hoy (según rol) y pendientes de aprobación.</summary>
    [HttpGet("welcome")]
    public async Task<ActionResult<ApiResponse<WelcomeDto>>> Welcome(CancellationToken ct) =>
        Success(await dashboardService.GetWelcomeAsync(ct));

    /// <summary>Indicadores de usuarios, cajas, cajeros y gestores (sp_dashboard_indicators).</summary>
    [HttpGet("indicators")]
    [Authorize(Roles = Admin)]
    public async Task<ActionResult<ApiResponse<DashboardIndicatorsDto>>> Indicators([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        Success(await dashboardService.GetIndicatorsAsync(from, to, ct));
}
