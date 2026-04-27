using System.Security.Claims;
using FluentValidation;
using GreenTransit.Application.Common.Behaviours;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.ServiceOrders.Commands;
using GreenTransit.Infrastructure.Persistence;
using GreenTransit.Infrastructure.Persistence.Repositories;
using GreenTransit.Web.Auth;
using MediatR;
using Microsoft.EntityFrameworkCore;
using GreenTransit.Web.Components;
using GreenTransit.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Serilog;
using Serilog.Events;

// ── Bootstrap logger: captura errores antes de que el host arranque ───────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Arrancando GreenTransit Web...");

    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog: reemplaza el sistema de logging de .NET ──────────────────────
    builder.Host.UseSerilog((context, services, config) =>
    {
        config
            .ReadFrom.Configuration(context.Configuration) // Lee sección "Serilog" de appsettings
            .ReadFrom.Services(services)                   // Permite inyección de ILogger desde DI
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId();
    });

    // ── Razor Components / Blazor ─────────────────────────────────────────────
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    // Habilita el estado de autenticación en cascada para Blazor (.NET 8+)
    // builder.Services.AddCascadingAuthenticationState()

    // Servicios mínimos de auth para que [Authorize] no explote mientras la autenticación está deshabilitada.
    // Se permite todo: no hay scheme de challenge configurado.
    builder.Services.AddAuthentication();
    builder.Services.AddAuthorization(options =>
    {
        options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
            .RequireAssertion(_ => true)
            .Build();
    });

    // ── Contexto de usuario (multi-tenant + auditoría) ────────────────────────
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

    // ── EF Core: AppDbContext con SQL Server ──────────────────────────────────
    // AddDbContext resuelve ICurrentUserService del contenedor al construir el contexto.
    builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    {
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            sql => sql.EnableRetryOnFailure(maxRetryCount: 3));
    });
    builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());

    // ── Repositorios y Unit of Work ───────────────────────────────────────────
    builder.Services.AddScoped(typeof(IRepository<>), typeof(GreenTransit.Infrastructure.Persistence.Repositories.EfRepository<>));
    builder.Services.AddScoped<IUnitOfWork, GreenTransit.Infrastructure.Persistence.UnitOfWork>();
    builder.Services.AddScoped<IUserRepository, GreenTransit.Infrastructure.Persistence.Repositories.UserRepository>();

    // ── MediatR: handlers + pipeline behaviors ────────────────────────────────
    // Orden: LoggingBehavior (externo) → ValidationBehavior → handler
    builder.Services.AddMediatR(cfg =>
    {
        cfg.RegisterServicesFromAssembly(typeof(CreateServiceOrderCommand).Assembly);
        cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    });

    // ── FluentValidation: registro automático de todos los validators ─────────
    builder.Services.AddValidatorsFromAssembly(typeof(CreateServiceOrderCommand).Assembly);

    // ── Pipeline ──────────────────────────────────────────────────────────────
    var app = builder.Build();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }
    else
    {
        app.UseDeveloperExceptionPage();
    }

    app.UseHttpsRedirection();

    // Middleware de logging de requests HTTP con Serilog
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} → {StatusCode} en {Elapsed:0.0000} ms";

        // Eleva a Error si hay excepción o status >= 500; de lo contrario Information
        options.GetLevel = (httpContext, _, ex) =>
            ex is not null || httpContext.Response.StatusCode >= 500
                ? LogEventLevel.Error
                : LogEventLevel.Information;

        // Añade propiedades adicionales al contexto del log
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost",   httpContext.Request.Host.Value);
            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
            diagnosticContext.Set("UserAgent",     httpContext.Request.Headers.UserAgent.ToString());
        };
    });

    // Autenticación deshabilitada temporalmente (sin scheme real, [Authorize] permite todo)
    app.UseAuthentication();
    app.UseAuthorization();

    // Blazor Server genera endpoints con metadata antiforgery; el middleware es obligatorio
    app.UseAntiforgery();

    app.MapStaticAssets();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    // Captura fallos fatales durante el arranque (p.ej. BD no disponible, config errónea)
    Log.Fatal(ex, "GreenTransit Web falló durante el arranque de la aplicación.");
}
finally
{
    // Garantiza que todos los logs pendientes se escriben antes de cerrar
    await Log.CloseAndFlushAsync();
}


