using System.Security.Claims;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Web.Auth;
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
    builder.Services.AddCascadingAuthenticationState();

    // ── Autenticación: Cookie + OpenID Connect (Authorization Code Flow) ──────
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme          = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.ExpireTimeSpan    = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.LoginPath         = "/login";
        options.AccessDeniedPath  = "/access-denied";
    })
    .AddOpenIdConnect(options =>
    {
        options.Authority    = builder.Configuration["OpenIdConnect:Authority"];
        options.ClientId     = builder.Configuration["OpenIdConnect:ClientId"];
        options.ClientSecret = builder.Configuration["OpenIdConnect:ClientSecret"];
        options.CallbackPath = builder.Configuration["OpenIdConnect:CallbackPath"] ?? "/signin-oidc";

        options.ResponseType = OpenIdConnectResponseType.Code;
        options.SaveTokens   = true;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.MapInboundClaims = false;

        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");

        // ── Eventos OIDC con Serilog ──────────────────────────────────────────
        options.Events = new OpenIdConnectEvents
        {
            OnTokenValidated = ctx =>
            {
                var logger = ctx.HttpContext.RequestServices
                    .GetRequiredService<ILogger<Program>>();

                var sub = ctx.Principal?.FindFirstValue("sub") ?? "(desconocido)";
                logger.LogInformation(
                    "Token OIDC validado. Subject: {Subject}", sub);

                return Task.CompletedTask;
            },

            OnAuthenticationFailed = ctx =>
            {
                var logger = ctx.HttpContext.RequestServices
                    .GetRequiredService<ILogger<Program>>();

                logger.LogError(
                    ctx.Exception,
                    "Error de autenticación OIDC: {ErrorMessage}", ctx.Exception.Message);

                ctx.HandleResponse();
                ctx.Response.Redirect("/access-denied");
                return Task.CompletedTask;
            }
        };
    });

    builder.Services.AddAuthorization();

    // ── Transformación de claims ──────────────────────────────────────────────
    builder.Services.AddScoped<IClaimsTransformation, ClaimsTransformation>();

    // ── Contexto de usuario (multi-tenant + auditoría) ────────────────────────
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

    // ── Pipeline ──────────────────────────────────────────────────────────────
    var app = builder.Build();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }

    app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
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

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseAntiforgery();

    app.MapStaticAssets();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    // ── Endpoints de autenticación ────────────────────────────────────────────
    app.MapGet("/login", (string? returnUrl) =>
        TypedResults.Challenge(
            new AuthenticationProperties { RedirectUri = returnUrl ?? "/" },
            [OpenIdConnectDefaults.AuthenticationScheme]));

    app.MapPost("/logout", async (HttpContext ctx) =>
    {
        await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await ctx.SignOutAsync(
            OpenIdConnectDefaults.AuthenticationScheme,
            new AuthenticationProperties { RedirectUri = "/" });
    });

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


