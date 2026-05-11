using System.Reflection;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Entities;
using GreenTransit.Infrastructure.Persistence;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GreenTransit.Infrastructure.Services;

/// <summary>
/// Escanea por reflexión los componentes Blazor con @page y sincroniza la tabla PageDefinitions.
/// Solo inserta entradas nuevas; nunca elimina las existentes.
/// </summary>
public sealed class PageDiscoveryService : IPageDiscoveryService
{
    private readonly AppDbContext                   _db;
    private readonly ILogger<PageDiscoveryService>  _logger;
    private readonly Assembly                       _webAssembly;

    public PageDiscoveryService(
        AppDbContext db,
        ILogger<PageDiscoveryService> logger,
        Assembly webAssembly)
    {
        _db          = db;
        _logger      = logger;
        _webAssembly = webAssembly;
    }

    public async Task<int> SyncPageDefinitionsAsync(CancellationToken ct = default)
    {
        // 1. Obtener todos los componentes Blazor con [Route]
        var pageTypes = _webAssembly.GetTypes()
            .Where(t => t.IsClass
                     && !t.IsAbstract
                     && typeof(ComponentBase).IsAssignableFrom(t)
                     && t.GetCustomAttributes(typeof(RouteAttribute), inherit: true).Length > 0)
            .ToList();

        // 2. Extraer rutas
        var discovered = new List<(string Route, string ComponentName, string ModuleName)>();
        foreach (var type in pageTypes)
        {
            var routeAttrs = type.GetCustomAttributes(typeof(RouteAttribute), inherit: true)
                                 .Cast<RouteAttribute>();
            foreach (var attr in routeAttrs)
            {
                var moduleName = InferModuleName(type.Namespace, attr.Template);

                // Excluir páginas de sistema/infraestructura: no deben estar sujetas
                // al control dinámico de permisos. Son accesibles a cualquier usuario
                // autenticado gobernadas únicamente por [Authorize].
                if (moduleName == "General")
                {
                    _logger.LogDebug(
                        "Ruta de sistema excluida de PageDefinitions: {Route} ({Component})",
                        attr.Template, type.Name);
                    continue;
                }

                discovered.Add((attr.Template, type.Name, moduleName));
            }
        }

        // 3. Desactivar rutas de sistema que puedan existir ya en BD (registradas antes de esta regla)
        var systemPages = await _db.PageDefinitions
            .Where(d => d.IsActive && d.ModuleName == "General")
            .ToListAsync(ct);

        if (systemPages.Count > 0)
        {
            foreach (var page in systemPages)
            {
                page.IsActive   = false;
                page.UpdatedAt  = DateTime.UtcNow;
                _logger.LogInformation(
                    "Página de sistema desactivada en PageDefinitions: {Route}", page.Route);
            }
            await _db.SaveChangesAsync(ct);
        }

        // 4. Rutas ya existentes en BD
        var existingRoutes = await _db.PageDefinitions
            .Select(p => p.Route)
            .ToHashSetAsync(StringComparer.OrdinalIgnoreCase, ct);

        // 5. Insertar las nuevas
        var newPages = discovered
            .Where(dp => !existingRoutes.Contains(dp.Route))
            .ToList();

        foreach (var page in newPages)
        {
            _db.PageDefinitions.Add(new PageDefinition
            {
                Route         = page.Route,
                PageName      = HumanizeName(page.ComponentName),
                ModuleName    = page.ModuleName,
                ComponentName = page.ComponentName,
                IsActive      = true,
                SortOrder     = 0,
                CreatedAt     = DateTime.UtcNow
            });
            _logger.LogInformation(
                "Nueva pantalla descubierta: {Route} ({Component})",
                page.Route, page.ComponentName);
        }

        if (newPages.Count > 0)
            await _db.SaveChangesAsync(ct);

        // 5b. Reparar páginas que fueron registradas como "General" (excluidas) pero
        //     que ahora tienen un módulo válido tras actualizar InferModuleName.
        //     Las reactiva y les asigna el módulo correcto.
        var discoveredByRoute = discovered.ToDictionary(
            dp => dp.Route, dp => dp, StringComparer.OrdinalIgnoreCase);

        var wrongModulePages = await _db.PageDefinitions
            .Where(d => d.ModuleName == "General")
            .ToListAsync(ct);

        int repaired = 0;
        foreach (var page in wrongModulePages)
        {
            if (!discoveredByRoute.TryGetValue(page.Route, out var disc)) continue;

            page.ModuleName    = disc.ModuleName;
            page.ComponentName = disc.ComponentName;
            page.IsActive      = true;
            page.UpdatedAt     = DateTime.UtcNow;
            repaired++;
            _logger.LogInformation(
                "Página reparada: {Route} → módulo '{Module}'", page.Route, disc.ModuleName);
        }

        if (repaired > 0)
            await _db.SaveChangesAsync(ct);

        // 6. Auto-asignar ReadWrite al perfil ADMIN en todas las páginas que no lo tengan
        await EnsureAdminHasFullAccessAsync(ct);

        _logger.LogInformation(
            "Sincronización de pantallas completada: {New} nuevas, {Total} total descubiertas.",
            newPages.Count, discovered.Count);

        return newPages.Count;
    }

    private static string InferModuleName(string? ns, string route)
    {
        if (ns?.Contains("Security") == true) return "Seguridad";
        if (ns?.Contains("Reporting") == true) return "Reporting";
        if (ns?.Contains("Logistics") == true) return "Dashboards Logísticos";
        if (ns?.Contains("Mobility") == true)  return "Movilidad Urbana";
        if (ns?.Contains("Sustainability") == true) return "Sostenibilidad";

        return route switch
        {
            var r when r.StartsWith("/entities")           ||
                       r.StartsWith("/ler-codes")          ||
                       r.StartsWith("/residues")           ||
                       r.StartsWith("/treatment-operations") => "Configuración",
            var r when r.StartsWith("/service-orders")     ||
                       r.StartsWith("/waste-moves")        ||
                       r.StartsWith("/entry-")             ||
                       r.StartsWith("/treatment-plants")   => "Operaciones",
            var r when r.StartsWith("/agreements")         ||
                       r.StartsWith("/settlements")        ||
                       r.StartsWith("/market-shares")      => "Contratos y Liquidaciones",
            var r when r.StartsWith("/incidents")          ||
                       r.StartsWith("/dum-zones")          ||
                       r.StartsWith("/emissions")          ||
                       r.StartsWith("/plant-energies")     ||
                       r.StartsWith("/emission-factor")    => "Sostenibilidad",
            var r when r.StartsWith("/users")              ||
                       r.StartsWith("/profiles")           ||
                       r.StartsWith("/security")           ||
                       r.StartsWith("/admin")              => "Seguridad",
            var r when r.StartsWith("/product-declarations") => "Declaraciones de Producto",
            var r when r.StartsWith("/logistics")          ||
                       r.StartsWith("/kpis")               ||
                       r.StartsWith("/traceability")       ||
                       r.StartsWith("/documents")          => "Reporting",
            var r when r.StartsWith("/mobility")           => "Movilidad Urbana",
            _ => "General"
        };
    }

    private static string HumanizeName(string componentName)
    {
        // Mapa estático de nombres conocidos
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ServiceOrderList"]      = "Órdenes de Servicio",
            ["ServiceOrderForm"]      = "Nueva/Editar Orden de Servicio",
            ["ServiceOrderDetail"]    = "Detalle Orden de Servicio",
            ["WasteMoveList"]         = "Traslados",
            ["WasteMoveForm"]         = "Nuevo/Editar Traslado",
            ["WasteMoveDetail"]       = "Detalle Traslado",
            ["UserList"]              = "Usuarios",
            ["ProfileList"]           = "Perfiles",
            ["PagePermissionMatrix"]  = "Permisos por Pantalla",
            ["SandboxManager"]        = "Sandbox — Generación de Datos",
            ["LerCodeList"]           = "Códigos LER",
            ["ResidueList"]           = "Residuos",
            ["EntityList"]            = "Entidades",
            ["EntryPlantList"]        = "Entradas Planta",
            ["EntryPlantForm"]        = "Nueva/Editar Entrada Planta",
            ["EntryCACList"]          = "Entradas CAC",
            ["TreatmentPlantList"]    = "Tratamiento Planta",
            ["TreatmentOperationList"]= "Operaciones R/D",
            ["AgreementList"]         = "Acuerdos",
            ["SettlementList"]        = "Liquidaciones",
            ["MarketShareList"]       = "Cuotas de Mercado",
            ["IncidentList"]          = "Incidencias",
            ["DumZoneList"]           = "Zonas DUM",
            ["EmissionList"]          = "Emisiones",
            ["PlantEnergyList"]       = "Energía Planta",
            ["EmissionFactorSetList"] = "Factores de Emisión",
            ["ProductDeclarationList"]= "Declaraciones de Producto",
            ["LogisticsOptimization"] = "Optimización Logística SCRAP",
            ["PublicMonitoring"]      = "Monitorización Pública",
            ["OperationalDashboard"]  = "Panel Operativo",
            ["Dashboard"]             = "Dashboard",
            ["Home"]                  = "Inicio"
        };

        if (map.TryGetValue(componentName, out var name)) return name;

        // Intento de humanizar automáticamente: insertar espacios antes de mayúsculas
        return System.Text.RegularExpressions.Regex.Replace(
            componentName, "(?<!^)([A-Z])", " $1");
    }

    /// <summary>
    /// Garantiza que el perfil ADMIN tiene ReadWrite en TODAS las páginas activas.
    /// Se ejecuta en cada sincronización para cubrir páginas nuevas.
    /// </summary>
    private async Task EnsureAdminHasFullAccessAsync(CancellationToken ct)
    {
        var adminProfile = await _db.Set<GreenTransit.Domain.Entities.UserProfile>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Reference == "ADMIN", ct);

        if (adminProfile is null)
        {
            _logger.LogWarning("EnsureAdminHasFullAccess: perfil ADMIN no encontrado.");
            return;
        }

        var allPageIds = await _db.PageDefinitions
            .Where(d => d.IsActive)
            .Select(d => d.ID)
            .ToListAsync(ct);

        var existingPermissionPageIds = await _db.PagePermissions
            .Where(p => p.IdProfile == adminProfile.Id)
            .Select(p => p.IdPageDefinition)
            .ToHashSetAsync(ct);

        var missing = allPageIds.Where(id => !existingPermissionPageIds.Contains(id)).ToList();
        if (missing.Count == 0) return;

        foreach (var pageId in missing)
        {
            _db.PagePermissions.Add(new GreenTransit.Domain.Entities.PagePermission
            {
                IdPageDefinition = pageId,
                IdProfile        = adminProfile.Id,
                AccessLevel      = "ReadWrite",
                CreatedAt        = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "EnsureAdminHasFullAccess: permisos ReadWrite añadidos al ADMIN en {Count} páginas.",
            missing.Count);
    }
}
