using System.Security.Claims;
using FluentValidation;
using GreenTransit.Application.Common;
using GreenTransit.Application.Common.Behaviours;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Infrastructure.Persistence.Seeding;
using GreenTransit.Domain.Entities;
using GreenTransit.Application.Features.ServiceOrders.Commands;
using GreenTransit.Domain.Authorization;
using GreenTransit.Infrastructure.Authorization;
using GreenTransit.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
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

    // Controllers para los endpoints de login/logout (Blazor no puede hacer
    // redirecciones HTTP externas directamente hacia el IdP OIDC)
    builder.Services.AddControllers();

    // RazorPages: necesario para que el pipeline de ASP.NET Core procese
    // correctamente el callback /signin-oidc del middleware OpenIdConnect
    builder.Services.AddRazorPages();

    // ── Autenticación: OpenID Connect + Cookies ───────────────────────────
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme          = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        options.DefaultSignOutScheme   = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.Cookie.Name         = ".GreenTransit.Auth";
        options.Cookie.HttpOnly     = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite     = SameSiteMode.Lax; // Lax es crítico: Strict impide recibir la cookie en el callback OIDC
        options.LoginPath           = "/account/login";
        options.LogoutPath          = "/account/logout";
        options.AccessDeniedPath    = "/acceso-denegado";
        options.ExpireTimeSpan      = TimeSpan.FromHours(8);
        options.SlidingExpiration   = true;
        // Devolver 401 en lugar de redirect para conexiones SignalR/AJAX,
        // evitando que Blazor lance un Challenge en cada negociación del WebSocket.
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/_blazor") ||
                context.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            var diagLogger = context.HttpContext.RequestServices
                .GetRequiredService<ILogger<Program>>();

            // ── DIAGNÓSTICO: volcar TODOS los claims presentes en la cookie ──
            diagLogger.LogWarning("🚫 OnRedirectToAccessDenied → Path={Path}", context.Request.Path);
            diagLogger.LogWarning("   IsAuthenticated={Auth} | AuthType={Type}",
                context.HttpContext.User.Identity?.IsAuthenticated,
                context.HttpContext.User.Identity?.AuthenticationType);
            foreach (var c in context.HttpContext.User.Claims)
                diagLogger.LogWarning("   Cookie claim: {Type} = {Value}", c.Type, c.Value);

            var gtUserFound = context.HttpContext.User.FindFirstValue(AuthClaims.UserFound);
            diagLogger.LogWarning("   gt_user_found en cookie = '{Val}'", gtUserFound ?? "*** AUSENTE ***");

            var email = context.HttpContext.User.FindFirstValue("email")
                        ?? context.HttpContext.User.FindFirstValue(ClaimTypes.Email)
                        ?? context.HttpContext.User.FindFirstValue("preferred_username")
                        ?? context.HttpContext.User.FindFirstValue("sub")
                        ?? "desconocido";

            var detail = $"No existe usuario con login '{email}' en la tabla Users";
            context.Response.Redirect(
                $"/acceso-denegado?error=usuario_no_encontrado&detail={Uri.EscapeDataString(detail)}");
            return Task.CompletedTask;
        };
    })
    .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
    {
        var oidcSection = builder.Configuration.GetSection("OpenIdConnect");
        options.Authority                     = oidcSection["Authority"];
        options.ClientId                      = oidcSection["ClientId"];
        options.ClientSecret                  = oidcSection["ClientSecret"];
        options.ResponseType                  = OpenIdConnectResponseType.Code;
        options.CallbackPath                  = oidcSection["CallbackPath"]          ?? "/signin-oidc";
        options.SignedOutCallbackPath          = oidcSection["SignedOutCallbackPath"] ?? "/signout-callback-oidc";
        options.SaveTokens                    = true;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.MapInboundClaims              = false;
        options.UsePkce                       = true;
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.TokenValidationParameters.NameClaimType  = "name";
        options.TokenValidationParameters.RoleClaimType  = "role";
        options.TokenValidationParameters.ValidateIssuer = true;
        // ── Eventos de diagnóstico ──────────────────────────────────────────
        options.Events = new OpenIdConnectEvents
        {
            OnRedirectToIdentityProvider = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILogger<Program>>();
                logger.LogInformation("🔄 Redirigiendo al servidor OIDC: {Url}",
                    context.ProtocolMessage.BuildRedirectUrl());
                return Task.CompletedTask;
            },
            OnAuthorizationCodeReceived = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILogger<Program>>();
                logger.LogInformation("✅ Authorization Code recibido correctamente");
                return Task.CompletedTask;
            },
            OnTokenResponseReceived = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILogger<Program>>();
                logger.LogInformation("✅ Token recibido correctamente");
                return Task.CompletedTask;
            },
            // ── OnTokenValidated: se ejecuta UNA VEZ al hacer login, antes de
            // que el middleware de cookies escriba la cookie. Los claims añadidos
            // aquí quedan PERSISTIDOS en la cookie y están disponibles en todos
            // los requests posteriores (incluidos los circuitos SignalR de Blazor).
            OnTokenValidated = async context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILogger<Program>>();
                var userRepo = context.HttpContext.RequestServices
                    .GetRequiredService<IUserRepository>();

                var sub   = context.Principal?.FindFirstValue(AuthClaims.Sub);
                var email = context.Principal?.FindFirstValue("email")
                            ?? context.Principal?.FindFirstValue(ClaimTypes.Email);
                var preferredUsername = context.Principal?.FindFirstValue("preferred_username");

                logger.LogInformation("OnTokenValidated: sub='{Sub}' email='{Email}' preferred_username='{PU}'",
                    sub ?? "∅", email ?? "∅", preferredUsername ?? "∅");

                // Intentar los mismos tres pasos que ClaimsTransformation
                var login = sub ?? email ?? preferredUsername;
                AppUser? user = null;

                if (!string.IsNullOrEmpty(login))
                    user = await userRepo.FindByLoginAsync(login);

                if (user is null && !string.IsNullOrEmpty(email) && email != login)
                    user = await userRepo.FindByEmailAsync(email);

                if (user is null && !string.IsNullOrEmpty(email))
                    user = await userRepo.FindByLoginAsync(email);

                if (user is null)
                {
                    logger.LogWarning("❌ OnTokenValidated: usuario no encontrado. sub='{Sub}' email='{Email}'", sub, email);
                    context.Fail("Usuario no registrado en GreenTransit");
                    return;
                }

                logger.LogInformation("✅ OnTokenValidated: usuario encontrado ID={Id} Login={Login} Perfil={Profile}",
                    user.Id, user.Login, user.Profile?.Reference ?? "SIN PERFIL");

                var identity = context.Principal!.Identity as ClaimsIdentity
                               ?? context.Principal!.Identities.First();

                identity.AddClaim(new Claim(AuthClaims.UserFound,  "true"));
                identity.AddClaim(new Claim(AuthClaims.IdUser,     user.Id.ToString(), ClaimValueTypes.Integer));
                identity.AddClaim(new Claim(AuthClaims.Login,      user.Login));
                identity.AddClaim(new Claim(AuthClaims.UserName,   user.CompleteName ?? user.Email ?? user.Login));
                identity.AddClaim(new Claim(ClaimTypes.Name,       user.CompleteName ?? user.Email ?? user.Login));

                if (!string.IsNullOrEmpty(user.Email))
                    identity.AddClaim(new Claim(AuthClaims.Email, user.Email));

                identity.AddClaim(new Claim(AuthClaims.ProfileId, user.IdProfile.ToString(), ClaimValueTypes.Integer));

                if (!string.IsNullOrEmpty(user.Profile?.Reference))
                {
                    identity.AddClaim(new Claim(AuthClaims.Profile, user.Profile.Reference));
                    identity.AddClaim(new Claim(ClaimTypes.Role,    user.Profile.Reference));
                }

                if (user.OwnerId.HasValue)
                    identity.AddClaim(new Claim(AuthClaims.OwnerId, user.OwnerId.Value.ToString()));

                if (!string.IsNullOrEmpty(user.Email))
                {
                    var entityId = await userRepo.FindEntityIdByEmailAsync(user.Email);
                    if (entityId.HasValue)
                        identity.AddClaim(new Claim(AuthClaims.EntityId, entityId.Value.ToString()));
                }
            },
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILogger<Program>>();
                logger.LogError(context.Exception, "❌ Error de autenticación OIDC");
                context.HandleResponse();
                context.Response.Redirect("/acceso-denegado?error=" +
                    Uri.EscapeDataString(context.Exception.Message));
                return Task.CompletedTask;
            },
            OnRemoteFailure = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILogger<Program>>();
                logger.LogError(context.Failure, "❌ Fallo remoto OIDC");
                context.HandleResponse();
                context.Response.Redirect("/acceso-denegado?error=" +
                    Uri.EscapeDataString(context.Failure?.Message ?? "Error desconocido"));
                return Task.CompletedTask;
            }
        };
    });

    // ── Handlers de autorización basados en perfil ───────────────────────────
    // Un único handler por tipo evalúa todas las policies que usen ese requisito.
    builder.Services.AddScoped<IAuthorizationHandler, ProfileAuthorizationHandler>();
    builder.Services.AddScoped<IAuthorizationHandler, OwnDataAuthorizationHandler>();

    builder.Services.AddAuthorization(options =>
    {
        // Política por defecto: autenticado OIDC Y usuario encontrado en la BD.
        // Usuarios autenticados sin registro en Users → redirigen a /acceso-denegado.
        options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireClaim(GreenTransit.Application.Common.AuthClaims.UserFound, "true")
            .Build();

        // ── MAESTROS ─────────────────────────────────────────────────────────

        // CRUD de Entidades: DISPATCH_OFFICE y ADMIN con acceso completo.
        options.AddPolicy(PolicyConstants.CanManageEntities, policy =>
            policy.AddRequirements(new ProfileRequirement(
                ProfileConstants.DispatchOffice, ProfileConstants.Admin)));

        // C+R de Entidades restringido al ámbito propio: SCRAP.
        options.AddPolicy(PolicyConstants.CanCreateEntitiesRestricted, policy =>
            policy.AddRequirements(new OwnDataRequirement(
                requiresEntityLink: false,
                ProfileConstants.Scrap)));

        // CRUD del catálogo LER (normativo, muy esporádico): solo ADMIN.
        options.AddPolicy(PolicyConstants.CanManageLER, policy =>
            policy.AddRequirements(new ProfileRequirement(
                ProfileConstants.Admin)));

        // CRUD de Residuos (tipo Waste y operativo): DISPATCH_OFFICE y ADMIN.
        options.AddPolicy(PolicyConstants.CanManageResidues, policy =>
            policy.AddRequirements(new ProfileRequirement(
                ProfileConstants.DispatchOffice, ProfileConstants.Admin)));

        // CRUD-P de Residuos propios (Product / ProductSpec): solo PRODUCER.
        options.AddPolicy(PolicyConstants.CanManageOwnResidues, policy =>
            policy.AddRequirements(new OwnDataRequirement(
                requiresEntityLink: true,
                ProfileConstants.Producer)));

        // CRUD del catálogo de Operaciones de Tratamiento R/D: solo ADMIN.
        options.AddPolicy(PolicyConstants.CanManageTreatmentOps, policy =>
            policy.AddRequirements(new ProfileRequirement(
                ProfileConstants.Admin)));

        // ── OPERACIONES ───────────────────────────────────────────────────────

        // CRUD de Órdenes de Servicio: DISPATCH_OFFICE y ADMIN (acceso completo).
        options.AddPolicy(PolicyConstants.CanManageServiceOrders, policy =>
            policy.AddRequirements(new ProfileRequirement(
                ProfileConstants.DispatchOffice, ProfileConstants.Admin)));

        // CRUD-P de Órdenes de Servicio propias: PRODUCER y PUBLIC_ENT.
        // El query handler filtra por IdIssuedBy = LinkedEntityId.
        options.AddPolicy(PolicyConstants.CanCreateOwnServiceOrders, policy =>
            policy.AddRequirements(new OwnDataRequirement(
                requiresEntityLink: true,
                ProfileConstants.Producer, ProfileConstants.PublicEnt)));

        // CRUD de Traslados: DISPATCH_OFFICE (creador principal) y ADMIN.
        options.AddPolicy(PolicyConstants.CanManageWasteMoves, policy =>
            policy.AddRequirements(new ProfileRequirement(
                ProfileConstants.DispatchOffice, ProfileConstants.Admin)));

        // U-P de Traslados asignados: solo CARRIER con entidad vinculada.
        // El handler valida que LinkedEntityId no sea null; el query filtra por IdCarrier.
        options.AddPolicy(PolicyConstants.CanUpdateAssignedMoves, policy =>
            policy.AddRequirements(new OwnDataRequirement(
                requiresEntityLink: true,
                ProfileConstants.Carrier)));

        // CRUD de Entradas en Planta: PLANT_OP (propias) y ADMIN (todas).
        // El query handler decide si filtrar por entidad vinculada según el perfil.
        options.AddPolicy(PolicyConstants.CanManageEntryPlants, policy =>
            policy.AddRequirements(new ProfileRequirement(
                ProfileConstants.PlantOp, ProfileConstants.Admin)));

        // CRUD de Entradas en CAC: CAC_OP (propias) y ADMIN (todas).
        options.AddPolicy(PolicyConstants.CanManageEntryCACs, policy =>
            policy.AddRequirements(new ProfileRequirement(
                ProfileConstants.CacOp, ProfileConstants.Admin)));

        // CRUD de Tratamientos en Planta: PLANT_OP (propios) y ADMIN (todos).
        options.AddPolicy(PolicyConstants.CanManageTreatments, policy =>
            policy.AddRequirements(new ProfileRequirement(
                ProfileConstants.PlantOp, ProfileConstants.Admin)));

        // ── SOSTENIBILIDAD ────────────────────────────────────────────────────

        // Apertura de incidencias: cualquier usuario autenticado en el sistema.
        options.AddPolicy(PolicyConstants.CanCreateIncidents, policy =>
            policy.RequireAuthenticatedUser()
                  .RequireClaim(GreenTransit.Application.Common.AuthClaims.UserFound, "true"));

        // Resolución/cierre de incidencias: DISPATCH_OFFICE y ADMIN.
        options.AddPolicy(PolicyConstants.CanResolveIncidents, policy =>
            policy.AddRequirements(new ProfileRequirement(
                ProfileConstants.DispatchOffice, ProfileConstants.Admin)));

        // CRUD de Zonas DUM y reglas de restricción: solo ADMIN.
        options.AddPolicy(PolicyConstants.CanManageDUMZones, policy =>
            policy.AddRequirements(new ProfileRequirement(
                ProfileConstants.Admin)));

        // CRUD de consumo energético de planta: PLANT_OP (propia) y ADMIN.
        options.AddPolicy(PolicyConstants.CanManagePlantEnergy, policy =>
            policy.AddRequirements(new ProfileRequirement(
                ProfileConstants.PlantOp, ProfileConstants.Admin)));

        // CRUD de conjuntos de factores de emisión (catálogo versionado): solo ADMIN.
        options.AddPolicy(PolicyConstants.CanManageEmissionFactors, policy =>
            policy.AddRequirements(new ProfileRequirement(
                ProfileConstants.Admin)));

        // ── CONTRATACIÓN Y ECONOMÍA ───────────────────────────────────────────

        // CRUD de Acuerdos: SCRAP (propios) y ADMIN.
        options.AddPolicy(PolicyConstants.CanManageAgreements, policy =>
            policy.AddRequirements(new ProfileRequirement(
                ProfileConstants.Scrap, ProfileConstants.Admin)));

        // CRUD de Liquidaciones: SCRAP (validador) y ADMIN.
        options.AddPolicy(PolicyConstants.CanManageSettlements, policy =>
            policy.AddRequirements(new ProfileRequirement(
                ProfileConstants.Scrap, ProfileConstants.Admin)));

        // ── REPORTING ─────────────────────────────────────────────────────────

        // Lectura de KPIs regulatorios: perfiles con responsabilidad de supervisión.
        options.AddPolicy(PolicyConstants.CanViewKPIs, policy =>
            policy.AddRequirements(new ProfileRequirement(
                ProfileConstants.Scrap,
                ProfileConstants.PublicEnt,
                ProfileConstants.PlantOp,
                ProfileConstants.Coordinator,
                ProfileConstants.DispatchOffice,
                ProfileConstants.Admin)));

        // Acceso al módulo de reporting y trazabilidad: todos los autenticados.
        // El query handler aplica el filtrado por datos propios según el perfil.
        options.AddPolicy(PolicyConstants.CanViewReporting, policy =>
            policy.RequireAuthenticatedUser()
                  .RequireClaim(GreenTransit.Application.Common.AuthClaims.UserFound, "true"));

        // ── SEGURIDAD ─────────────────────────────────────────────────────────

        // CRUD de Usuarios del tenant: solo ADMIN.
        options.AddPolicy(PolicyConstants.CanManageUsers, policy =>
            policy.AddRequirements(new ProfileRequirement(
                ProfileConstants.Admin)));

        // CRUD de Perfiles: solo ADMIN.
        options.AddPolicy(PolicyConstants.CanManageProfiles, policy =>
            policy.AddRequirements(new ProfileRequirement(
                ProfileConstants.Admin)));

        // Lectura restringida de usuarios del propio ámbito: SCRAP.
        options.AddPolicy(PolicyConstants.CanViewOwnUsers, policy =>
            policy.AddRequirements(new OwnDataRequirement(
                requiresEntityLink: false,
                ProfileConstants.Scrap)));

        // ── DECLARACIONES DE PRODUCCIÓN ───────────────────────────────────────

        // Ver declaraciones: ADMIN, PRODUCER, SCRAP, COORDINATOR.
        options.AddPolicy(PolicyConstants.CanViewProductDeclarations, policy =>
            policy.AddRequirements(new ProfileRequirement(
                ProfileConstants.Admin, ProfileConstants.Producer,
                ProfileConstants.Scrap, ProfileConstants.Coordinator)));

        // Crear/editar declaraciones: PRODUCER (propias) y ADMIN.
        options.AddPolicy(PolicyConstants.CanManageProductDeclarations, policy =>
            policy.AddRequirements(new ProfileRequirement(
                ProfileConstants.Producer, ProfileConstants.Admin)));

        // Validar o rechazar: solo ADMIN.
        options.AddPolicy(PolicyConstants.CanValidateProductDeclarations, policy =>
            policy.AddRequirements(new ProfileRequirement(
                ProfileConstants.Admin)));

        // Gestión de diccionarios de declaraciones: solo ADMIN.
        options.AddPolicy(PolicyConstants.CanManageDeclarationDicts, policy =>
            policy.AddRequirements(new ProfileRequirement(
                ProfileConstants.Admin)));

        // ── CUOTAS DE MERCADO ─────────────────────────────────────────────────

        // Ver cuotas y cumplimiento: SCRAP y ADMIN.
        options.AddPolicy(PolicyConstants.CanViewMarketShares, policy =>
            policy.AddRequirements(new ProfileRequirement(
                ProfileConstants.Scrap, ProfileConstants.Admin)));

        // CRUD de cuotas de mercado: solo ADMIN.
        options.AddPolicy(PolicyConstants.CanManageMarketShares, policy =>
            policy.AddRequirements(new ProfileRequirement(
                ProfileConstants.Admin)));

        // Operaciones exclusivas de administración del sistema.
        options.AddPolicy(PolicyConstants.AdminOnly, policy =>
            policy.AddRequirements(new ProfileRequirement(
                ProfileConstants.Admin)));
    });

    // ClaimsTransformation: enriquece el principal con IdUser, OwnerId y Profile desde la BD
    builder.Services.AddScoped<IClaimsTransformation, GreenTransit.Web.Auth.ClaimsTransformation>();

    // Estado de autenticación en cascada para Blazor
    builder.Services.AddCascadingAuthenticationState();

    // ── Contexto de usuario (multi-tenant + auditoría) ────────────────────────
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
    // Abstracción de evaluación de policies para el AuthorizationBehavior de MediatR
    builder.Services.AddScoped<IPolicyEvaluator, GreenTransit.Web.Services.PolicyEvaluator>();

    // ── EF Core: AppDbContext con SQL Server ──────────────────────────────────
    // AddDbContext resuelve ICurrentUserService del contenedor al construir el contexto.
    builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    {
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            sql => sql.EnableRetryOnFailure(maxRetryCount: 3));
    });
    builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());

    builder.Services.AddScoped<IDbInitializer, GreenTransit.Infrastructure.Persistence.DbInitializer>();

    // ── Repositorios y Unit of Work ───────────────────────────────────────────
    builder.Services.AddScoped(typeof(IRepository<>), typeof(GreenTransit.Infrastructure.Persistence.Repositories.EfRepository<>));
    builder.Services.AddScoped<IUnitOfWork, GreenTransit.Infrastructure.Persistence.UnitOfWork>();
    builder.Services.AddScoped<IUserRepository, GreenTransit.Infrastructure.Persistence.Repositories.UserRepository>();

    // ── Servicios de dominio ──────────────────────────────────────────────────
    builder.Services.AddScoped<GreenTransit.Domain.Services.ProductDeclarationStateService>();

    // ── Servicios de dominio (Infrastructure) ─────────────────────────────────
    builder.Services.AddScoped<IDumZoneService, GreenTransit.Infrastructure.Services.DumZoneService>();
    builder.Services.AddScoped<GreenTransit.Application.Common.Interfaces.IProductDeclarationNotificationService,
        GreenTransit.Infrastructure.Services.ProductDeclarationNotificationStub>();
    builder.Services.AddScoped<IEntityUserProvisioningService,
        GreenTransit.Infrastructure.Services.EntityUserProvisioningService>();
    builder.Services.AddScoped<IDataScopeService, GreenTransit.Infrastructure.Services.DataScopeService>();
    builder.Services.AddScoped<ISandboxDataSeeder, SandboxDataSeeder>();

    // ── Objetivos regulatorios por defecto ────────────────────────────────────
    builder.Services.AddSingleton<GreenTransit.Application.Common.Interfaces.IRegulatoryTargetDefaults,
        GreenTransit.Web.Services.RegulatoryTargetDefaults>();

    // ── Opciones de configuración ─────────────────────────────────────────────
    builder.Services.Configure<GreenTransit.Application.Features.PlantEnergies.Queries.PlantEnergyOptions>(
        builder.Configuration.GetSection(
            GreenTransit.Application.Features.PlantEnergies.Queries.PlantEnergyOptions.Section));

    // ── Caché en memoria (catálogos geográficos y otros estáticos) ───────────
    builder.Services.AddMemoryCache();

    // ── MediatR: handlers + pipeline behaviors ────────────────────────────────
    // Orden: LoggingBehavior → AuthorizationBehavior → ValidationBehavior → Handler
    // AuthorizationBehavior debe ir antes de ValidationBehavior para no ejecutar
    // validaciones de datos en requests que el usuario no debería ver.
    builder.Services.AddMediatR(cfg =>
    {
        cfg.RegisterServicesFromAssembly(typeof(CreateServiceOrderCommand).Assembly);
        cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
        cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    });

    // ── FluentValidation: registro automático de todos los validators ─────────
    builder.Services.AddValidatorsFromAssembly(typeof(CreateServiceOrderCommand).Assembly);

    // ── Pipeline ──────────────────────────────────────────────────────────────
    var app = builder.Build();

    // ── Seed de datos maestros (idempotente) ──────────────────────────────────
    await using (var scope = app.Services.CreateAsyncScope())
    {
        var initializer = scope.ServiceProvider.GetRequiredService<IDbInitializer>();
        await initializer.InitializeAsync();
    }

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

    // 2. Routing ANTES de Auth
    app.UseRouting();

    // 3. Antiforgery ANTES de Auth (Blazor .NET 8+ lo requiere)
    // Blazor Server genera endpoints con metadata antiforgery; el middleware es obligatorio
    app.UseAntiforgery();

    // 4. Auth DESPUÉS de Routing y Antiforgery
    app.UseAuthentication();
    app.UseAuthorization();

    // 5. Mapear endpoints DESPUÉS de Auth
    app.MapStaticAssets();
    // MapRazorPages es necesario para que el pipeline procese correctamente
    // el callback /signin-oidc del middleware OpenIdConnect
    app.MapRazorPages();
    app.MapControllers();

    // ── Endpoints admin: seed/clean sandbox ───────────────────────────────────
    app.MapPost("/api/admin/seed-sandbox", async (ISandboxDataSeeder seeder, CancellationToken ct) =>
    {
        await seeder.SeedAsync(ct);
        return Results.Ok(new { message = "Sandbox data seeded successfully" });
    }).RequireAuthorization(PolicyConstants.AdminOnly);

    app.MapDelete("/api/admin/seed-sandbox", async (ISandboxDataSeeder seeder, CancellationToken ct) =>
    {
        await seeder.CleanAsync(ct);
        return Results.Ok(new { message = "Sandbox data cleaned successfully" });
    }).RequireAuthorization(PolicyConstants.AdminOnly);
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();
        // IMPORTANTE: sin .RequireAuthorization() — causaría bucle en SignalR

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


