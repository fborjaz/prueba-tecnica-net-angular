using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CashManagement.Api.Services;
using CashManagement.Application.Common;
using CashManagement.Application.Interfaces;
using CashManagement.Domain.Constants;
using CashManagement.Infrastructure.Persistence;
using CashManagement.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

namespace CashManagement.Api.Extensions;

public static class ApiServiceExtensions
{
    public const string CorsPolicy = "Frontend";

    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddControllers()
            .AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            })
            .ConfigureApiBehaviorOptions(o =>
            {
                // Errores de binding con el mismo formato ApiResponse que el resto de la API.
                o.InvalidModelStateResponseFactory = context =>
                {
                    var errors = context.ModelState
                        .Where(e => e.Value?.Errors.Count > 0)
                        .ToDictionary(e => e.Key, e => e.Value!.Errors.Select(x => x.ErrorMessage).ToArray());
                    return new BadRequestObjectResult(ApiResponse<object>.Fail("La solicitud no tiene el formato esperado.", errors));
                };
            });

        services.AddCors(o => o.AddPolicy(CorsPolicy, policy =>
        {
            var origins = configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:4200"];
            policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
        }));

        services.AddJwtAuthentication(configuration);
        services.AddSwagger();
        return services;
    }

    private static void AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.Section).Get<JwtOptions>()
                  ?? throw new InvalidOperationException("Falta la sección Jwt en la configuración.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = jwt.GetSigningKey(),
                    // Clave para descifrar el JWE: sin ella las claims no se pueden leer.
                    TokenDecryptionKey = jwt.GetEncryptionKey(),
                    NameClaimType = "unique_name",
                    RoleClaimType = "role"
                };

                options.Events = new JwtBearerEvents
                {
                    // Revalida en BD: un usuario bloqueado, inactivo o con sesión cerrada pierde acceso aunque su token no haya expirado.
                    OnTokenValidated = async context =>
                    {
                        // Solo se aceptan tokens cifrados (JWE). Un JWT solo firmado llevaría las
                        // claims legibles, aunque la firma sea válida.
                        if (context.SecurityToken is not JsonWebToken { InnerToken: not null })
                        {
                            context.Fail("El token debe venir cifrado.");
                            return;
                        }

                        var sub = context.Principal?.FindFirst("sub")?.Value;
                        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                        var valid = int.TryParse(sub, out var userId) && await db.Users.AsNoTracking().AnyAsync(u =>
                            u.Id == userId && !u.IsDeleted && u.SessionActive && u.StatusId == UserStatuses.Active);

                        if (!valid) context.Fail("La sesión no es válida o el usuario ya no está activo.");
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail("Sesión no válida o expirada. Inicie sesión nuevamente."));
                    },
                    OnForbidden = async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail("No tiene permisos para realizar esta operación."));
                    }
                };
            });

        services.AddAuthorization();
    }

    private static void AddSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Cash Management API",
                Version = "v1",
                Description = "API REST para la gestión de caja y turnos de un proveedor de internet. " +
                              "Autentíquese con POST /api/auth/login y pegue el token en Authorize."
            });

            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWE",
                In = ParameterLocation.Header,
                Description = "Token JWT cifrado obtenido en /api/auth/login"
            });

            c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            });

            var xml = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
            if (File.Exists(xml)) c.IncludeXmlComments(xml);
        });
    }
}
