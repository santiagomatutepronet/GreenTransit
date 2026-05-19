# Prompt para GitHub Copilot: Migración del NavBar a Radzen Blazor

> **Instrucción**: Copia este prompt completo en GitHub Copilot Chat adjuntando los archivos `MainLayout.razor`, `NavMenu.razor`, `NavMenuStateService.cs`, `Program.cs`, `App.razor` y `_Imports.razor` de tu proyecto.

---

## Contexto del proyecto

Estoy desarrollando **GreenTransit**, una aplicación Blazor Web App (.NET 10) con:
- **Arquitectura**: Clean Architecture, EF Core, MediatR
- **Autenticación**: OpenID Connect contra proveedor externo
- **Multi-tenant**: filtrado por `OwnerId`
- **Layout actual**: sidebar colapsable custom (HTML/CSS puro) + topbar con buscador global (`GlobalSearchBar.razor`) y selector de OwnerId
- **Menú lateral**: `NavMenu.razor` con grupos colapsables (Configuración, Operaciones, Economía, Declaraciones, Sostenibilidad, Reporting, Seguridad), cada grupo con chevron animado y estado persistente via `NavMenuStateService.cs` (Scoped)
- **Permisos dinámicos**: cada enlace del menú se filtra con `IPagePermissionService.CanAccessRouteAsync()` antes de renderizarlo. Solo aparecen las rutas que el perfil del usuario tiene asignadas en la tabla `PagePermissions`.

---

## Objetivo

Migrar el sistema de navegación completo (MainLayout + NavMenu) a **componentes Radzen Blazor**, manteniendo TODA la lógica de negocio existente (permisos, estado del menú, multi-tenant, buscador global).

---

## Paso 1: Instalar Radzen Blazor

### 1.1. Añadir el paquete NuGet

En el proyecto `Web` (o el que contiene los componentes Blazor):

```bash
dotnet add package Radzen.Blazor
```

### 1.2. Registrar servicios en `Program.cs`

Añadir **después** de los servicios existentes:

```csharp
using Radzen;

// ... servicios existentes ...

builder.Services.AddRadzenComponents();
```

### 1.3. Añadir usings en `_Imports.razor`

Añadir estas dos líneas al final:

```razor
@using Radzen
@using Radzen.Blazor
```

### 1.4. Incluir tema y JS en `App.razor`

Dentro del `<head>`:

```html
<RadzenTheme Theme="material" @rendermode="InteractiveServer" />
```

Después del último `<script>`:

```html
<script src="_content/Radzen.Blazor/Radzen.Blazor.js?v=@(typeof(Radzen.Colors).Assembly.GetName().Version)"></script>
```

> **Nota sobre temas**: Radzen ofrece temas gratuitos: `material`, `standard`, `default`, `dark`, `software`, `humanistic`. Usa `material` como punto de partida. Si ya tienes modo oscuro/claro, puedes cambiar el tema dinámicamente con `ThemeService`.

---

## Paso 2: Migrar `MainLayout.razor`

Reemplaza la estructura HTML actual del layout por componentes Radzen. La estructura objetivo es:

```razor
@inherits LayoutComponentBase
@inject NavigationManager NavigationManager

<RadzenLayout>
    @* ═══════════════ HEADER / TOPBAR ═══════════════ *@
    <RadzenHeader>
        <RadzenStack Orientation="Orientation.Horizontal" 
                     AlignItems="AlignItems.Center" 
                     JustifyContent="JustifyContent.SpaceBetween"
                     class="rz-px-4"
                     Style="height: 100%;">
            
            <RadzenStack Orientation="Orientation.Horizontal" 
                         AlignItems="AlignItems.Center" Gap="0.5rem">
                @* Botón hamburguesa para toggle del sidebar *@
                <RadzenSidebarToggle Click="@(() => _sidebarExpanded = !_sidebarExpanded)" />
                
                @* Logo / Nombre de la app *@
                <RadzenText TextStyle="TextStyle.H6" class="rz-m-0">
                    GreenTransit
                </RadzenText>
            </RadzenStack>

            <RadzenStack Orientation="Orientation.Horizontal" 
                         AlignItems="AlignItems.Center" Gap="1rem">
                @* ══ Buscador global (tu componente existente) ══ *@
                <GlobalSearchBar />
                
                @* ══ Selector de OwnerId (solo para admins multi-tenant) ══ *@
                @* Mantener tu componente existente de selector de tenant *@
                
                @* ══ Notificaciones ══ *@
                @* Tu componente de notificaciones existente *@
                
                @* ══ Perfil de usuario ══ *@
                @* Tu componente existente *@
            </RadzenStack>
        </RadzenStack>
    </RadzenHeader>

    @* ═══════════════ SIDEBAR ═══════════════ *@
    <RadzenSidebar @bind-Expanded="@_sidebarExpanded" 
                   Style="--rz-sidebar-width: 280px;">
        <NavMenu />
    </RadzenSidebar>

    @* ═══════════════ BODY ═══════════════ *@
    <RadzenBody>
        <div class="rz-p-4">
            @Body
        </div>
    </RadzenBody>
</RadzenLayout>

@* ══ Componentes globales de Radzen (necesarios para Dialog, Toast, etc.) ══ *@
<RadzenDialog />
<RadzenNotification />
<RadzenContextMenu />
<RadzenTooltip />

@code {
    private bool _sidebarExpanded = true;
}
```

### Notas de migración del MainLayout:

1. **`RadzenLayout`** es el contenedor raíz que gestiona la disposición header/sidebar/body.
2. **`RadzenHeader`** reemplaza tu topbar HTML custom.
3. **`RadzenSidebar`** reemplaza tu sidebar HTML con clases CSS custom. Tiene responsive integrado: colapsa automáticamente en móvil.
4. **`RadzenSidebarToggle`** genera el botón hamburguesa automáticamente.
5. **`RadzenBody`** contiene el `@Body` del layout.
6. **`@bind-Expanded`** proporciona binding bidireccional del estado del sidebar.
7. **`--rz-sidebar-width`**: variable CSS para el ancho del sidebar (por defecto 250px).
8. Mantén tus componentes custom existentes (`GlobalSearchBar`, selector de tenant, notificaciones) dentro del header.

---

## Paso 3: Migrar `NavMenu.razor`

El `NavMenu.razor` actual usa HTML custom con divs, chevrones CSS y lógica manual de grupos colapsables. Radzen proporciona **`RadzenPanelMenu`** con **`RadzenPanelMenuItem`** que maneja esto nativamente.

### Estructura de referencia de los componentes Radzen:

```
RadzenPanelMenu                    ← Contenedor del menú completo
  ├─ RadzenPanelMenuItem           ← Grupo padre (ej: "Configuración")
  │   ├─ RadzenPanelMenuItem       ← Enlace hijo (ej: "Entidades")
  │   ├─ RadzenPanelMenuItem       ← Enlace hijo (ej: "Códigos LER")
  │   └─ RadzenPanelMenuItem       ← Enlace hijo (ej: "Residuos")
  ├─ RadzenPanelMenuItem           ← Grupo padre (ej: "Operaciones")
  │   ├─ RadzenPanelMenuItem       ← Enlace hijo
  │   └─ ...
  └─ ...
```

### Propiedades clave de `RadzenPanelMenuItem`:

| Propiedad | Tipo | Descripción |
|-----------|------|-------------|
| `Text` | `string` | Texto visible del item |
| `Icon` | `string` | Icono Material Design (ej: `"settings"`, `"local_shipping"`) |
| `Path` | `string` | Ruta de navegación (equivale a `href` en `NavLink`) |
| `Expanded` | `bool` | Si el grupo está expandido (soporta `@bind-Expanded`) |
| `Visible` | `bool` | Controla la visibilidad (AQUÍ integras los permisos) |
| `Match` | `NavLinkMatch` | `NavLinkMatch.All` o `NavLinkMatch.Prefix` |
| `Click` | `EventCallback<MenuItemEventArgs>` | Evento al hacer clic |

### Implementación del NavMenu con permisos:

```razor
@inject IPagePermissionService PagePermissionService
@inject NavMenuStateService NavMenuState
@inject AuthenticationStateProvider AuthStateProvider

<RadzenPanelMenu Multiple="true" Style="height: 100%; overflow-y: auto;">

    @* ══════════ HOME ══════════ *@
    <RadzenPanelMenuItem Text="Inicio" 
                         Icon="dashboard" 
                         Path="/" 
                         Visible="@_permissions.GetValueOrDefault("/")" />

    @* ══════════ CONFIGURACIÓN ══════════ *@
    <RadzenPanelMenuItem Text="Configuración" 
                         Icon="settings"
                         Expanded="@IsGroupExpanded("Configuracion")"
                         Visible="@HasAnyVisibleChild("Configuracion")">
        
        <RadzenPanelMenuItem Text="Entidades" 
                             Icon="business" 
                             Path="/entities"
                             Visible="@_permissions.GetValueOrDefault("/entities")" />
        <RadzenPanelMenuItem Text="Códigos LER" 
                             Icon="list_alt" 
                             Path="/ler-codes"
                             Visible="@_permissions.GetValueOrDefault("/ler-codes")" />
        <RadzenPanelMenuItem Text="Residuos" 
                             Icon="delete_outline" 
                             Path="/residues"
                             Visible="@_permissions.GetValueOrDefault("/residues")" />
        <RadzenPanelMenuItem Text="Operaciones de Tratamiento" 
                             Icon="recycling" 
                             Path="/treatment-operations"
                             Visible="@_permissions.GetValueOrDefault("/treatment-operations")" />
    </RadzenPanelMenuItem>

    @* ══════════ OPERACIONES ══════════ *@
    <RadzenPanelMenuItem Text="Operaciones" 
                         Icon="local_shipping"
                         Expanded="@IsGroupExpanded("Operaciones")"
                         Visible="@HasAnyVisibleChild("Operaciones")">
        
        <RadzenPanelMenuItem Text="Órdenes de Servicio" 
                             Icon="assignment" 
                             Path="/service-orders"
                             Visible="@_permissions.GetValueOrDefault("/service-orders")" />
        <RadzenPanelMenuItem Text="Traslados" 
                             Icon="swap_horiz" 
                             Path="/waste-moves"
                             Visible="@_permissions.GetValueOrDefault("/waste-moves")" />
        <RadzenPanelMenuItem Text="Entradas en Planta" 
                             Icon="factory" 
                             Path="/entry-plants"
                             Visible="@_permissions.GetValueOrDefault("/entry-plants")" />
        <RadzenPanelMenuItem Text="Entradas en CAC" 
                             Icon="warehouse" 
                             Path="/entry-cacs"
                             Visible="@_permissions.GetValueOrDefault("/entry-cacs")" />
        <RadzenPanelMenuItem Text="Tratamientos" 
                             Icon="science" 
                             Path="/treatment-plants"
                             Visible="@_permissions.GetValueOrDefault("/treatment-plants")" />
    </RadzenPanelMenuItem>

    @* ══════════ ECONOMÍA / CONTRATOS ══════════ *@
    <RadzenPanelMenuItem Text="Economía" 
                         Icon="euro_symbol"
                         Expanded="@IsGroupExpanded("Economia")"
                         Visible="@HasAnyVisibleChild("Economia")">
        
        <RadzenPanelMenuItem Text="Convenios" 
                             Icon="handshake" 
                             Path="/agreements"
                             Visible="@_permissions.GetValueOrDefault("/agreements")" />
        <RadzenPanelMenuItem Text="Cuotas de Mercado" 
                             Icon="pie_chart" 
                             Path="/market-shares"
                             Visible="@_permissions.GetValueOrDefault("/market-shares")" />
        <RadzenPanelMenuItem Text="Liquidaciones" 
                             Icon="receipt_long" 
                             Path="/settlements"
                             Visible="@_permissions.GetValueOrDefault("/settlements")" />
    </RadzenPanelMenuItem>

    @* ══════════ DECLARACIONES ══════════ *@
    <RadzenPanelMenuItem Text="Declaraciones" 
                         Icon="description"
                         Expanded="@IsGroupExpanded("Declaraciones")"
                         Visible="@HasAnyVisibleChild("Declaraciones")">
        
        <RadzenPanelMenuItem Text="Declaraciones de Producto" 
                             Icon="inventory_2" 
                             Path="/product-declarations"
                             Visible="@_permissions.GetValueOrDefault("/product-declarations")" />
    </RadzenPanelMenuItem>

    @* ══════════ SOSTENIBILIDAD ══════════ *@
    <RadzenPanelMenuItem Text="Sostenibilidad" 
                         Icon="eco"
                         Expanded="@IsGroupExpanded("Sostenibilidad")"
                         Visible="@HasAnyVisibleChild("Sostenibilidad")">
        
        <RadzenPanelMenuItem Text="Incidencias" 
                             Icon="report_problem" 
                             Path="/incidents"
                             Visible="@_permissions.GetValueOrDefault("/incidents")" />
        <RadzenPanelMenuItem Text="Zonas DUM" 
                             Icon="map" 
                             Path="/dum-zones"
                             Visible="@_permissions.GetValueOrDefault("/dum-zones")" />
        <RadzenPanelMenuItem Text="Factores de Emisión" 
                             Icon="cloud" 
                             Path="/emissions"
                             Visible="@_permissions.GetValueOrDefault("/emissions")" />
        <RadzenPanelMenuItem Text="Energía de Plantas" 
                             Icon="bolt" 
                             Path="/plant-energies"
                             Visible="@_permissions.GetValueOrDefault("/plant-energies")" />
    </RadzenPanelMenuItem>

    @* ══════════ REPORTING ══════════ *@
    <RadzenPanelMenuItem Text="Reporting" 
                         Icon="assessment"
                         Expanded="@IsGroupExpanded("Reporting")"
                         Visible="@HasAnyVisibleChild("Reporting")">
        
        <RadzenPanelMenuItem Text="Trazabilidad" 
                             Icon="timeline" 
                             Path="/traceability"
                             Visible="@_permissions.GetValueOrDefault("/traceability")" />
        <RadzenPanelMenuItem Text="KPIs Regulatorios" 
                             Icon="speed" 
                             Path="/kpis"
                             Visible="@_permissions.GetValueOrDefault("/kpis")" />
        <RadzenPanelMenuItem Text="Documentos" 
                             Icon="folder_open" 
                             Path="/documents"
                             Visible="@_permissions.GetValueOrDefault("/documents")" />
        
        @* ── Sub-grupo: Dashboards Logísticos ── *@
        <RadzenPanelMenuItem Text="Optimización Logística" 
                             Icon="route" 
                             Path="/logistics/optimization"
                             Visible="@_permissions.GetValueOrDefault("/logistics/optimization")" />
        <RadzenPanelMenuItem Text="Monitorización Pública" 
                             Icon="visibility" 
                             Path="/logistics/public-monitoring"
                             Visible="@_permissions.GetValueOrDefault("/logistics/public-monitoring")" />
        <RadzenPanelMenuItem Text="Panel Operativo" 
                             Icon="dashboard_customize" 
                             Path="/logistics/operational"
                             Visible="@_permissions.GetValueOrDefault("/logistics/operational")" />

        @* ── Sub-grupo: Mapas de Calor ── *@
        <RadzenPanelMenuItem Text="Mapas de Calor" Icon="thermostat"
                             Visible="@HasAnyVisibleChild("MapasCalor")">
            <RadzenPanelMenuItem Text="Densidad de Residuos" 
                                 Icon="layers" 
                                 Path="/reporting/heat-maps/waste-density"
                                 Visible="@_permissions.GetValueOrDefault("/reporting/heat-maps/waste-density")" />
            <RadzenPanelMenuItem Text="Patrones y Estacionalidad" 
                                 Icon="trending_up" 
                                 Path="/reporting/heat-maps/pattern-analysis"
                                 Visible="@_permissions.GetValueOrDefault("/reporting/heat-maps/pattern-analysis")" />
            <RadzenPanelMenuItem Text="Vista Entidad Pública" 
                                 Icon="public" 
                                 Path="/reporting/heat-maps/public-view"
                                 Visible="@_permissions.GetValueOrDefault("/reporting/heat-maps/public-view")" />
        </RadzenPanelMenuItem>

        @* ── Sub-grupo: Huella de Carbono ── *@
        <RadzenPanelMenuItem Text="Huella de Carbono" Icon="co2"
                             Visible="@HasAnyVisibleChild("HuellaCarbono")">
            <RadzenPanelMenuItem Text="Visión Consolidada" 
                                 Icon="analytics" 
                                 Path="/reporting/carbon-footprint/overview"
                                 Visible="@_permissions.GetValueOrDefault("/reporting/carbon-footprint/overview")" />
            <RadzenPanelMenuItem Text="Emisiones del Transporte" 
                                 Icon="local_shipping" 
                                 Path="/reporting/carbon-footprint/transport"
                                 Visible="@_permissions.GetValueOrDefault("/reporting/carbon-footprint/transport")" />
            <RadzenPanelMenuItem Text="Huella Energética Plantas" 
                                 Icon="power" 
                                 Path="/reporting/carbon-footprint/plant-energy"
                                 Visible="@_permissions.GetValueOrDefault("/reporting/carbon-footprint/plant-energy")" />
            <RadzenPanelMenuItem Text="Reporte Productor" 
                                 Icon="person" 
                                 Path="/reporting/carbon-footprint/producer"
                                 Visible="@_permissions.GetValueOrDefault("/reporting/carbon-footprint/producer")" />
            <RadzenPanelMenuItem Text="Vista Entidad Pública" 
                                 Icon="account_balance" 
                                 Path="/reporting/carbon-footprint/public-view"
                                 Visible="@_permissions.GetValueOrDefault("/reporting/carbon-footprint/public-view")" />
        </RadzenPanelMenuItem>

        @* ── Sub-grupo: Cumplimiento Normativo ── *@
        <RadzenPanelMenuItem Text="Cumplimiento Normativo" Icon="gavel"
                             Visible="@HasAnyVisibleChild("CumplimientoNormativo")">
            <RadzenPanelMenuItem Text="Visión SCRAP" 
                                 Icon="fact_check" 
                                 Path="/reporting/regulatory-compliance/scrap-overview"
                                 Visible="@_permissions.GetValueOrDefault("/reporting/regulatory-compliance/scrap-overview")" />
            <RadzenPanelMenuItem Text="Auditoría Cuotas" 
                                 Icon="balance" 
                                 Path="/reporting/regulatory-compliance/market-share-audit"
                                 Visible="@_permissions.GetValueOrDefault("/reporting/regulatory-compliance/market-share-audit")" />
            <RadzenPanelMenuItem Text="Monitorización Convenios" 
                                 Icon="monitor_heart" 
                                 Path="/reporting/regulatory-compliance/agreement-monitoring"
                                 Visible="@_permissions.GetValueOrDefault("/reporting/regulatory-compliance/agreement-monitoring")" />
            <RadzenPanelMenuItem Text="Vista Entidad Pública" 
                                 Icon="public" 
                                 Path="/reporting/regulatory-compliance/public-view"
                                 Visible="@_permissions.GetValueOrDefault("/reporting/regulatory-compliance/public-view")" />
            <RadzenPanelMenuItem Text="Datos Oficina" 
                                 Icon="business_center" 
                                 Path="/reporting/regulatory-compliance/dispatch-data"
                                 Visible="@_permissions.GetValueOrDefault("/reporting/regulatory-compliance/dispatch-data")" />
        </RadzenPanelMenuItem>
    </RadzenPanelMenuItem>

    @* ══════════ SEGURIDAD ══════════ *@
    <RadzenPanelMenuItem Text="Seguridad" 
                         Icon="security"
                         Expanded="@IsGroupExpanded("Seguridad")"
                         Visible="@HasAnyVisibleChild("Seguridad")">
        
        <RadzenPanelMenuItem Text="Usuarios" 
                             Icon="people" 
                             Path="/users"
                             Visible="@_permissions.GetValueOrDefault("/users")" />
        <RadzenPanelMenuItem Text="Perfiles" 
                             Icon="admin_panel_settings" 
                             Path="/profiles"
                             Visible="@_permissions.GetValueOrDefault("/profiles")" />
        <RadzenPanelMenuItem Text="Permisos por Página" 
                             Icon="lock" 
                             Path="/security/page-permissions"
                             Visible="@_permissions.GetValueOrDefault("/security/page-permissions")" />
    </RadzenPanelMenuItem>

</RadzenPanelMenu>

@code {
    private Dictionary<string, bool> _permissions = new();

    // ══ Grupos y sus rutas hijas (para decidir si un grupo padre es visible) ══
    private static readonly Dictionary<string, string[]> _groupRoutes = new()
    {
        ["Configuracion"] = new[] { "/entities", "/ler-codes", "/residues", "/treatment-operations" },
        ["Operaciones"] = new[] { "/service-orders", "/waste-moves", "/entry-plants", "/entry-cacs", "/treatment-plants" },
        ["Economia"] = new[] { "/agreements", "/market-shares", "/settlements" },
        ["Declaraciones"] = new[] { "/product-declarations" },
        ["Sostenibilidad"] = new[] { "/incidents", "/dum-zones", "/emissions", "/plant-energies" },
        ["Reporting"] = new[] { 
            "/traceability", "/kpis", "/documents",
            "/logistics/optimization", "/logistics/public-monitoring", "/logistics/operational",
            "/reporting/heat-maps/waste-density", "/reporting/heat-maps/pattern-analysis", "/reporting/heat-maps/public-view",
            "/reporting/carbon-footprint/overview", "/reporting/carbon-footprint/transport", "/reporting/carbon-footprint/plant-energy",
            "/reporting/carbon-footprint/producer", "/reporting/carbon-footprint/public-view",
            "/reporting/regulatory-compliance/scrap-overview", "/reporting/regulatory-compliance/market-share-audit",
            "/reporting/regulatory-compliance/agreement-monitoring", "/reporting/regulatory-compliance/public-view",
            "/reporting/regulatory-compliance/dispatch-data"
        },
        ["Seguridad"] = new[] { "/users", "/profiles", "/security/page-permissions" },
        // Sub-grupos dentro de Reporting:
        ["MapasCalor"] = new[] { "/reporting/heat-maps/waste-density", "/reporting/heat-maps/pattern-analysis", "/reporting/heat-maps/public-view" },
        ["HuellaCarbono"] = new[] { "/reporting/carbon-footprint/overview", "/reporting/carbon-footprint/transport", "/reporting/carbon-footprint/plant-energy", "/reporting/carbon-footprint/producer", "/reporting/carbon-footprint/public-view" },
        ["CumplimientoNormativo"] = new[] { "/reporting/regulatory-compliance/scrap-overview", "/reporting/regulatory-compliance/market-share-audit", "/reporting/regulatory-compliance/agreement-monitoring", "/reporting/regulatory-compliance/public-view", "/reporting/regulatory-compliance/dispatch-data" },
    };

    protected override async Task OnInitializedAsync()
    {
        await LoadPermissionsAsync();
    }

    private async Task LoadPermissionsAsync()
    {
        // Obtener todas las rutas únicas del menú
        var allRoutes = _groupRoutes.Values
            .SelectMany(r => r)
            .Distinct()
            .Append("/") // Home
            .ToList();

        // Cargar permisos de forma batch para evitar N+1 queries
        foreach (var route in allRoutes)
        {
            _permissions[route] = await PagePermissionService.CanAccessRouteAsync(route);
        }
    }

    /// <summary>
    /// Un grupo padre es visible si al menos un hijo tiene permiso.
    /// </summary>
    private bool HasAnyVisibleChild(string groupName)
    {
        if (!_groupRoutes.TryGetValue(groupName, out var routes))
            return false;

        return routes.Any(r => _permissions.GetValueOrDefault(r));
    }

    /// <summary>
    /// Consulta el NavMenuStateService para saber si un grupo está expandido.
    /// Mantiene la compatibilidad con el servicio Scoped existente.
    /// </summary>
    private bool IsGroupExpanded(string groupName)
    {
        return !NavMenuState.IsCollapsed(groupName);
    }
}
```

---

## Paso 4: Adaptar `NavMenuStateService.cs`

El `NavMenuStateService` existente puede seguir funcionando tal cual — solo necesitas que el `NavMenu.razor` llame a `IsGroupExpanded()` para los grupos principales (como se muestra arriba). Si quieres que el estado de expansión de Radzen se sincronice bidireccionalmente, puedes escuchar el evento `ExpandedChanged` de cada `RadzenPanelMenuItem` padre:

```csharp
// Opcional: Si quieres sincronizar el estado de expansión de Radzen con NavMenuStateService
// Añade esto al @code del NavMenu.razor:

private void OnGroupExpandedChanged(string groupName, bool expanded)
{
    if (expanded)
        NavMenuState.Expand(groupName);
    else
        NavMenuState.Collapse(groupName);
}
```

Y en cada grupo padre del markup:

```razor
<RadzenPanelMenuItem Text="Configuración" 
                     Icon="settings"
                     Expanded="@IsGroupExpanded("Configuracion")"
                     ExpandedChanged="@(expanded => OnGroupExpandedChanged("Configuracion", expanded))"
                     Visible="@HasAnyVisibleChild("Configuracion")">
```

> **Nota**: `RadzenPanelMenuItem` soporta `@bind-Expanded`, pero como necesitamos conectar con `NavMenuStateService`, es mejor usar el patrón manual `Expanded` + `ExpandedChanged`.

---

## Paso 5: CSS y limpieza

### 5.1. Eliminar CSS custom del NavMenu anterior

Busca y elimina en tus archivos CSS (probablemente `NavMenu.razor.css` o `app.css`) todo lo relacionado con:
- `.sidebar`, `.sidebar-collapsed`
- `.nav-group`, `.nav-group-title`, `.chevron`
- `.nav-item`, `.nav-link`
- Animaciones de chevron
- Media queries del sidebar custom

### 5.2. Ajustes CSS opcionales para Radzen

Si necesitas personalizar el sidebar, usa las variables CSS de Radzen:

```css
/* En tu archivo CSS global */
:root {
    --rz-sidebar-width: 280px;                    /* Ancho del sidebar expandido */
    --rz-sidebar-min-width: 52px;                 /* Ancho colapsado (solo iconos) */
    --rz-sidebar-background-color: var(--rz-base-100);
    --rz-panel-menu-item-padding: 0.5rem 1rem;
}

/* Scroll en el menú si hay muchos items */
.rz-sidebar .rz-panel-menu {
    overflow-y: auto;
    flex: 1;
}
```

---

## Paso 6: Verificación

Tras aplicar los cambios, verifica:

1. **Compilación**: `dotnet build` sin errores.
2. **Permisos**: cada perfil de usuario solo ve las rutas asignadas en `PagePermissions`.
3. **Navegación**: hacer clic en un item navega correctamente (el `Path` debe coincidir con `@page "/ruta"` de cada página).
4. **Responsive**: en pantalla pequeña, el sidebar colapsa automáticamente.
5. **Estado del menú**: los grupos recuerdan si están expandidos/colapsados durante la sesión (via `NavMenuStateService`).
6. **Buscador global**: sigue funcionando en el header.
7. **Modo oscuro/claro**: si tienes toggle de tema, verifica que Radzen lo respeta (puedes inyectar `ThemeService` para cambiar entre temas programáticamente).
8. **Triple capa de seguridad**: `ProfileAuthorizeView` → `PagePermissionService` → `RouteAccessGuard` sigue intacta.

---

---
---

# PARTE II — Migración de todas las Grids a `RadzenDataGrid`

---

## Paso 7: Componente a utilizar — `RadzenDataGrid<TItem>`

`RadzenDataGrid` es el componente de tabla de datos de Radzen. Reemplaza cualquier tabla HTML manual, `<table>`, componente grid custom, o `QuickGrid` que exista actualmente en la aplicación.

### Capacidades nativas de RadzenDataGrid:

- **Sorting**: simple y multi-columna (`AllowSorting="true"`, `AllowMultiColumnSorting="true"`)
- **Filtering**: simple por columna y avanzado (`AllowFiltering="true"`, `FilterMode="FilterMode.Advanced"` o `FilterMode.Simple` o `FilterMode.CheckBoxList`)
- **Paging**: con selector de tamaño de página (`AllowPaging="true"`, `PageSize="20"`, `PageSizeOptions="new int[] { 10, 20, 50, 100 }"`)
- **Grouping**: agrupación por columnas (`AllowGrouping="true"`)
- **Selection**: selección de filas individual y múltiple (`SelectionMode="DataGridSelectionMode.Single"` o `.Multiple`)
- **Inline Editing**: edición en línea e in-cell (`EditMode="DataGridEditMode.Single"` o `.Multiple`)
- **Column Templates**: templates custom para celdas, cabeceras y filtros
- **Export**: exportación nativa a Excel y CSV
- **Virtualization**: renderizado virtual para grandes volúmenes de datos (`AllowVirtualization="true"`)
- **Density**: modo compacto para tablas densas (`Density="Density.Compact"`)
- **LoadData**: carga bajo demanda server-side (paginación, filtrado y ordenación en servidor)
- **Responsive**: adaptación automática a pantallas pequeñas
- **Column Picker**: el usuario puede mostrar/ocultar columnas
- **Save/Load Settings**: persistencia de configuración del usuario (orden de columnas, filtros, etc.)
- **Conditional Formatting**: estilos condicionales por fila y celda
- **Empty state**: template personalizable cuando no hay datos

---

## Paso 8: Patrón general de migración de grids

### 8.1. Estructura básica de un `RadzenDataGrid`

```razor
<RadzenDataGrid @ref="_grid"
                TItem="MiDto"
                Data="@_items"
                Count="@_totalCount"
                LoadData="@LoadData"
                IsLoading="@_isLoading"
                AllowSorting="true"
                AllowFiltering="true"
                AllowPaging="true"
                PageSize="20"
                PageSizeOptions="@(new int[] { 10, 20, 50, 100 })"
                PagerHorizontalAlign="HorizontalAlign.Center"
                ShowPagingSummary="true"
                PagingSummaryFormat="Mostrando {0} a {1} de {2} registros"
                FilterMode="FilterMode.Simple"
                FilterCaseSensitivity="FilterCaseSensitivity.CaseInsensitive"
                Density="Density.Default"
                ColumnWidth="200px"
                Style="height: auto;">
    <HeaderTemplate>
        @* ══ Barra de herramientas sobre la grid ══ *@
        <RadzenStack Orientation="Orientation.Horizontal" 
                     AlignItems="AlignItems.Center" 
                     Gap="0.5rem" Class="rz-p-2">
            <RadzenButton Text="Nuevo" 
                          Icon="add_circle_outline" 
                          ButtonStyle="ButtonStyle.Primary"
                          Click="@OnCreateNew"
                          Visible="@_canWrite" />
            <RadzenButton Text="Exportar Excel" 
                          Icon="grid_on" 
                          ButtonStyle="ButtonStyle.Light"
                          Click="@(args => ExportToExcel())" />
            <RadzenButton Text="Exportar CSV" 
                          Icon="wrap_text" 
                          ButtonStyle="ButtonStyle.Light"
                          Click="@(args => ExportToCsv())" />
        </RadzenStack>
    </HeaderTemplate>
    <EmptyTemplate>
        <p style="text-align: center; padding: 2rem;">
            <RadzenIcon Icon="info" /> No se encontraron registros.
        </p>
    </EmptyTemplate>
    <Columns>
        @* ══ Definición de columnas — ver ejemplos por entidad más abajo ══ *@
    </Columns>
</RadzenDataGrid>
```

### 8.2. Código base (`@code`) con patrón LoadData server-side

Este es el patrón que **todas las grids deben seguir** para trabajar con MediatR:

```csharp
@code {
    private RadzenDataGrid<MiDto> _grid = default!;
    private IEnumerable<MiDto> _items = Enumerable.Empty<MiDto>();
    private int _totalCount;
    private bool _isLoading;
    private bool _canWrite;

    protected override async Task OnInitializedAsync()
    {
        // Verificar permiso de escritura para mostrar/ocultar botones
        _canWrite = await PagePermissionService.HasWriteAccessAsync(
            NavigationManager.ToBaseRelativePath(NavigationManager.Uri));
    }

    private async Task LoadData(LoadDataArgs args)
    {
        _isLoading = true;

        try
        {
            // Construir la query MediatR con los parámetros de la grid
            var query = new GetMiEntidadQuery
            {
                Skip = args.Skip ?? 0,
                Top = args.Top ?? 20,
                OrderBy = args.OrderBy,   // String con formato "Campo asc, Campo2 desc"
                Filter = args.Filter      // String OData filter expression
            };

            var result = await Mediator.Send(query);

            _items = result.Items;
            _totalCount = result.TotalCount;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void OnCreateNew()
    {
        NavigationManager.NavigateTo("/mi-entidad/new");
    }

    private async Task ExportToExcel()
    {
        // Usar las columnas visibles y filtros actuales de la grid
        await _grid.ExportAsync("excel");
    }

    private async Task ExportToCsv()
    {
        await _grid.ExportAsync("csv");
    }
}
```

### 8.3. Adaptación del Query Handler MediatR para soportar LoadData

Los Query Handlers actuales deben aceptar los parámetros `Skip`, `Top`, `OrderBy` y `Filter` que `RadzenDataGrid` envía via `LoadDataArgs`. Patrón:

```csharp
public class GetMiEntidadQuery : IRequest<PaginatedResult<MiDto>>
{
    public int Skip { get; set; }
    public int Top { get; set; } = 20;
    public string? OrderBy { get; set; }
    public string? Filter { get; set; }
}

public class GetMiEntidadQueryHandler : IRequestHandler<GetMiEntidadQuery, PaginatedResult<MiDto>>
{
    public async Task<PaginatedResult<MiDto>> Handle(GetMiEntidadQuery request, CancellationToken ct)
    {
        var query = _db.MiEntidad
            .Where(e => e.OwnerId == _currentUser.OwnerId)  // Multi-tenant
            .AsQueryable();

        query = _dataScope.ApplyScope(query);                 // Filtro por perfil

        // ══ Aplicar filtro de RadzenDataGrid ══
        if (!string.IsNullOrEmpty(request.Filter))
        {
            query = query.Where(request.Filter);  // System.Linq.Dynamic.Core
        }

        // ══ Total antes de paginar ══
        var totalCount = await query.CountAsync(ct);

        // ══ Aplicar ordenación ══
        if (!string.IsNullOrEmpty(request.OrderBy))
        {
            query = query.OrderBy(request.OrderBy);  // System.Linq.Dynamic.Core
        }
        else
        {
            query = query.OrderByDescending(e => e.CreatedAt);  // Default
        }

        // ══ Paginación ══
        var items = await query
            .Skip(request.Skip)
            .Take(request.Top)
            .Select(e => new MiDto { /* mapeo */ })
            .ToListAsync(ct);

        return new PaginatedResult<MiDto>(items, totalCount);
    }
}
```

> **Dependencia necesaria**: `System.Linq.Dynamic.Core` (NuGet) permite aplicar las expressions string de `OrderBy` y `Filter` directamente sobre `IQueryable`. Instalar con:
> ```bash
> dotnet add package System.Linq.Dynamic.Core
> ```

### 8.4. DTO de resultado paginado (si no existe ya)

```csharp
public class PaginatedResult<T>
{
    public IEnumerable<T> Items { get; set; } = Enumerable.Empty<T>();
    public int TotalCount { get; set; }

    public PaginatedResult() { }
    public PaginatedResult(IEnumerable<T> items, int totalCount)
    {
        Items = items;
        TotalCount = totalCount;
    }
}
```

---

## Paso 9: Propiedades clave de `RadzenDataGridColumn`

Referencia rápida de las propiedades más usadas al definir columnas:

| Propiedad | Tipo | Descripción |
|-----------|------|-------------|
| `Property` | `string` | Nombre de la propiedad del DTO (para binding automático, sorting y filtering) |
| `Title` | `string` | Título visible en el header |
| `Width` | `string` | Ancho (ej: `"120px"`, `"15%"`) |
| `FormatString` | `string` | Formato .NET (ej: `"{0:N2}"` para decimales, `"{0:dd/MM/yyyy}"` para fechas, `"{0:C}"` para moneda) |
| `TextAlign` | `TextAlign` | Alineación: `TextAlign.Center`, `TextAlign.Right`, etc. |
| `Filterable` | `bool` | Si la columna es filtrable (default `true`) |
| `Sortable` | `bool` | Si la columna es ordenable (default `true`) |
| `Frozen` | `bool` | Columna fija al hacer scroll horizontal |
| `Visible` | `bool` | Visibilidad de la columna |
| `Type` | `typeof(...)` | Tipo de dato para filtros avanzados (ej: `typeof(DateTime)`) |
| `FilterValue` | `object` | Valor de filtro inicial |
| `FilterOperator` | `FilterOperator` | Operador de filtro inicial |

### Templates de columna (para contenido custom):

```razor
@* ══ Template de celda (contenido custom) ══ *@
<RadzenDataGridColumn TItem="MiDto" Property="Status" Title="Estado" Width="120px">
    <Template Context="item">
        <RadzenBadge Text="@item.Status" 
                     BadgeStyle="@GetBadgeStyle(item.Status)" />
    </Template>
</RadzenDataGridColumn>

@* ══ Template con botones de acción ══ *@
<RadzenDataGridColumn TItem="MiDto" Title="Acciones" Width="150px" 
                      Filterable="false" Sortable="false" TextAlign="TextAlign.Center">
    <Template Context="item">
        <RadzenButton Icon="edit" 
                      ButtonStyle="ButtonStyle.Light" 
                      Size="ButtonSize.Small"
                      Click="@(() => OnEdit(item))"
                      Visible="@_canWrite" />
        <RadzenButton Icon="visibility" 
                      ButtonStyle="ButtonStyle.Info" 
                      Size="ButtonSize.Small"
                      Click="@(() => OnView(item))" />
        <RadzenButton Icon="delete" 
                      ButtonStyle="ButtonStyle.Danger" 
                      Size="ButtonSize.Small"
                      Click="@(() => OnDelete(item))"
                      Visible="@_canWrite" />
    </Template>
</RadzenDataGridColumn>

@* ══ Template con link de navegación ══ *@
<RadzenDataGridColumn TItem="MiDto" Property="Reference" Title="Referencia" Width="180px">
    <Template Context="item">
        <RadzenLink Path="@($"/mi-entidad/{item.Id}")" Text="@item.Reference" />
    </Template>
</RadzenDataGridColumn>

@* ══ Template booleano con icono ══ *@
<RadzenDataGridColumn TItem="MiDto" Property="IsActive" Title="Activo" Width="80px" 
                      TextAlign="TextAlign.Center">
    <Template Context="item">
        <RadzenIcon Icon="@(item.IsActive ? "check_circle" : "cancel")"
                    Style="@($"color: {(item.IsActive ? "green" : "red")}")" />
    </Template>
</RadzenDataGridColumn>
```

---

## Paso 10: Migración grid por grid — Inventario de pantallas con listado

A continuación se detalla **cada pantalla que contiene una grid/tabla** y cómo debe transformarse a `RadzenDataGrid`. Copilot debe aplicar el patrón del Paso 8 a cada una, adaptando las columnas al DTO correspondiente.

### 10.1. Módulo Configuración

#### `EntityList.razor` — Listado de Entidades

```razor
<RadzenDataGrid TItem="EntityDto" Data="@_items" Count="@_totalCount" 
                LoadData="@LoadData" IsLoading="@_isLoading"
                AllowSorting="true" AllowFiltering="true" AllowPaging="true"
                PageSize="20" PageSizeOptions="@(new int[] { 10, 20, 50, 100 })"
                ShowPagingSummary="true"
                PagingSummaryFormat="Mostrando {0} a {1} de {2} entidades"
                FilterMode="FilterMode.Simple"
                FilterCaseSensitivity="FilterCaseSensitivity.CaseInsensitive">
    <Columns>
        <RadzenDataGridColumn TItem="EntityDto" Property="Name" Title="Nombre" Width="200px" />
        <RadzenDataGridColumn TItem="EntityDto" Property="NationalId" Title="NIF/CIF" Width="120px" />
        <RadzenDataGridColumn TItem="EntityDto" Property="CenterCode" Title="NIMA" Width="120px" />
        <RadzenDataGridColumn TItem="EntityDto" Property="EntityRole" Title="Rol" Width="130px">
            <Template Context="item">
                <RadzenBadge Text="@item.EntityRole" />
            </Template>
        </RadzenDataGridColumn>
        <RadzenDataGridColumn TItem="EntityDto" Property="Address" Title="Dirección" Width="250px" />
        <RadzenDataGridColumn TItem="EntityDto" Property="PhoneNumber" Title="Teléfono" Width="120px" />
        <RadzenDataGridColumn TItem="EntityDto" Property="Email" Title="Email" Width="180px" />
        <RadzenDataGridColumn TItem="EntityDto" Property="IsActive" Title="Activo" Width="80px" 
                              TextAlign="TextAlign.Center">
            <Template Context="item">
                <RadzenIcon Icon="@(item.IsActive ? "check_circle" : "cancel")"
                            Style="@($"color: {(item.IsActive ? "var(--rz-success)" : "var(--rz-danger)")}")" />
            </Template>
        </RadzenDataGridColumn>
        @* Columna de acciones *@
        <RadzenDataGridColumn TItem="EntityDto" Title="Acciones" Width="120px" 
                              Filterable="false" Sortable="false" TextAlign="TextAlign.Center">
            <Template Context="item">
                <RadzenButton Icon="edit" Size="ButtonSize.Small" ButtonStyle="ButtonStyle.Light"
                              Click="@(() => NavigationManager.NavigateTo($"/entities/{item.Id}"))"
                              Visible="@_canWrite" />
                <RadzenButton Icon="visibility" Size="ButtonSize.Small" ButtonStyle="ButtonStyle.Info"
                              Click="@(() => NavigationManager.NavigateTo($"/entities/{item.Id}"))" />
            </Template>
        </RadzenDataGridColumn>
    </Columns>
</RadzenDataGrid>
```

#### `LerCodeList.razor` — Listado de Códigos LER

```razor
<Columns>
    <RadzenDataGridColumn TItem="LerCodeDto" Property="Code" Title="Código LER" Width="120px" Frozen="true" />
    <RadzenDataGridColumn TItem="LerCodeDto" Property="Description" Title="Descripción" Width="350px" />
    <RadzenDataGridColumn TItem="LerCodeDto" Property="IsDangerous" Title="Peligroso" Width="100px" TextAlign="TextAlign.Center">
        <Template Context="item">
            <RadzenIcon Icon="@(item.IsDangerous ? "warning" : "")" 
                        Style="color: var(--rz-warning)" />
        </Template>
    </RadzenDataGridColumn>
    <RadzenDataGridColumn TItem="LerCodeDto" Property="IsRAEE" Title="RAEE" Width="80px" TextAlign="TextAlign.Center">
        <Template Context="item">
            <RadzenIcon Icon="@(item.IsRAEE ? "devices" : "")" />
        </Template>
    </RadzenDataGridColumn>
    @* Acciones solo para ADMIN *@
</Columns>
```

#### `ResidueList.razor` — Listado de Residuos

```razor
<Columns>
    <RadzenDataGridColumn TItem="ResidueDto" Property="Name" Title="Nombre" Width="200px" />
    <RadzenDataGridColumn TItem="ResidueDto" Property="ResidueType" Title="Tipo" Width="120px">
        <Template Context="item">
            <RadzenBadge Text="@item.ResidueType"
                         BadgeStyle="@(item.ResidueType == "Waste" ? BadgeStyle.Warning : BadgeStyle.Info)" />
        </Template>
    </RadzenDataGridColumn>
    <RadzenDataGridColumn TItem="ResidueDto" Property="LerCode" Title="Código LER" Width="120px" />
    <RadzenDataGridColumn TItem="ResidueDto" Property="LerDescription" Title="Desc. LER" Width="250px" />
    <RadzenDataGridColumn TItem="ResidueDto" Property="IsDangerous" Title="Peligroso" Width="90px" TextAlign="TextAlign.Center">
        <Template Context="item">
            <RadzenIcon Icon="@(item.IsDangerous ? "warning" : "")" Style="color: var(--rz-warning)" />
        </Template>
    </RadzenDataGridColumn>
    @* Acciones *@
</Columns>
```

#### `TreatmentOperationList.razor` — Operaciones de Tratamiento (R/D)

```razor
<Columns>
    <RadzenDataGridColumn TItem="TreatmentOperationDto" Property="Code" Title="Código" Width="80px" />
    <RadzenDataGridColumn TItem="TreatmentOperationDto" Property="Description" Title="Descripción" Width="400px" />
    <RadzenDataGridColumn TItem="TreatmentOperationDto" Property="OperationType" Title="Tipo" Width="130px">
        <Template Context="item">
            <RadzenBadge Text="@item.OperationType"
                         BadgeStyle="@(item.OperationType == "R" ? BadgeStyle.Success : BadgeStyle.Danger)" />
        </Template>
    </RadzenDataGridColumn>
    <RadzenDataGridColumn TItem="TreatmentOperationDto" Property="IsRecycling" Title="Reciclaje" Width="90px" TextAlign="TextAlign.Center">
        <Template Context="item">
            <RadzenIcon Icon="@(item.IsRecycling ? "recycling" : "")" Style="color: var(--rz-success)" />
        </Template>
    </RadzenDataGridColumn>
    @* Acciones solo ADMIN *@
</Columns>
```

---

### 10.2. Módulo Operaciones

#### `ServiceOrderList.razor` — Órdenes de Servicio

```razor
<RadzenDataGrid TItem="ServiceOrderDto" ...
                AllowSorting="true" AllowMultiColumnSorting="true"
                AllowFiltering="true" FilterMode="FilterMode.Simple"
                AllowPaging="true" PageSize="20">
    <Columns>
        <RadzenDataGridColumn TItem="ServiceOrderDto" Property="ServiceOrderNumber" Title="Nº Orden" Width="140px" Frozen="true">
            <Template Context="item">
                <RadzenLink Path="@($"/service-orders/{item.Id}")" Text="@item.ServiceOrderNumber" />
            </Template>
        </RadzenDataGridColumn>
        <RadzenDataGridColumn TItem="ServiceOrderDto" Property="Status" Title="Estado" Width="120px">
            <Template Context="item">
                <RadzenBadge Text="@item.StatusLabel" 
                             BadgeStyle="@GetServiceOrderBadge(item.Status)" />
            </Template>
        </RadzenDataGridColumn>
        <RadzenDataGridColumn TItem="ServiceOrderDto" Property="Priority" Title="Prioridad" Width="100px">
            <Template Context="item">
                <RadzenBadge Text="@item.PriorityLabel" 
                             BadgeStyle="@GetPriorityBadge(item.Priority)" />
            </Template>
        </RadzenDataGridColumn>
        <RadzenDataGridColumn TItem="ServiceOrderDto" Property="IssuedByName" Title="Emisor" Width="180px" />
        <RadzenDataGridColumn TItem="ServiceOrderDto" Property="LerCode" Title="LER" Width="100px" />
        <RadzenDataGridColumn TItem="ServiceOrderDto" Property="WasteStream" Title="Flujo" Width="140px" />
        <RadzenDataGridColumn TItem="ServiceOrderDto" Property="EstimatedWeight" Title="Peso Est. (Kg)" Width="120px"
                              FormatString="{0:N2}" TextAlign="TextAlign.Right" />
        <RadzenDataGridColumn TItem="ServiceOrderDto" Property="PlannedPickupStart" Title="Recogida Plan." Width="140px"
                              FormatString="{0:dd/MM/yyyy HH:mm}" />
        <RadzenDataGridColumn TItem="ServiceOrderDto" Property="CreatedAt" Title="Creado" Width="120px"
                              FormatString="{0:dd/MM/yyyy}" />
        @* Acciones *@
        <RadzenDataGridColumn TItem="ServiceOrderDto" Title="" Width="120px" 
                              Filterable="false" Sortable="false" TextAlign="TextAlign.Center">
            <Template Context="item">
                <RadzenButton Icon="edit" Size="ButtonSize.Small" ButtonStyle="ButtonStyle.Light"
                              Click="@(() => OnEdit(item))" Visible="@(_canWrite && item.IsEditable)" />
                <RadzenButton Icon="visibility" Size="ButtonSize.Small" ButtonStyle="ButtonStyle.Info"
                              Click="@(() => OnView(item))" />
            </Template>
        </RadzenDataGridColumn>
    </Columns>
</RadzenDataGrid>
```

#### `WasteMoveList.razor` — Traslados

```razor
<Columns>
    <RadzenDataGridColumn TItem="WasteMoveDto" Property="WasteMoveReference" Title="Referencia" Width="150px" Frozen="true">
        <Template Context="item">
            <RadzenLink Path="@($"/waste-moves/{item.Id}")" Text="@item.WasteMoveReference" />
        </Template>
    </RadzenDataGridColumn>
    <RadzenDataGridColumn TItem="WasteMoveDto" Property="ServiceStatus" Title="Estado" Width="130px">
        <Template Context="item">
            <RadzenBadge Text="@item.ServiceStatusLabel" 
                         BadgeStyle="@GetWasteMoveBadge(item.ServiceStatus)" />
        </Template>
    </RadzenDataGridColumn>
    <RadzenDataGridColumn TItem="WasteMoveDto" Property="SourceName" Title="Origen" Width="180px" />
    <RadzenDataGridColumn TItem="WasteMoveDto" Property="DestinationName" Title="Destino" Width="180px" />
    <RadzenDataGridColumn TItem="WasteMoveDto" Property="CarrierName" Title="Transportista" Width="160px" />
    <RadzenDataGridColumn TItem="WasteMoveDto" Property="PlannedPickupStart" Title="Recogida" Width="140px"
                          FormatString="{0:dd/MM/yyyy HH:mm}" />
    <RadzenDataGridColumn TItem="WasteMoveDto" Property="TotalWeight" Title="Peso (Kg)" Width="110px"
                          FormatString="{0:N2}" TextAlign="TextAlign.Right" />
    <RadzenDataGridColumn TItem="WasteMoveDto" Property="ScrapName" Title="SCRAP" Width="140px" />
    @* Acciones *@
</Columns>
```

#### `EntryPlantList.razor` — Entradas en Planta

```razor
<Columns>
    <RadzenDataGridColumn TItem="EntryPlantDto" Property="TicketScale" Title="Ticket Báscula" Width="130px" Frozen="true" />
    <RadzenDataGridColumn TItem="EntryPlantDto" Property="PlantName" Title="Planta" Width="180px" />
    <RadzenDataGridColumn TItem="EntryPlantDto" Property="EntryDate" Title="Fecha Entrada" Width="140px"
                          FormatString="{0:dd/MM/yyyy HH:mm}" />
    <RadzenDataGridColumn TItem="EntryPlantDto" Property="GrossWeight" Title="Peso Bruto (Kg)" Width="120px"
                          FormatString="{0:N2}" TextAlign="TextAlign.Right" />
    <RadzenDataGridColumn TItem="EntryPlantDto" Property="TareWeight" Title="Tara (Kg)" Width="100px"
                          FormatString="{0:N2}" TextAlign="TextAlign.Right" />
    <RadzenDataGridColumn TItem="EntryPlantDto" Property="NetWeight" Title="Peso Neto (Kg)" Width="120px"
                          FormatString="{0:N2}" TextAlign="TextAlign.Right" />
    <RadzenDataGridColumn TItem="EntryPlantDto" Property="WasteMoveReference" Title="Traslado" Width="140px" />
    @* Acciones *@
</Columns>
```

#### `EntryCACList.razor` — Entradas en CAC

```razor
@* Misma estructura que EntryPlantList pero con campos propios de CAC *@
<Columns>
    <RadzenDataGridColumn TItem="EntryCACDto" Property="TicketScale" Title="Ticket Báscula" Width="130px" Frozen="true" />
    <RadzenDataGridColumn TItem="EntryCACDto" Property="CACName" Title="CAC" Width="180px" />
    <RadzenDataGridColumn TItem="EntryCACDto" Property="EntryDate" Title="Fecha Entrada" Width="140px"
                          FormatString="{0:dd/MM/yyyy HH:mm}" />
    <RadzenDataGridColumn TItem="EntryCACDto" Property="NetWeight" Title="Peso Neto (Kg)" Width="120px"
                          FormatString="{0:N2}" TextAlign="TextAlign.Right" />
    <RadzenDataGridColumn TItem="EntryCACDto" Property="WasteMoveReference" Title="Traslado" Width="140px" />
    @* Acciones *@
</Columns>
```

#### `TreatmentPlantList.razor` — Tratamientos

```razor
<Columns>
    <RadzenDataGridColumn TItem="TreatmentPlantDto" Property="PlantName" Title="Planta" Width="180px" />
    <RadzenDataGridColumn TItem="TreatmentPlantDto" Property="TreatmentDate" Title="Fecha Tratamiento" Width="140px"
                          FormatString="{0:dd/MM/yyyy}" />
    <RadzenDataGridColumn TItem="TreatmentPlantDto" Property="OperationCode" Title="Operación R/D" Width="100px" />
    <RadzenDataGridColumn TItem="TreatmentPlantDto" Property="WeightTotal" Title="Peso Total (Kg)" Width="120px"
                          FormatString="{0:N2}" TextAlign="TextAlign.Right" />
    <RadzenDataGridColumn TItem="TreatmentPlantDto" Property="WeightReused" Title="Reutilizado (Kg)" Width="130px"
                          FormatString="{0:N2}" TextAlign="TextAlign.Right" />
    <RadzenDataGridColumn TItem="TreatmentPlantDto" Property="WeightValued" Title="Valorizado (Kg)" Width="130px"
                          FormatString="{0:N2}" TextAlign="TextAlign.Right" />
    @* Acciones *@
</Columns>
```

---

### 10.3. Módulo Economía

#### `AgreementList.razor` — Convenios

```razor
<Columns>
    <RadzenDataGridColumn TItem="AgreementDto" Property="AgreementNumber" Title="Nº Convenio" Width="140px" Frozen="true">
        <Template Context="item">
            <RadzenLink Path="@($"/agreements/{item.Id}")" Text="@item.AgreementNumber" />
        </Template>
    </RadzenDataGridColumn>
    <RadzenDataGridColumn TItem="AgreementDto" Property="ScrapName" Title="SCRAP" Width="160px" />
    <RadzenDataGridColumn TItem="AgreementDto" Property="PublicEntityName" Title="Entidad Pública" Width="180px" />
    <RadzenDataGridColumn TItem="AgreementDto" Property="StartDate" Title="Inicio" Width="110px" FormatString="{0:dd/MM/yyyy}" />
    <RadzenDataGridColumn TItem="AgreementDto" Property="EndDate" Title="Fin" Width="110px" FormatString="{0:dd/MM/yyyy}" />
    <RadzenDataGridColumn TItem="AgreementDto" Property="IsActive" Title="Vigente" Width="80px" TextAlign="TextAlign.Center">
        <Template Context="item">
            <RadzenIcon Icon="@(item.IsActive ? "check_circle" : "cancel")"
                        Style="@($"color: {(item.IsActive ? "var(--rz-success)" : "var(--rz-danger)")}")" />
        </Template>
    </RadzenDataGridColumn>
    @* Acciones *@
</Columns>
```

#### `MarketShareList.razor` — Cuotas de Mercado

```razor
<Columns>
    <RadzenDataGridColumn TItem="MarketShareDto" Property="ScrapName" Title="SCRAP" Width="160px" />
    <RadzenDataGridColumn TItem="MarketShareDto" Property="Year" Title="Año" Width="80px" TextAlign="TextAlign.Center" />
    <RadzenDataGridColumn TItem="MarketShareDto" Property="Period" Title="Período" Width="100px" />
    <RadzenDataGridColumn TItem="MarketShareDto" Property="WasteStream" Title="Flujo" Width="140px" />
    <RadzenDataGridColumn TItem="MarketShareDto" Property="Weight" Title="Cuota (Kg)" Width="120px"
                          FormatString="{0:N2}" TextAlign="TextAlign.Right" />
    @* Acciones *@
</Columns>
```

#### `SettlementList.razor` — Liquidaciones

```razor
<Columns>
    <RadzenDataGridColumn TItem="SettlementDto" Property="Reference" Title="Referencia" Width="140px" Frozen="true" />
    <RadzenDataGridColumn TItem="SettlementDto" Property="ScrapName" Title="SCRAP" Width="160px" />
    <RadzenDataGridColumn TItem="SettlementDto" Property="Period" Title="Período" Width="120px" />
    <RadzenDataGridColumn TItem="SettlementDto" Property="TotalAmount" Title="Importe Total" Width="130px"
                          FormatString="{0:N2} €" TextAlign="TextAlign.Right" />
    <RadzenDataGridColumn TItem="SettlementDto" Property="Status" Title="Estado" Width="110px">
        <Template Context="item">
            <RadzenBadge Text="@item.StatusLabel" BadgeStyle="@GetSettlementBadge(item.Status)" />
        </Template>
    </RadzenDataGridColumn>
    @* Acciones *@
</Columns>
```

---

### 10.4. Módulo Declaraciones

#### `ProductDeclarationList.razor` — Declaraciones de Producto

```razor
<Columns>
    <RadzenDataGridColumn TItem="ProductDeclarationDto" Property="Reference" Title="Referencia" Width="140px" Frozen="true" />
    <RadzenDataGridColumn TItem="ProductDeclarationDto" Property="ProducerName" Title="Productor" Width="180px" />
    <RadzenDataGridColumn TItem="ProductDeclarationDto" Property="PeriodLabel" Title="Período" Width="120px" />
    <RadzenDataGridColumn TItem="ProductDeclarationDto" Property="CategoryLabel" Title="Categoría" Width="140px" />
    <RadzenDataGridColumn TItem="ProductDeclarationDto" Property="TotalWeight" Title="Peso Total (Kg)" Width="130px"
                          FormatString="{0:N2}" TextAlign="TextAlign.Right" />
    <RadzenDataGridColumn TItem="ProductDeclarationDto" Property="DocState" Title="Estado" Width="110px">
        <Template Context="item">
            <RadzenBadge Text="@item.DocStateLabel" BadgeStyle="@GetDocStateBadge(item.DocState)" />
        </Template>
    </RadzenDataGridColumn>
    @* Acciones *@
</Columns>
```

---

### 10.5. Módulo Sostenibilidad

#### `IncidentList.razor` — Incidencias

```razor
<Columns>
    <RadzenDataGridColumn TItem="IncidentDto" Property="IncidentNumber" Title="Nº Incidencia" Width="130px" Frozen="true">
        <Template Context="item">
            <RadzenLink Path="@($"/incidents/{item.Id}")" Text="@item.IncidentNumber" />
        </Template>
    </RadzenDataGridColumn>
    <RadzenDataGridColumn TItem="IncidentDto" Property="Severity" Title="Gravedad" Width="110px">
        <Template Context="item">
            <RadzenBadge Text="@item.SeverityLabel" 
                         BadgeStyle="@GetSeverityBadge(item.Severity)" />
        </Template>
    </RadzenDataGridColumn>
    <RadzenDataGridColumn TItem="IncidentDto" Property="Description" Title="Descripción" Width="300px" />
    <RadzenDataGridColumn TItem="IncidentDto" Property="CreatedAt" Title="Creada" Width="120px"
                          FormatString="{0:dd/MM/yyyy}" />
    <RadzenDataGridColumn TItem="IncidentDto" Property="ClosedAt" Title="Cerrada" Width="120px"
                          FormatString="{0:dd/MM/yyyy}" />
    <RadzenDataGridColumn TItem="IncidentDto" Property="IsOpen" Title="Abierta" Width="80px" TextAlign="TextAlign.Center">
        <Template Context="item">
            <RadzenIcon Icon="@(item.IsOpen ? "error_outline" : "check_circle")"
                        Style="@($"color: {(item.IsOpen ? "var(--rz-danger)" : "var(--rz-success)")}")" />
        </Template>
    </RadzenDataGridColumn>
    @* Acciones *@
</Columns>
```

---

### 10.6. Módulo Seguridad

#### `UserList.razor` — Usuarios

```razor
<Columns>
    <RadzenDataGridColumn TItem="UserDto" Property="Login" Title="Login" Width="160px" />
    <RadzenDataGridColumn TItem="UserDto" Property="Email" Title="Email" Width="200px" />
    <RadzenDataGridColumn TItem="UserDto" Property="ProfileName" Title="Perfil" Width="140px">
        <Template Context="item">
            <RadzenBadge Text="@item.ProfileName" BadgeStyle="BadgeStyle.Info" />
        </Template>
    </RadzenDataGridColumn>
    <RadzenDataGridColumn TItem="UserDto" Property="LinkedEntityName" Title="Entidad" Width="180px" />
    <RadzenDataGridColumn TItem="UserDto" Property="IsActive" Title="Activo" Width="80px" TextAlign="TextAlign.Center">
        <Template Context="item">
            <RadzenIcon Icon="@(item.IsActive ? "check_circle" : "cancel")"
                        Style="@($"color: {(item.IsActive ? "var(--rz-success)" : "var(--rz-danger)")}")" />
        </Template>
    </RadzenDataGridColumn>
    @* Acciones *@
</Columns>
```

#### `PagePermissionList.razor` — Permisos por Página

```razor
@* Esta grid es especial — muestra todas las páginas cruzadas con los perfiles *@
@* Mantener la lógica actual pero usar RadzenDataGrid como contenedor *@
<Columns>
    <RadzenDataGridColumn TItem="PagePermissionDto" Property="PageName" Title="Página" Width="250px" Frozen="true" />
    <RadzenDataGridColumn TItem="PagePermissionDto" Property="ModuleName" Title="Módulo" Width="160px" />
    @* Una columna por cada perfil del sistema, con dropdown en template para asignar nivel *@
    @foreach (var profile in _profiles)
    {
        <RadzenDataGridColumn TItem="PagePermissionDto" Title="@profile.Description" Width="130px"
                              Sortable="false" Filterable="false" TextAlign="TextAlign.Center">
            <Template Context="item">
                <RadzenDropDown TValue="string" 
                                Data="@_permissionLevels"
                                Value="@GetPermissionLevel(item, profile.Id)"
                                Change="@(value => OnPermissionChanged(item, profile.Id, value))"
                                Style="width: 100%;" />
            </Template>
        </RadzenDataGridColumn>
    }
</Columns>
```

---

## Paso 11: Funciones auxiliares comunes para badges

Añadir estos helpers en un archivo compartido o en cada componente según convenga:

```csharp
// ══ Helpers para BadgeStyle según estado ══

private static BadgeStyle GetServiceOrderBadge(string status) => status switch
{
    "Pending" => BadgeStyle.Warning,
    "Scheduled" => BadgeStyle.Info,
    "InProgress" => BadgeStyle.Primary,
    "Completed" => BadgeStyle.Success,
    "Cancelled" => BadgeStyle.Danger,
    _ => BadgeStyle.Light
};

private static BadgeStyle GetPriorityBadge(string priority) => priority switch
{
    "Critical" => BadgeStyle.Danger,
    "High" => BadgeStyle.Warning,
    "Normal" => BadgeStyle.Info,
    "Low" => BadgeStyle.Light,
    _ => BadgeStyle.Light
};

private static BadgeStyle GetWasteMoveBadge(string status) => status switch
{
    "SOLICITADO" => BadgeStyle.Secondary,
    "PLANIFICADO" => BadgeStyle.Info,
    "RECOGIDO" => BadgeStyle.Primary,
    "EN CAC" => BadgeStyle.Warning,
    "EN PLANTA" => BadgeStyle.Dark,
    "CLASIFICADO" => BadgeStyle.Success,
    _ => BadgeStyle.Light
};

private static BadgeStyle GetDocStateBadge(string state) => state switch
{
    "Borrador" => BadgeStyle.Light,
    "Emitido" => BadgeStyle.Info,
    "Validado" => BadgeStyle.Success,
    "Rechazado" => BadgeStyle.Danger,
    _ => BadgeStyle.Light
};

private static BadgeStyle GetSeverityBadge(string severity) => severity switch
{
    "Critical" => BadgeStyle.Danger,
    "High" => BadgeStyle.Warning,
    "Medium" => BadgeStyle.Info,
    "Low" => BadgeStyle.Light,
    _ => BadgeStyle.Light
};
```

---

## Paso 12: Verificación de grids

Tras migrar cada grid, verificar:

1. **Paginación server-side**: los datos se cargan bajo demanda (no se carga todo en memoria).
2. **Sorting**: hacer clic en columna ordena correctamente (pasar `OrderBy` al handler MediatR).
3. **Filtering**: los filtros se aplican en servidor (pasar `Filter` al handler).
4. **Exportación**: los botones Export Excel/CSV generan el archivo correctamente.
5. **Permisos de escritura**: los botones de Crear/Editar/Eliminar solo aparecen si el perfil tiene nivel `Escritura` o `Ambos` en `PagePermissions`.
6. **Filtrado por datos propios**: un `PRODUCER` solo ve sus SOs, un `CARRIER` solo sus traslados, etc. (esto lo hace el handler, no la grid).
7. **Responsive**: la grid se adapta en móvil (scroll horizontal automático).
8. **Empty state**: cuando no hay datos, aparece el mensaje "No se encontraron registros".
9. **Loading state**: mientras se cargan datos, aparece el spinner de Radzen.
10. **Formatos**: fechas en `dd/MM/yyyy`, pesos con `N2`, importes con `€`.

---

## Resumen de archivos a modificar

### Parte I — Navegación (NavBar + Layout)

| Archivo | Acción |
|---------|--------|
| `Web.csproj` | Añadir `<PackageReference Include="Radzen.Blazor" />` |
| `Program.cs` | Añadir `builder.Services.AddRadzenComponents();` |
| `_Imports.razor` | Añadir `@using Radzen` y `@using Radzen.Blazor` |
| `App.razor` | Añadir `<RadzenTheme>` y el script JS |
| `MainLayout.razor` | Reescribir con `RadzenLayout/RadzenHeader/RadzenSidebar/RadzenBody` |
| `NavMenu.razor` | Reescribir con `RadzenPanelMenu/RadzenPanelMenuItem` |
| `NavMenu.razor.css` | Eliminar (Radzen gestiona los estilos) |
| `app.css` / `site.css` | Eliminar estilos custom del sidebar antiguo |
| `NavMenuStateService.cs` | Sin cambios (mantener tal cual) |

### Parte II — Grids (DataGrid)

| Archivo | Acción |
|---------|--------|
| `EntityList.razor` | Reemplazar tabla/grid por `RadzenDataGrid<EntityDto>` |
| `LerCodeList.razor` | Reemplazar por `RadzenDataGrid<LerCodeDto>` |
| `ResidueList.razor` | Reemplazar por `RadzenDataGrid<ResidueDto>` |
| `TreatmentOperationList.razor` | Reemplazar por `RadzenDataGrid<TreatmentOperationDto>` |
| `ServiceOrderList.razor` | Reemplazar por `RadzenDataGrid<ServiceOrderDto>` |
| `WasteMoveList.razor` | Reemplazar por `RadzenDataGrid<WasteMoveDto>` |
| `EntryPlantList.razor` | Reemplazar por `RadzenDataGrid<EntryPlantDto>` |
| `EntryCACList.razor` | Reemplazar por `RadzenDataGrid<EntryCACDto>` |
| `TreatmentPlantList.razor` | Reemplazar por `RadzenDataGrid<TreatmentPlantDto>` |
| `AgreementList.razor` | Reemplazar por `RadzenDataGrid<AgreementDto>` |
| `MarketShareList.razor` | Reemplazar por `RadzenDataGrid<MarketShareDto>` |
| `SettlementList.razor` | Reemplazar por `RadzenDataGrid<SettlementDto>` |
| `ProductDeclarationList.razor` | Reemplazar por `RadzenDataGrid<ProductDeclarationDto>` |
| `IncidentList.razor` | Reemplazar por `RadzenDataGrid<IncidentDto>` |
| `UserList.razor` | Reemplazar por `RadzenDataGrid<UserDto>` |
| `PagePermissionList.razor` | Reemplazar por `RadzenDataGrid<PagePermissionDto>` (grid especial con dropdowns) |
| **Query Handlers** | Adaptar para aceptar `Skip/Top/OrderBy/Filter` y devolver `PaginatedResult<T>` |
| `PaginatedResult.cs` | Crear DTO genérico si no existe |

### Dependencia adicional

| Paquete NuGet | Propósito |
|---------------|-----------|
| `System.Linq.Dynamic.Core` | Permite aplicar `OrderBy("campo asc")` y `Where("filtro")` como strings sobre IQueryable — necesario para que los parámetros de RadzenDataGrid se traduzcan a queries EF Core |

---

## Reglas importantes para Copilot

1. **NO modifiques** `IPagePermissionService`, `NavMenuStateService`, `RouteAccessGuard`, `IDataScopeService`, ni la lógica de permisos o filtrado multi-tenant. Solo cambia la capa visual.
2. **Las rutas (`Path`)** deben coincidir EXACTAMENTE con las rutas `@page` existentes en cada componente Blazor.
3. **Ajusta las rutas y nombres de DTOs** del ejemplo a los reales de tu proyecto si difieren.
4. **Los iconos** son de Material Design Icons (los que usa Radzen por defecto). Puedes cambiarlos según prefieras.
5. Si algún grupo, ruta o pantalla no existe aún en tu proyecto, **omítelo**.
6. Mantén el patrón de **carga batch de permisos** en `OnInitializedAsync` para evitar N+1 queries.
7. **Todas las grids** deben usar `LoadData` con server-side paging/sorting/filtering via MediatR — **nunca** cargar todos los registros en memoria.
8. **Cada grid** debe incluir `HeaderTemplate` con botones de acción (Nuevo, Exportar) condicionados por permisos.
9. **Los nombres de propiedades** en `Property="..."` deben coincidir exactamente con los campos del DTO de cada entidad.
10. **Formatos de fecha**: usar siempre `dd/MM/yyyy` (formato español). Formatos numéricos con `N2` para pesos y cantidades.
11. Si una grid existente usa `QuickGrid`, `<table>` HTML manual, o cualquier otro componente de tabla, **reemplazarlo completamente** por `RadzenDataGrid`.
12. Mantener las **columnas de acciones** (editar/ver/eliminar) siempre como última columna, con `Filterable="false"` y `Sortable="false"`.
13. **Todos los formularios** deben usar `RadzenTemplateForm` con controles Radzen y validadores Radzen (ver Parte III).
14. **Todos los `<input>`, `<select>`, `<textarea>`** HTML nativos y los `InputText`, `InputNumber`, `InputSelect`, `InputDate` de Blazor deben reemplazarse por sus equivalentes Radzen.

---
---

# PARTE III — Migración de todos los Formularios a Controles Radzen

---

## Paso 13: Componentes de formulario Radzen — Referencia

### 13.1. Contenedor de formulario: `RadzenTemplateForm`

Reemplaza a `EditForm` de Blazor. Proporciona validación integrada con los validadores de Radzen.

```razor
<RadzenTemplateForm TItem="MiModel" Data="@_model" Submit="@OnSubmit" InvalidSubmit="@OnInvalidSubmit">
    @* Campos del formulario aquí *@
    <RadzenButton ButtonType="ButtonType.Submit" Text="Guardar" Icon="save" 
                  ButtonStyle="ButtonStyle.Primary" />
</RadzenTemplateForm>

@code {
    private MiModel _model = new();

    private async Task OnSubmit(MiModel model)
    {
        // Enviar command via MediatR
        await Mediator.Send(new CreateMiEntidadCommand { ... });
    }

    private void OnInvalidSubmit(FormInvalidSubmitEventArgs args)
    {
        // Opcional: log o notificación de campos inválidos
    }
}
```

> **Compatibilidad**: `RadzenTemplateForm` también funciona con `DataAnnotationsValidator` de Blazor si prefieres usar `DataAnnotations` en los modelos. Puedes mezclar `RadzenRequiredValidator` con `[Required]` según conveniencia.

### 13.2. Contenedor de campo: `RadzenFormField`

Envuelve cada input con label flotante Material Design, iconos y mensajes de validación:

```razor
<RadzenFormField Text="Nombre" Variant="Variant.Outlined" Style="width: 100%;">
    <ChildContent>
        <RadzenTextBox Name="Name" @bind-Value="@_model.Name" Style="width: 100%;" />
    </ChildContent>
    <Helper>
        <RadzenRequiredValidator Component="Name" Text="El nombre es obligatorio" />
    </Helper>
</RadzenFormField>
```

**Variantes de `RadzenFormField`**: `Variant.Outlined` (borde), `Variant.Filled` (fondo), `Variant.Flat` (sin borde). Usar **`Variant.Outlined`** como estándar en toda la aplicación para consistencia.

### 13.3. Tabla de equivalencias — Controles HTML/Blazor → Radzen

| Control actual | Reemplazar por | Propiedades principales |
|----------------|---------------|------------------------|
| `<input type="text">` / `InputText` | **`RadzenTextBox`** | `@bind-Value`, `Name`, `Placeholder`, `MaxLength`, `Disabled`, `ReadOnly` |
| `<textarea>` / `InputTextArea` | **`RadzenTextArea`** | `@bind-Value`, `Name`, `Rows`, `MaxLength` |
| `<input type="number">` / `InputNumber` | **`RadzenNumeric<T>`** | `@bind-Value`, `Name`, `Min`, `Max`, `Step`, `Format` (ej: `"0.00"`), `Placeholder` |
| `<input type="date">` / `InputDate` | **`RadzenDatePicker`** | `@bind-Value`, `Name`, `DateFormat` (ej: `"dd/MM/yyyy"`), `ShowTime`, `HourFormat` (`"24"`), `Min`, `Max` |
| `<input type="datetime-local">` | **`RadzenDatePicker`** | Igual + `ShowTime="true"`, `DateFormat="dd/MM/yyyy HH:mm"` |
| `<select>` / `InputSelect` | **`RadzenDropDown<T>`** | `@bind-Value`, `Name`, `Data`, `TextProperty`, `ValueProperty`, `Placeholder`, `AllowClear`, `AllowFiltering`, `FilterCaseSensitivity` |
| `<select multiple>` | **`RadzenDropDown<IEnumerable<T>>`** | Igual + `Multiple="true"`, `Chips="true"` |
| `<input type="checkbox">` / `InputCheckbox` | **`RadzenCheckBox<bool>`** | `@bind-Value`, `Name` |
| Toggle / switch | **`RadzenSwitch`** | `@bind-Value`, `Name` |
| Radio buttons | **`RadzenRadioButtonList<T>`** | `@bind-Value`, `Name`, `Data`, `TextProperty`, `ValueProperty`, `Orientation` |
| Segmented button / tabs de selección | **`RadzenSelectBar<T>`** | `@bind-Value`, `Name`, `Items` (con `RadzenSelectBarItem`) |
| Autocomplete / búsqueda con sugerencias | **`RadzenAutoComplete`** | `@bind-Value`, `Data`, `TextProperty`, `MinLength`, `FilterDelay`, `FilterOperator` |
| Dropdown con búsqueda + grid | **`RadzenDropDownDataGrid<T>`** | `@bind-Value`, `Data`, `TextProperty`, `ValueProperty`, `AllowFiltering`, `Columns` |
| Selector de color | **`RadzenColorPicker`** | `@bind-Value`, `ShowHSV`, `ShowRGBA` |
| Subida de ficheros | **`RadzenUpload`** | `Url`, `Accept`, `MaxFileSize`, `Complete`, `Error` |
| Máscara de input | **`RadzenMask`** | `@bind-Value`, `Mask` (ej: `"000-000-000"` para NIF) |
| Slider / rango | **`RadzenSlider<T>`** | `@bind-Value`, `Min`, `Max`, `Step` |
| Password | **`RadzenPassword`** | `@bind-Value`, `Name` |
| Label | **`RadzenLabel`** | `Text`, `Component` (para `for`) |
| Fieldset / agrupador | **`RadzenFieldset`** | `Text` (título del grupo), `AllowCollapse` |

### 13.4. Validadores Radzen

Todos se colocan **dentro** de `RadzenTemplateForm` y se vinculan al input via `Component="NombreDelInput"`:

| Validador | Propósito | Ejemplo |
|-----------|-----------|---------|
| `RadzenRequiredValidator` | Campo obligatorio | `<RadzenRequiredValidator Component="Name" Text="Obligatorio" />` |
| `RadzenEmailValidator` | Formato email | `<RadzenEmailValidator Component="Email" Text="Email inválido" />` |
| `RadzenLengthValidator` | Longitud mín/máx | `<RadzenLengthValidator Component="NIF" Min="9" Max="9" Text="NIF debe tener 9 caracteres" />` |
| `RadzenNumericRangeValidator` | Rango numérico | `<RadzenNumericRangeValidator Component="Weight" Min="0.01" Text="El peso debe ser mayor que 0" />` |
| `RadzenCompareValidator` | Comparar dos campos | `<RadzenCompareValidator Component="ConfirmPassword" Value="@_model.Password" Text="No coinciden" />` |
| `RadzenRegexValidator` | Expresión regular | `<RadzenRegexValidator Component="CenterCode" Pattern="^ES\d{10}$" Text="Formato NIMA inválido" />` |
| `RadzenCustomValidator` | Validación custom | `<RadzenCustomValidator Component="Year" Validator="@ValidateYear" Text="Año fuera de rango" />` |
| `RadzenDataAnnotationValidator` | Data Annotations del modelo | `<RadzenDataAnnotationValidator />` (sin Component, valida todo) |

> **Regla**: cada campo obligatorio del formulario debe tener al menos un `RadzenRequiredValidator` dentro de su `<Helper>` en el `RadzenFormField`. Para DefaultValue en numéricos, usar `DefaultValue="0"`.

---

## Paso 14: Layout de formularios con RadzenRow/RadzenColumn

Reemplaza las `<div class="row">` / `<div class="col-md-6">` de Bootstrap por componentes Radzen para layout responsive:

```razor
<RadzenRow Gap="1rem">
    <RadzenColumn Size="12" SizeMD="6">
        <RadzenFormField Text="Nombre" Variant="Variant.Outlined" Style="width: 100%;">
            <ChildContent>
                <RadzenTextBox Name="Name" @bind-Value="@_model.Name" Style="width: 100%;" />
            </ChildContent>
            <Helper>
                <RadzenRequiredValidator Component="Name" Text="El nombre es obligatorio" />
            </Helper>
        </RadzenFormField>
    </RadzenColumn>
    <RadzenColumn Size="12" SizeMD="6">
        <RadzenFormField Text="NIF/CIF" Variant="Variant.Outlined" Style="width: 100%;">
            <ChildContent>
                <RadzenTextBox Name="NationalId" @bind-Value="@_model.NationalId" Style="width: 100%;" />
            </ChildContent>
            <Helper>
                <RadzenRequiredValidator Component="NationalId" Text="El NIF/CIF es obligatorio" />
            </Helper>
        </RadzenFormField>
    </RadzenColumn>
</RadzenRow>
```

**Propiedades de `RadzenColumn`**: `Size` (12 col base), `SizeSM`, `SizeMD`, `SizeLG`, `SizeXL` para responsive breakpoints.

---

## Paso 15: Migración formulario por formulario

Copilot debe aplicar el patrón anterior a **cada formulario** de la aplicación. A continuación se detalla cada uno con sus campos y el control Radzen apropiado.

### 15.1. `EntityForm.razor` — Formulario de Entidades

```razor
<RadzenTemplateForm TItem="EntityFormModel" Data="@_model" Submit="@OnSubmit">
    <RadzenFieldset Text="Identificación">
        <RadzenRow Gap="1rem">
            <RadzenColumn SizeMD="6">
                <RadzenFormField Text="Nombre" Variant="Variant.Outlined" Style="width: 100%;">
                    <ChildContent>
                        <RadzenTextBox Name="Name" @bind-Value="@_model.Name" MaxLength="256" Style="width: 100%;" />
                    </ChildContent>
                    <Helper><RadzenRequiredValidator Component="Name" Text="Obligatorio" /></Helper>
                </RadzenFormField>
            </RadzenColumn>
            <RadzenColumn SizeMD="3">
                <RadzenFormField Text="NIF/CIF" Variant="Variant.Outlined" Style="width: 100%;">
                    <ChildContent>
                        <RadzenTextBox Name="NationalId" @bind-Value="@_model.NationalId" MaxLength="32" Style="width: 100%;" />
                    </ChildContent>
                    <Helper><RadzenRequiredValidator Component="NationalId" Text="Obligatorio" /></Helper>
                </RadzenFormField>
            </RadzenColumn>
            <RadzenColumn SizeMD="3">
                <RadzenFormField Text="NIMA" Variant="Variant.Outlined" Style="width: 100%;">
                    <ChildContent>
                        <RadzenTextBox Name="CenterCode" @bind-Value="@_model.CenterCode" MaxLength="64" Style="width: 100%;" />
                    </ChildContent>
                </RadzenFormField>
            </RadzenColumn>
        </RadzenRow>
        <RadzenRow Gap="1rem" class="rz-mt-4">
            <RadzenColumn SizeMD="4">
                <RadzenFormField Text="Rol" Variant="Variant.Outlined" Style="width: 100%;">
                    <ChildContent>
                        <RadzenDropDown TValue="string" Name="EntityRole" @bind-Value="@_model.EntityRole"
                                        Data="@_entityRoles" Style="width: 100%;"
                                        Placeholder="Seleccione un rol..." AllowClear="true" />
                    </ChildContent>
                    <Helper><RadzenRequiredValidator Component="EntityRole" Text="Obligatorio" /></Helper>
                </RadzenFormField>
            </RadzenColumn>
            <RadzenColumn SizeMD="4">
                <RadzenFormField Text="Tipo de tercero" Variant="Variant.Outlined" Style="width: 100%;">
                    <ChildContent>
                        <RadzenDropDown TValue="string" Name="TypeThirdParty" @bind-Value="@_model.TypeThirdParty"
                                        Data="@_thirdPartyTypes" Style="width: 100%;" AllowClear="true" />
                    </ChildContent>
                </RadzenFormField>
            </RadzenColumn>
            <RadzenColumn SizeMD="4">
                <RadzenFormField Text="Actividad económica" Variant="Variant.Outlined" Style="width: 100%;">
                    <ChildContent>
                        <RadzenTextBox Name="EconomicActivity" @bind-Value="@_model.EconomicActivity" Style="width: 100%;" />
                    </ChildContent>
                </RadzenFormField>
            </RadzenColumn>
        </RadzenRow>
    </RadzenFieldset>

    <RadzenFieldset Text="Localización" class="rz-mt-4">
        <RadzenRow Gap="1rem">
            <RadzenColumn SizeMD="6">
                <RadzenFormField Text="Dirección" Variant="Variant.Outlined" Style="width: 100%;">
                    <ChildContent>
                        <RadzenTextBox Name="Address" @bind-Value="@_model.Address" Style="width: 100%;" />
                    </ChildContent>
                </RadzenFormField>
            </RadzenColumn>
            <RadzenColumn SizeMD="3">
                <RadzenFormField Text="Código Postal" Variant="Variant.Outlined" Style="width: 100%;">
                    <ChildContent>
                        <RadzenTextBox Name="ZipCode" @bind-Value="@_model.ZipCode" MaxLength="10" Style="width: 100%;" />
                    </ChildContent>
                </RadzenFormField>
            </RadzenColumn>
            <RadzenColumn SizeMD="3">
                <RadzenFormField Text="País" Variant="Variant.Outlined" Style="width: 100%;">
                    <ChildContent>
                        <RadzenDropDown TValue="string" Name="CountryCode" @bind-Value="@_model.CountryCode"
                                        Data="@_countries" TextProperty="Name" ValueProperty="Code"
                                        AllowFiltering="true" FilterCaseSensitivity="FilterCaseSensitivity.CaseInsensitive"
                                        Style="width: 100%;" />
                    </ChildContent>
                </RadzenFormField>
            </RadzenColumn>
        </RadzenRow>
        <RadzenRow Gap="1rem" class="rz-mt-4">
            <RadzenColumn SizeMD="4">
                <RadzenFormField Text="Comunidad Autónoma" Variant="Variant.Outlined" Style="width: 100%;">
                    <ChildContent>
                        <RadzenDropDown TValue="string" Name="StateCode" @bind-Value="@_model.StateCode"
                                        Data="@_autonomousCommunities" TextProperty="Name" ValueProperty="Code"
                                        AllowFiltering="true" Style="width: 100%;" Change="@OnStateChanged" />
                    </ChildContent>
                </RadzenFormField>
            </RadzenColumn>
            <RadzenColumn SizeMD="4">
                <RadzenFormField Text="Provincia" Variant="Variant.Outlined" Style="width: 100%;">
                    <ChildContent>
                        <RadzenDropDown TValue="string" Name="ProvinceCode" @bind-Value="@_model.ProvinceCode"
                                        Data="@_provinces" TextProperty="Name" ValueProperty="Code"
                                        AllowFiltering="true" Style="width: 100%;" Change="@OnProvinceChanged" />
                    </ChildContent>
                </RadzenFormField>
            </RadzenColumn>
            <RadzenColumn SizeMD="4">
                <RadzenFormField Text="Municipio" Variant="Variant.Outlined" Style="width: 100%;">
                    <ChildContent>
                        <RadzenDropDown TValue="string" Name="MunicipalityCode" @bind-Value="@_model.MunicipalityCode"
                                        Data="@_municipalities" TextProperty="Name" ValueProperty="Code"
                                        AllowFiltering="true" Style="width: 100%;" />
                    </ChildContent>
                </RadzenFormField>
            </RadzenColumn>
        </RadzenRow>
        <RadzenRow Gap="1rem" class="rz-mt-4">
            <RadzenColumn SizeMD="3">
                <RadzenFormField Text="Latitud" Variant="Variant.Outlined" Style="width: 100%;">
                    <ChildContent>
                        <RadzenNumeric TValue="decimal?" Name="Latitude" @bind-Value="@_model.Latitude"
                                       Format="0.000000" Style="width: 100%;" />
                    </ChildContent>
                </RadzenFormField>
            </RadzenColumn>
            <RadzenColumn SizeMD="3">
                <RadzenFormField Text="Longitud" Variant="Variant.Outlined" Style="width: 100%;">
                    <ChildContent>
                        <RadzenNumeric TValue="decimal?" Name="Longitude" @bind-Value="@_model.Longitude"
                                       Format="0.000000" Style="width: 100%;" />
                    </ChildContent>
                </RadzenFormField>
            </RadzenColumn>
        </RadzenRow>
    </RadzenFieldset>

    <RadzenFieldset Text="Contacto" class="rz-mt-4">
        <RadzenRow Gap="1rem">
            <RadzenColumn SizeMD="4">
                <RadzenFormField Text="Teléfono" Variant="Variant.Outlined" Style="width: 100%;">
                    <Start><RadzenIcon Icon="phone" /></Start>
                    <ChildContent>
                        <RadzenTextBox Name="PhoneNumber" @bind-Value="@_model.PhoneNumber" Style="width: 100%;" />
                    </ChildContent>
                </RadzenFormField>
            </RadzenColumn>
            <RadzenColumn SizeMD="4">
                <RadzenFormField Text="Email" Variant="Variant.Outlined" Style="width: 100%;">
                    <Start><RadzenIcon Icon="email" /></Start>
                    <ChildContent>
                        <RadzenTextBox Name="Email" @bind-Value="@_model.Email" Style="width: 100%;" />
                    </ChildContent>
                    <Helper><RadzenEmailValidator Component="Email" Text="Email inválido" /></Helper>
                </RadzenFormField>
            </RadzenColumn>
            <RadzenColumn SizeMD="4">
                <RadzenFormField Text="Persona de contacto" Variant="Variant.Outlined" Style="width: 100%;">
                    <ChildContent>
                        <RadzenTextBox Name="ContactPerson" @bind-Value="@_model.ContactPerson" Style="width: 100%;" />
                    </ChildContent>
                </RadzenFormField>
            </RadzenColumn>
        </RadzenRow>
    </RadzenFieldset>

    <RadzenRow class="rz-mt-4" JustifyContent="JustifyContent.End" Gap="0.5rem">
        <RadzenColumn Size="12" class="rz-text-align-end">
            <RadzenFormField Text="Activo" Variant="Variant.Flat">
                <ChildContent>
                    <RadzenSwitch Name="IsActive" @bind-Value="@_model.IsActive" />
                </ChildContent>
            </RadzenFormField>
            <RadzenButton Text="Cancelar" ButtonStyle="ButtonStyle.Light" Icon="cancel" 
                          Click="@OnCancel" class="rz-mr-2" />
            <RadzenButton ButtonType="ButtonType.Submit" Text="Guardar" Icon="save" 
                          ButtonStyle="ButtonStyle.Primary" />
        </RadzenColumn>
    </RadzenRow>
</RadzenTemplateForm>
```

### 15.2. Resumen de formularios restantes

Copilot debe aplicar **el mismo patrón** (RadzenTemplateForm + RadzenFormField + RadzenRow/Column + validadores) a todos los demás formularios. A continuación el mapeo de campos → control Radzen para cada uno:

#### `ServiceOrderForm.razor` — Orden de Servicio

| Campo | Control Radzen | Notas |
|-------|---------------|-------|
| Emisor (`IdIssuedBy`) | `RadzenDropDown<Guid>` con `AllowFiltering` | `ReadOnly` para PRODUCER/PUBLIC_ENT (autocompletado con `LinkedEntityId`) |
| Punto de recogida (`IdPickupPoint`) | `RadzenDropDown<Guid>` con `AllowFiltering` | Filtrado por `EntityRole ∈ {CAC, PublicEntity, Producer, OperatorTransfer}` |
| Código LER (`IdLERCode`) | `RadzenDropDownDataGrid<Guid>` | Con columnas Code + Description para búsqueda rica |
| Flujo (`WasteStream`) | `RadzenDropDown<string>` | Datos de `WasteFlowCatalog`, agrupados por RAP/Operativos |
| Sub-flujo (`SubStream`) | `RadzenDropDown<string>` | Dependiente de `WasteStream`, `Disabled` hasta seleccionar flujo |
| Prioridad (`Priority`) | `RadzenSelectBar<string>` | Con `RadzenSelectBarItem` para Low/Normal/High/Critical con iconos |
| Peso estimado (`EstimatedWeight`) | `RadzenNumeric<decimal>` | `Format="0.00"`, `Min="0"`, placeholder "Kg" |
| Ventana de recogida planificada | `RadzenDatePicker` × 2 | `ShowTime="true"`, `DateFormat="dd/MM/yyyy HH:mm"`, `HourFormat="24"` |
| Ventana de entrega planificada | `RadzenDatePicker` × 2 | Igual |
| Transportista previsto (`IdCarrier`) | `RadzenDropDown<Guid?>` con `AllowFiltering` | Filtrado por `EntityRole = Carrier` |
| Planta prevista (`IdPlannedPlant`) | `RadzenDropDown<Guid?>` con `AllowFiltering` | Filtrado por `EntityRole = Plant` |
| Contenedores (`ContainersJson`) | `RadzenDropDown<string>` + `RadzenNumeric<int>` | Dropdown cerrado: Bigbag/Contenedor/Palé/Granel/Otro + campo numérico cantidad |
| Notas | `RadzenTextArea` | `Rows="3"` |
| **Líneas de residuo** (`ServiceOrderResidues`) | `RadzenDataGrid` inline editable | Sub-grid con columnas: LER, Peso, Unidades. Botón "Añadir línea" |

#### `WasteMoveForm.razor` — Traslado

| Campo | Control Radzen | Notas |
|-------|---------------|-------|
| SO vinculada (`ServiceOrderId`) | `RadzenDropDownDataGrid<Guid>` | Búsqueda por `ServiceOrderNumber` |
| Origen (`IdSource`) | `RadzenDropDown<Guid>` con `AllowFiltering` | Filtrado por roles válidos |
| Destino (`IdDestination`) | `RadzenDropDown<Guid>` con `AllowFiltering` | Filtrado por roles válidos |
| SCRAP (`IdScrap`) | `RadzenDropDown<Guid?>` con `AllowFiltering` | |
| SCRAP 2 (`IdScrap2`) | `RadzenDropDown<Guid?>` con `AllowFiltering` | Visible solo si aplica doble SCRAP |
| Operador de traslado | `RadzenDropDown<Guid?>` con `AllowFiltering` | Filtrado por `EntityRole = OperatorTransfer` |
| Fecha recogida planificada | `RadzenDatePicker` × 2 | `ShowTime="true"` |
| Fecha entrega planificada | `RadzenDatePicker` × 2 | `ShowTime="true"` |
| Estado (`ServiceStatus`) | `RadzenBadge` (solo lectura) | No editable desde el form, se cambia por máquina de estados |
| **Líneas residuo** (`WasteMoveResidues`) | `RadzenDataGrid` inline editable | Columnas: Residuo, Peso, Unidades, PrecioPorKg, OpTratamiento destino |

#### `EntryPlantForm.razor` — Entrada en Planta

| Campo | Control Radzen |
|-------|---------------|
| Ticket báscula | `RadzenTextBox` |
| Planta | `RadzenDropDown<Guid>` (filtrado `EntityRole = Plant`) |
| Fecha entrada | `RadzenDatePicker` con `ShowTime="true"` |
| Peso bruto / Tara / Neto | `RadzenNumeric<decimal>` × 3, `Format="0.00"` |
| Traslado vinculado | `RadzenDropDownDataGrid<Guid?>` |
| **Líneas residuo** (`EntryPlantResidues`) | `RadzenDataGrid` inline editable |

#### `EntryCACForm.razor` — Entrada en CAC

Mismo patrón que `EntryPlantForm` pero con campos de CAC.

#### `TreatmentPlantForm.razor` — Tratamiento

| Campo | Control Radzen |
|-------|---------------|
| Planta | `RadzenDropDown<Guid>` |
| Fecha tratamiento | `RadzenDatePicker` |
| Operación R/D | `RadzenDropDown<Guid>` (datos de `TreatmentOperations`) |
| **Líneas tratamiento** (`TreatmentPlantResidues`) | `RadzenDataGrid` inline con `WeightTotal`, `WeightReused`, `WeightValued`, `WeightRejected` |

#### `AgreementForm.razor` (Wizard) — Convenio

| Paso del wizard | Campos → Control Radzen |
|----------------|------------------------|
| Partes | SCRAP, Entidad Pública, Coordinador → `RadzenDropDown<Guid>` × 3 con `AllowFiltering` |
| Ámbito | `WasteStream`/`SubStream` → `RadzenDropDown`, CCAA/Provincia/Municipio → cascada de `RadzenDropDown` |
| Vigencia | `EffectiveFrom`/`EffectiveTo` → `RadzenDatePicker` × 2 |
| Tarifas | JSON editor o campos dinámicos con `RadzenNumeric` |
| Documento | `RadzenUpload` para adjuntar contrato |

> **Nota wizard**: Usar `RadzenSteps` con `RadzenStepsItem` para implementar el wizard multi-paso.

#### `IncidentForm.razor` — Incidencia

| Campo | Control Radzen |
|-------|---------------|
| Gravedad | `RadzenSelectBar<string>` con items Low/Medium/High/Critical |
| Descripción | `RadzenTextArea` con `Rows="5"` |
| Traslado vinculado | `RadzenDropDownDataGrid<Guid?>` |
| Fecha | `RadzenDatePicker` (readonly, automático) |

#### `ProductDeclarationForm.razor` — Declaración de Producto (Wizard 2 pasos)

| Paso | Campos → Control Radzen |
|------|------------------------|
| Cabecera | Productor → `RadzenDropDown<Guid>` (readonly para PRODUCER), Año → `RadzenNumeric<int>`, Periodo → `RadzenDropDown`, Tipo → `RadzenDropDown`, Moneda → `RadzenDropDown`, Referencia → `RadzenTextBox` |
| Líneas | `RadzenDataGrid` inline editable: Producto → `RadzenDropDownDataGrid`, Cantidad → `RadzenNumeric`, Unidad → `RadzenDropDown`, Fuente → `RadzenDropDown` |

#### `MarketShareForm.razor` — Cuota de Mercado

| Campo | Control Radzen |
|-------|---------------|
| SCRAP | `RadzenDropDown<Guid>` |
| Año | `RadzenNumeric<int>` con `Min="2020"` `Max="2030"` |
| Periodo | `RadzenDropDown<string>` (T1-T4, Anual) |
| Flujo | `RadzenDropDown<string>` |
| Categoría | `RadzenDropDown<string>` (dependiente del flujo) |
| Cuota (Kg) | `RadzenNumeric<decimal>` con `Format="0.00"` |

#### `UserForm.razor` — Usuarios

| Campo | Control Radzen |
|-------|---------------|
| Login | `RadzenTextBox` (readonly en edición) |
| Email | `RadzenTextBox` + `RadzenEmailValidator` |
| Perfil (`IdProfile`) | `RadzenDropDown<Guid>` |
| Entidad vinculada | `RadzenDropDown<Guid?>` con `AllowFiltering` |
| Activo | `RadzenSwitch` |

#### `SettlementForm.razor` — Liquidación

| Campo | Control Radzen |
|-------|---------------|
| SCRAP | `RadzenDropDown<Guid>` |
| Periodo | `RadzenDropDown<string>` |
| Referencia | `RadzenTextBox` |
| Importe total | `RadzenNumeric<decimal>` con `Format="0.00"` |
| Estado | `RadzenBadge` (solo lectura, se cambia por workflow) |
| **Líneas** (`SettlementLines`) | `RadzenDataGrid` inline editable |

---

## Paso 16: Patrón para selectores de Entidades con búsqueda

Muchos formularios usan selectores de entidades (Emisor, Origen, Destino, SCRAP, etc.). El patrón estándar es:

```razor
<RadzenFormField Text="Origen" Variant="Variant.Outlined" Style="width: 100%;">
    <ChildContent>
        <RadzenDropDown TValue="Guid?" Name="IdSource" @bind-Value="@_model.IdSource"
                        Data="@_sourceEntities"
                        TextProperty="DisplayName"
                        ValueProperty="Id"
                        AllowFiltering="true"
                        FilterCaseSensitivity="FilterCaseSensitivity.CaseInsensitive"
                        FilterOperator="StringFilterOperator.Contains"
                        AllowClear="true"
                        Placeholder="Buscar entidad..."
                        Style="width: 100%;"
                        LoadData="@OnLoadSourceEntities">
            <Template Context="entity">
                <RadzenStack Orientation="Orientation.Horizontal" AlignItems="AlignItems.Center" Gap="0.5rem">
                    <RadzenBadge Text="@entity.EntityRole" BadgeStyle="BadgeStyle.Info" />
                    <span>@entity.DisplayName</span>
                    <span style="color: var(--rz-text-disabled-color); font-size: 0.85em;">@entity.NationalId</span>
                </RadzenStack>
            </Template>
        </RadzenDropDown>
    </ChildContent>
    <Helper><RadzenRequiredValidator Component="IdSource" Text="Obligatorio" /></Helper>
</RadzenFormField>
```

> Para selectores de entidad con muchos registros (>500), usar `RadzenDropDownDataGrid<Guid>` con columnas visibles (Nombre, NIF, NIMA, Rol) y paginación server-side via `LoadData`.

---

## Paso 17: Patrón para selectores cascada (WasteStream → SubStream)

```razor
@* ── Flujo de residuo ── *@
<RadzenFormField Text="Flujo de residuo" Variant="Variant.Outlined" Style="width: 100%;">
    <ChildContent>
        <RadzenDropDown TValue="string" Name="WasteStream" @bind-Value="@_model.WasteStream"
                        Data="@_wasteStreams" TextProperty="Name" ValueProperty="Code"
                        Placeholder="Seleccione flujo..." Style="width: 100%;"
                        Change="@OnWasteStreamChanged" />
    </ChildContent>
    <Helper><RadzenRequiredValidator Component="WasteStream" Text="Obligatorio" /></Helper>
</RadzenFormField>

@* ── Sub-flujo (dependiente) ── *@
<RadzenFormField Text="Sub-flujo" Variant="Variant.Outlined" Style="width: 100%;">
    <ChildContent>
        <RadzenDropDown TValue="string" Name="SubStream" @bind-Value="@_model.SubStream"
                        Data="@_subStreams" TextProperty="Name" ValueProperty="Code"
                        Disabled="@(string.IsNullOrEmpty(_model.WasteStream))"
                        Placeholder="Seleccione sub-flujo..." Style="width: 100%;" />
    </ChildContent>
</RadzenFormField>

@code {
    private void OnWasteStreamChanged(object value)
    {
        _model.SubStream = null;  // Limpiar sub-flujo
        _subStreams = WasteFlowCatalog.GetSubStreams(_model.WasteStream);
    }
}
```

---

## Paso 18: Verificación de formularios

Tras migrar cada formulario, verificar:

1. **Validación**: al pulsar Guardar sin rellenar campos obligatorios, los validadores Radzen muestran los mensajes de error bajo cada campo.
2. **Binding bidireccional**: los valores se mantienen al navegar entre campos.
3. **Selectores cascada**: al cambiar un padre (ej: WasteStream), el hijo se limpia y recarga.
4. **ReadOnly/Disabled**: los campos que dependen del perfil (ej: `IdIssuedBy` para PRODUCER) se muestran correctamente como solo lectura.
5. **Layout responsive**: los campos se reorganizan de 2-3 columnas en desktop a 1 columna en móvil.
6. **Consistent variant**: todos los `RadzenFormField` usan `Variant.Outlined`.
7. **Submit**: el formulario envía el command MediatR correctamente.
8. **Botón Cancelar**: navega al listado sin guardar cambios.
9. **Modo edición vs creación**: en edición se precargan los valores, en creación los defaults.
10. **Grids inline**: las sub-tablas dentro de formularios (líneas de residuo, líneas de producto, etc.) usan `RadzenDataGrid` con edición inline.

---

## Resumen final de archivos — Parte III (Formularios)

| Archivo | Acción |
|---------|--------|
| `EntityForm.razor` | Reescribir con `RadzenTemplateForm` + controles Radzen |
| `ServiceOrderForm.razor` | Reescribir con controles Radzen + sub-grid inline para `ServiceOrderResidues` |
| `WasteMoveForm.razor` | Reescribir con controles Radzen + sub-grid inline para `WasteMoveResidues` |
| `EntryPlantForm.razor` | Reescribir con controles Radzen + sub-grid inline |
| `EntryCACForm.razor` | Reescribir con controles Radzen + sub-grid inline |
| `TreatmentPlantForm.razor` | Reescribir con controles Radzen + sub-grid inline |
| `AgreementForm.razor` (Wizard) | Reescribir con `RadzenSteps` + controles Radzen por paso |
| `IncidentForm.razor` | Reescribir con controles Radzen |
| `ProductDeclarationForm.razor` | Reescribir wizard 2 pasos + `RadzenDataGrid` inline para líneas |
| `MarketShareForm.razor` | Reescribir con controles Radzen |
| `SettlementForm.razor` | Reescribir con controles Radzen + sub-grid inline para líneas |
| `UserForm.razor` | Reescribir con controles Radzen |
| `PagePermissionForm.razor` | Si existe, reescribir con controles Radzen |
| Cualquier otro `*Form.razor` | Aplicar el mismo patrón |
