# Prompt para GitHub Copilot — Gestión de Permisos por Pantalla en GreenTransit

> **Contexto del proyecto**: GreenTransit es una plataforma web multi-rol, multi-tenant (`OwnerId`) construida con **Blazor Server (.NET 10)**, **CQRS (MediatR)**, **EF Core**, **SQL Server Azure**, **FluentValidation** y **Serilog**. El modelo de datos es el v4.1. La autenticación es por **OpenID Connect** (Authority externa). La autorización se basa en `Profiles` (tabla `Profiles` con `ID int IDENTITY` + `Reference` + `Description`) asignados a cada `Users.IdProfile`. Las policies se definen en `PolicyConstants` y se evalúan contra el perfil del usuario vía `ICurrentUserService`. El componente `ProfileAuthorizeView` controla la visibilidad de botones según perfil. Arquitectura Clean Architecture: `Domain`, `Application`, `Infrastructure`, `Web`, `Tests`.

---

## 🎯 Objetivo

Crear una nueva pantalla de **Gestión de Permisos por Pantalla** (`/security/page-permissions`) dentro del módulo de **Seguridad**, ubicada en el menú lateral **debajo de "Perfiles"** (`/profiles`). Esta pantalla permite al administrador ver todas las pantallas/rutas de la aplicación y configurar qué perfiles tienen acceso a cada una, indicando si el acceso es de **Lectura**, **Escritura** o **Ambos**.

**Requisito clave**: cuando se añada una nueva pantalla/ruta al sistema (por ejemplo, un nuevo `.razor` con `@page`), debe aparecer **automáticamente** en esta pantalla para que el administrador le asigne permisos.

---

## 📊 Modelo de datos

### Tabla nueva: `PageDefinitions`

Catálogo auto-descubierto de todas las pantallas de la aplicación.

```sql
CREATE TABLE PageDefinitions (
    ID              INT IDENTITY(1,1) PRIMARY KEY,
    Route           NVARCHAR(256)  NOT NULL,          -- Ej: '/service-orders', '/users'
    PageName        NVARCHAR(256)  NOT NULL,          -- Ej: 'Órdenes de Servicio', 'Usuarios'
    ModuleName      NVARCHAR(128)  NOT NULL,          -- Ej: 'Operaciones', 'Seguridad', 'Configuración'
    ComponentName   NVARCHAR(256)  NULL,              -- Ej: 'ServiceOrderList' (nombre del componente Blazor)
    IsActive        BIT            NOT NULL DEFAULT 1,-- Para ocultar pantallas obsoletas sin borrar
    SortOrder       INT            NOT NULL DEFAULT 0,-- Orden dentro del módulo
    CreatedAt       DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2      NULL,
    CONSTRAINT UQ_PageDefinitions_Route UNIQUE (Route)
);
```

### Tabla nueva: `PagePermissions`

Relación N:M entre pantallas y perfiles con nivel de acceso.

```sql
CREATE TABLE PagePermissions (
    ID              INT IDENTITY(1,1) PRIMARY KEY,
    IdPageDefinition INT           NOT NULL,
    IdProfile       INT            NOT NULL,
    AccessLevel     NVARCHAR(16)   NOT NULL,           -- 'Read', 'Write', 'ReadWrite'
    CreatedAt       DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2      NULL,
    IdUser          INT            NULL,                -- Quién configuró el permiso
    CONSTRAINT FK_PagePermissions_PageDefinition FOREIGN KEY (IdPageDefinition) REFERENCES PageDefinitions(ID),
    CONSTRAINT FK_PagePermissions_Profile FOREIGN KEY (IdProfile) REFERENCES Profiles(ID),
    CONSTRAINT UQ_PagePermissions_Page_Profile UNIQUE (IdPageDefinition, IdProfile),
    CONSTRAINT CK_PagePermissions_AccessLevel CHECK (AccessLevel IN ('Read', 'Write', 'ReadWrite'))
);
```

### Entidades de dominio

Crear en `Domain/Entities/`:

```csharp
// Domain/Entities/PageDefinition.cs
public class PageDefinition
{
    public int ID { get; set; }
    public string Route { get; set; } = string.Empty;
    public string PageName { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public string? ComponentName { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navegación
    public ICollection<PagePermission> Permissions { get; set; } = new List<PagePermission>();
}

// Domain/Entities/PagePermission.cs
public class PagePermission
{
    public int ID { get; set; }
    public int IdPageDefinition { get; set; }
    public int IdProfile { get; set; }
    public string AccessLevel { get; set; } = "Read"; // "Read" | "Write" | "ReadWrite"
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? IdUser { get; set; }

    // Navegación
    public PageDefinition PageDefinition { get; set; } = null!;
    public Profile Profile { get; set; } = null!;
}
```

### Enum de dominio

```csharp
// Domain/Enums/AccessLevel.cs
public enum AccessLevel
{
    Read,
    Write,
    ReadWrite
}
```

### Configuración EF Core

Añadir en `Infrastructure/Persistence/Configurations/`:

```csharp
// PageDefinitionConfiguration.cs
public class PageDefinitionConfiguration : IEntityTypeConfiguration<PageDefinition>
{
    public void Configure(EntityTypeBuilder<PageDefinition> builder)
    {
        builder.ToTable("PageDefinitions");
        builder.HasKey(e => e.ID);
        builder.Property(e => e.Route).HasMaxLength(256).IsRequired();
        builder.Property(e => e.PageName).HasMaxLength(256).IsRequired();
        builder.Property(e => e.ModuleName).HasMaxLength(128).IsRequired();
        builder.Property(e => e.ComponentName).HasMaxLength(256);
        builder.Property(e => e.AccessLevel).HasMaxLength(16).IsRequired();
        builder.HasIndex(e => e.Route).IsUnique();
        builder.HasMany(e => e.Permissions).WithOne(p => p.PageDefinition).HasForeignKey(p => p.IdPageDefinition);
    }
}

// PagePermissionConfiguration.cs
public class PagePermissionConfiguration : IEntityTypeConfiguration<PagePermission>
{
    public void Configure(EntityTypeBuilder<PagePermission> builder)
    {
        builder.ToTable("PagePermissions");
        builder.HasKey(e => e.ID);
        builder.Property(e => e.AccessLevel).HasMaxLength(16).IsRequired();
        builder.HasIndex(e => new { e.IdPageDefinition, e.IdProfile }).IsUnique();
        builder.HasOne(e => e.PageDefinition).WithMany(p => p.Permissions).HasForeignKey(e => e.IdPageDefinition);
        builder.HasOne(e => e.Profile).WithMany().HasForeignKey(e => e.IdProfile);
    }
}
```

Registrar ambas en `AppDbContext` como `DbSet<PageDefinition>` y `DbSet<PagePermission>`.

---

## 🔄 Auto-descubrimiento de pantallas (requisito crítico)

Crear un servicio **hosted** que al iniciar la aplicación (o bajo demanda) escanee todas las páginas Blazor del ensamblado y sincronice la tabla `PageDefinitions`.

### Servicio de descubrimiento

```csharp
// Application/Common/Interfaces/IPageDiscoveryService.cs
public interface IPageDiscoveryService
{
    Task SyncPageDefinitionsAsync(CancellationToken ct = default);
}

// Infrastructure/Services/PageDiscoveryService.cs
public class PageDiscoveryService : IPageDiscoveryService
{
    private readonly AppDbContext _db;
    private readonly ILogger<PageDiscoveryService> _logger;

    public PageDiscoveryService(AppDbContext db, ILogger<PageDiscoveryService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SyncPageDefinitionsAsync(CancellationToken ct = default)
    {
        // 1. Obtener todas las clases del ensamblado Web que tengan [Route] o @page
        var assembly = typeof(GreenTransit.Web.Program).Assembly; // o el ensamblado de las páginas
        var pageTypes = assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(Microsoft.AspNetCore.Components.ComponentBase))
                     && t.GetCustomAttributes(typeof(Microsoft.AspNetCore.Components.RouteAttribute), true).Any())
            .ToList();

        // 2. Extraer rutas
        var discoveredPages = new List<(string Route, string ComponentName, string ModuleName)>();
        foreach (var type in pageTypes)
        {
            var routeAttrs = type.GetCustomAttributes(typeof(Microsoft.AspNetCore.Components.RouteAttribute), true)
                                 .Cast<Microsoft.AspNetCore.Components.RouteAttribute>();
            foreach (var attr in routeAttrs)
            {
                var moduleName = InferModuleName(type.Namespace, attr.Template);
                discoveredPages.Add((attr.Template, type.Name, moduleName));
            }
        }

        // 3. Obtener rutas existentes en BD
        var existingRoutes = await _db.PageDefinitions
            .Select(p => p.Route)
            .ToListAsync(ct);

        // 4. Insertar las nuevas (las que no existen en BD)
        var newPages = discoveredPages
            .Where(dp => !existingRoutes.Contains(dp.Route))
            .ToList();

        foreach (var page in newPages)
        {
            _db.PageDefinitions.Add(new PageDefinition
            {
                Route = page.Route,
                PageName = HumanizeName(page.ComponentName), // Ej: "ServiceOrderList" → "Órdenes de Servicio"
                ModuleName = page.ModuleName,
                ComponentName = page.ComponentName,
                IsActive = true,
                SortOrder = 0,
                CreatedAt = DateTime.UtcNow
            });
            _logger.LogInformation("Nueva pantalla descubierta: {Route} ({Component})", page.Route, page.ComponentName);
        }

        if (newPages.Count > 0)
            await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Sincronización de pantallas completada: {New} nuevas, {Total} total",
            newPages.Count, discoveredPages.Count);
    }

    /// <summary>
    /// Infiere el módulo a partir del namespace o la ruta.
    /// Ejemplos:
    ///   - "Pages.Security.*"        → "Seguridad"
    ///   - "Pages.Reporting.*"       → "Reporting"
    ///   - "Pages.Logistics.*"       → "Dashboards Logísticos"
    ///   - "/entities", "/ler-codes" → "Configuración"
    ///   - "/service-orders"         → "Operaciones"
    /// </summary>
    private string InferModuleName(string? ns, string route)
    {
        // Implementar lógica de mapeo namespace/ruta → nombre de módulo en español
        if (ns?.Contains("Security") == true) return "Seguridad";
        if (ns?.Contains("Reporting") == true) return "Reporting";
        if (ns?.Contains("Logistics") == true) return "Dashboards Logísticos";
        if (ns?.Contains("Sustainability") == true) return "Sostenibilidad";

        // Por ruta
        return route switch
        {
            var r when r.StartsWith("/entities") || r.StartsWith("/ler-codes")
                    || r.StartsWith("/residues") || r.StartsWith("/treatment-operations") => "Configuración",
            var r when r.StartsWith("/service-orders") || r.StartsWith("/waste-moves")
                    || r.StartsWith("/entry-") || r.StartsWith("/treatment-plants") => "Operaciones",
            var r when r.StartsWith("/agreements") || r.StartsWith("/settlements")
                    || r.StartsWith("/market-shares") => "Contratos y Liquidaciones",
            var r when r.StartsWith("/incidents") || r.StartsWith("/dum-zones")
                    || r.StartsWith("/emissions") || r.StartsWith("/plant-energies") => "Sostenibilidad",
            var r when r.StartsWith("/users") || r.StartsWith("/profiles")
                    || r.StartsWith("/security") => "Seguridad",
            var r when r.StartsWith("/product-declarations") => "Declaraciones de Producto",
            _ => "General"
        };
    }

    private string HumanizeName(string componentName)
    {
        // Implementar mapeo de nombres de componente a nombres legibles
        // Ej: "ServiceOrderList" → "Órdenes de Servicio"
        //     "UserList"         → "Usuarios"
        //     "ProfileList"      → "Perfiles"
        // Usar un diccionario estático o inferir desde el nombre
        return componentName; // Placeholder: el admin puede renombrarlo desde la UI
    }
}
```

### Registro en startup

En `Program.cs` o `DbInitializer`, ejecutar la sincronización **después** de las migraciones y el seed de `Profiles`:

```csharp
// En DbInitializer.cs (ya existe para seed de Profiles)
// Al final del método InitializeAsync:
var pageDiscovery = serviceProvider.GetRequiredService<IPageDiscoveryService>();
await pageDiscovery.SyncPageDefinitionsAsync();
```

Registrar el servicio:

```csharp
// Program.cs
builder.Services.AddScoped<IPageDiscoveryService, PageDiscoveryService>();
```

---

## 📋 Capa Application — CQRS (MediatR)

Crear en `Application/Features/Security/`:

### DTOs

```csharp
// DTOs/PageDefinitionDto.cs
public record PageDefinitionDto
{
    public int Id { get; init; }
    public string Route { get; init; } = string.Empty;
    public string PageName { get; init; } = string.Empty;
    public string ModuleName { get; init; } = string.Empty;
    public string? ComponentName { get; init; }
    public bool IsActive { get; init; }
    public int SortOrder { get; init; }
    public List<PagePermissionDto> Permissions { get; init; } = new();
}

// DTOs/PagePermissionDto.cs
public record PagePermissionDto
{
    public int Id { get; init; }
    public int IdPageDefinition { get; init; }
    public int IdProfile { get; init; }
    public string ProfileReference { get; init; } = string.Empty;
    public string ProfileDescription { get; init; } = string.Empty;
    public string AccessLevel { get; init; } = "Read";
}

// DTOs/PagePermissionMatrixDto.cs — Vista matricial para la UI
public record PagePermissionMatrixDto
{
    public List<ProfileSummaryDto> Profiles { get; init; } = new();
    public List<ModuleGroupDto> Modules { get; init; } = new();
}

public record ProfileSummaryDto
{
    public int Id { get; init; }
    public string Reference { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

public record ModuleGroupDto
{
    public string ModuleName { get; init; } = string.Empty;
    public List<PageWithPermissionsDto> Pages { get; init; } = new();
}

public record PageWithPermissionsDto
{
    public int Id { get; init; }
    public string Route { get; init; } = string.Empty;
    public string PageName { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    /// <summary>
    /// Diccionario: IdProfile → AccessLevel ("Read" | "Write" | "ReadWrite" | null si sin acceso)
    /// </summary>
    public Dictionary<int, string?> PermissionsByProfile { get; init; } = new();
}
```

### Queries

```csharp
// Queries/GetPagePermissionMatrixQuery.cs
public record GetPagePermissionMatrixQuery : IRequest<PagePermissionMatrixDto>
{
    public string? ModuleFilter { get; init; }
    public string? SearchTerm { get; init; }
    public bool IncludeInactive { get; init; } = false;
}

// Handler:
// 1. Cargar todos los Profiles (sin filtro OwnerId — son catálogo del sistema)
// 2. Cargar PageDefinitions con sus PagePermissions (Include)
// 3. Filtrar por ModuleName si se proporciona
// 4. Filtrar por SearchTerm (buscar en Route, PageName)
// 5. Filtrar por IsActive si !IncludeInactive
// 6. Agrupar por ModuleName
// 7. Para cada página, crear el diccionario IdProfile → AccessLevel
// 8. Devolver PagePermissionMatrixDto
```

### Commands

```csharp
// Commands/UpdatePagePermissionCommand.cs
public record UpdatePagePermissionCommand : IRequest<Unit>
{
    public int IdPageDefinition { get; init; }
    public int IdProfile { get; init; }
    public string? AccessLevel { get; init; } // null = quitar permiso, "Read" | "Write" | "ReadWrite"
}

// Handler:
// 1. Validar que existen PageDefinition e IdProfile
// 2. Si AccessLevel es null → eliminar el registro de PagePermissions (quitar acceso)
// 3. Si existe registro → actualizar AccessLevel + UpdatedAt
// 4. Si no existe → crear nuevo PagePermission
// 5. Registrar IdUser del admin que hizo el cambio
// 6. Log con Serilog: "Permiso actualizado: página {Route}, perfil {Profile}, acceso {Level}"

// Commands/BulkUpdatePagePermissionsCommand.cs — Para guardar toda la matriz de golpe
public record BulkUpdatePagePermissionsCommand : IRequest<Unit>
{
    public List<PagePermissionEntry> Entries { get; init; } = new();
}

public record PagePermissionEntry
{
    public int IdPageDefinition { get; init; }
    public int IdProfile { get; init; }
    public string? AccessLevel { get; init; }
}

// Handler:
// 1. Validar todas las entradas
// 2. Cargar todos los PagePermissions existentes para las páginas implicadas
// 3. Para cada entrada:
//    - null → eliminar si existe
//    - valor → upsert (actualizar o crear)
// 4. SaveChangesAsync en una sola transacción
// 5. Log resumen: "Actualización masiva de permisos: {Count} cambios por usuario {IdUser}"

// Commands/UpdatePageDefinitionCommand.cs — Para editar nombre/módulo/orden de una página
public record UpdatePageDefinitionCommand : IRequest<Unit>
{
    public int Id { get; init; }
    public string PageName { get; init; } = string.Empty;
    public string ModuleName { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public bool IsActive { get; init; }
}

// Commands/SyncPageDefinitionsCommand.cs — Lanzar re-descubrimiento bajo demanda
public record SyncPageDefinitionsCommand : IRequest<int>; // Devuelve nº de páginas nuevas descubiertas
```

### Validators (FluentValidation)

```csharp
// Validators/UpdatePagePermissionValidator.cs
public class UpdatePagePermissionValidator : AbstractValidator<UpdatePagePermissionCommand>
{
    public UpdatePagePermissionValidator()
    {
        RuleFor(x => x.IdPageDefinition).GreaterThan(0);
        RuleFor(x => x.IdProfile).GreaterThan(0);
        RuleFor(x => x.AccessLevel)
            .Must(v => v == null || v == "Read" || v == "Write" || v == "ReadWrite")
            .WithMessage("AccessLevel debe ser 'Read', 'Write', 'ReadWrite' o null.");
    }
}
```

---

## 🖥️ Capa Web — Página Blazor

### Ruta y autorización

```
Ruta:    /security/page-permissions
Policy:  CanManagePagePermissions → solo ADMIN
Menú:    Seguridad → debajo de "Perfiles" (/profiles)
```

### Policy nueva

Añadir en `PolicyConstants.cs`:

```csharp
public const string CanManagePagePermissions = "CanManagePagePermissions";
```

Registrar en el `AuthorizationPolicyProvider` o en `Program.cs`:

```csharp
options.AddPolicy(PolicyConstants.CanManagePagePermissions, policy =>
    policy.RequireClaim("gt_profile", ProfileConstants.Admin));
```

### Entrada en el menú lateral

Añadir en `NavMenu.razor` (o componente equivalente), dentro de la sección **Seguridad**, **debajo de Perfiles**:

```razor
@* --- Sección Seguridad --- *@
<AuthorizeView Policy="@PolicyConstants.CanManageUsers">
    <Authorized>
        <NavLink href="/users" Match="NavLinkMatch.Prefix">
            <i class="bi bi-people"></i> Usuarios
        </NavLink>
    </Authorized>
</AuthorizeView>

<AuthorizeView Policy="@PolicyConstants.CanManageProfiles">
    <Authorized>
        <NavLink href="/profiles" Match="NavLinkMatch.Prefix">
            <i class="bi bi-person-badge"></i> Perfiles
        </NavLink>
    </Authorized>
</AuthorizeView>

@* ← NUEVO: Permisos por Pantalla — DEBAJO de Perfiles *@
<AuthorizeView Policy="@PolicyConstants.CanManagePagePermissions">
    <Authorized>
        <NavLink href="/security/page-permissions" Match="NavLinkMatch.Prefix">
            <i class="bi bi-shield-lock"></i> Permisos por Pantalla
        </NavLink>
    </Authorized>
</AuthorizeView>
```

### Componente principal: `PagePermissionMatrix.razor`

Ubicación: `Web/Components/Pages/Security/PagePermissionMatrix.razor`

```razor
@page "/security/page-permissions"
@attribute [Authorize(Policy = PolicyConstants.CanManagePagePermissions)]
@using GreenTransit.Domain.Authorization
@inject IMediator Mediator
@inject ICurrentUserService CurrentUser
@inject IJSRuntime JS
```

### Diseño de la UI

La pantalla muestra una **matriz/grilla** donde:

**Filas** = pantallas de la aplicación, agrupadas por módulo (acordeón o secciones colapsables).
**Columnas** = perfiles del sistema (ADMIN, SCRAP, PRODUCER, CARRIER, PLANT_OP, CAC_OP, PUBLIC_ENT, COORDINATOR, DISPATCH_OFFICE).

**Cada celda** contiene un selector con 4 opciones:
- `—` (sin acceso) → se muestra con fondo gris claro
- `R` (Lectura) → badge azul
- `W` (Escritura) → badge naranja
- `RW` (Lectura + Escritura) → badge verde

**Estructura visual**:

```
┌─────────────────────────────────────────────────────────────────────────┐
│ 🔒 Permisos por Pantalla                          [🔄 Sincronizar] [💾 Guardar] │
├─────────────────────────────────────────────────────────────────────────┤
│ Filtro módulo: [Todos ▾]    Buscar: [________________]  □ Mostrar inactivas │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│ ▼ Configuración                                                         │
│ ┌──────────────────┬───────┬───────┬───────┬───────┬───────┬──────┬...┐ │
│ │ Pantalla          │ ADMIN │ SCRAP │ PROD  │ CARR  │ PLANT │ CAC  │   │ │
│ ├──────────────────┼───────┼───────┼───────┼───────┼───────┼──────┤   │ │
│ │ /entities         │ [RW▾] │ [R▾]  │ [R▾]  │ [R▾]  │ [R▾]  │ [R▾] │   │ │
│ │ /ler-codes        │ [RW▾] │ [R▾]  │ [R▾]  │ [—▾]  │ [—▾]  │ [—▾] │   │ │
│ │ /residues         │ [RW▾] │ [R▾]  │ [RW▾] │ [R▾]  │ [R▾]  │ [R▾] │   │ │
│ └──────────────────┴───────┴───────┴───────┴───────┴───────┴──────┘   │ │
│                                                                         │
│ ▼ Operaciones                                                           │
│ ┌──────────────────┬───────┬───────┬───────┬───────┬───────┬──────┐   │ │
│ │ /service-orders   │ [RW▾] │ [R▾]  │ [RW▾] │ [R▾]  │ [R▾]  │ [R▾] │   │ │
│ │ /waste-moves      │ [RW▾] │ [R▾]  │ [R▾]  │ [W▾]  │ [R▾]  │ [R▾] │   │ │
│ └──────────────────┴───────┴───────┴───────┴───────┴───────┴──────┘   │ │
│                                                                         │
│ ▼ Seguridad                                                             │
│ ┌──────────────────┬───────┬───────┬───────┬───────┬───────┬──────┐   │ │
│ │ /users            │ [RW▾] │ [—▾]  │ [—▾]  │ [—▾]  │ [—▾]  │ [—▾] │   │ │
│ │ /profiles         │ [RW▾] │ [—▾]  │ [—▾]  │ [—▾]  │ [—▾]  │ [—▾] │   │ │
│ │ /security/page-   │ [RW▾] │ [—▾]  │ [—▾]  │ [—▾]  │ [—▾]  │ [—▾] │   │ │
│ │  permissions      │       │       │       │       │       │      │   │ │
│ └──────────────────┴───────┴───────┴───────┴───────┴───────┴──────┘   │ │
│                                                                         │
│ ⚠️ Pantallas nuevas (sin permisos configurados)                         │
│ ┌──────────────────┬───────┬───────┬───────┬───────┬───────┬──────┐   │ │
│ │ /nueva-pantalla   │ [—▾]  │ [—▾]  │ [—▾]  │ [—▾]  │ [—▾]  │ [—▾] │   │ │
│ └──────────────────┴───────┴───────┴───────┴───────┴───────┴──────┘   │ │
│                                                                         │
│                                              Cambios pendientes: 3  [💾] │
└─────────────────────────────────────────────────────────────────────────┘
```

### Comportamiento de la UI

1. **Carga inicial**: ejecuta `GetPagePermissionMatrixQuery` y renderiza la matriz completa.
2. **Cada celda** es un `<select>` con las 4 opciones (`—`, `R`, `W`, `RW`). Al cambiar, se acumula el cambio en una lista local `_pendingChanges`.
3. **Botón "Guardar"**: envía `BulkUpdatePagePermissionsCommand` con todos los cambios pendientes. Muestra toast de confirmación.
4. **Botón "Sincronizar"**: ejecuta `SyncPageDefinitionsCommand`. Si descubre nuevas pantallas, recarga la matriz y muestra notificación con el nº de pantallas nuevas. Las pantallas nuevas sin permisos se destacan con un badge ⚠️ o fondo amarillo claro.
5. **Filtro por módulo**: `<select>` con los valores únicos de `ModuleName`.
6. **Buscador**: filtra en cliente por `Route` o `PageName` (debounce 300ms).
7. **Checkbox "Mostrar inactivas"**: incluye/excluye páginas con `IsActive = false` (se muestran en gris tachado).
8. **Edición de nombre**: cada fila tiene un botón de lápiz (✏️) que abre un modal inline para editar `PageName`, `ModuleName`, `SortOrder` e `IsActive` vía `UpdatePageDefinitionCommand`.
9. **Columna ADMIN siempre en RW**: la columna del perfil ADMIN no debe poder ponerse en `—` ni en `R` solo — validación en frontend y backend.
10. **Scroll horizontal**: la tabla puede tener muchas columnas (9 perfiles); usar `overflow-x: auto` con cabeceras sticky.
11. **Indicador de cambios pendientes**: counter visible en la esquina inferior derecha que muestra cuántos cambios hay sin guardar.
12. **Confirmación al salir**: si hay cambios pendientes y el usuario navega fuera, mostrar diálogo de confirmación.

### Estilos de badges

```css
/* Consistente con el sistema de diseño GreenTransit */
.access-none  { background: #f0f0f0; color: #999; }      /* — Sin acceso */
.access-read  { background: #e3f2fd; color: #1565c0; }    /* R  Lectura */
.access-write { background: #fff3e0; color: #e65100; }    /* W  Escritura */
.access-rw    { background: #e8f5e9; color: #2e7d32; }    /* RW Ambos */
.page-new     { background: #fffde7; }                     /* Fila nueva sin permisos */
.page-inactive { opacity: 0.5; text-decoration: line-through; }
```

---

## 🔄 Flujo de auto-descubrimiento

Cuando un desarrollador añade una nueva página Blazor (por ejemplo `NuevaPantalla.razor` con `@page "/nueva-ruta"`):

1. **Al desplegar** (restart de la app), el `DbInitializer` ejecuta `IPageDiscoveryService.SyncPageDefinitionsAsync()`.
2. El servicio detecta la nueva ruta `/nueva-ruta` que no existe en `PageDefinitions`.
3. Inserta un nuevo registro con `IsActive = true`, `PageName` inferido del nombre del componente, `ModuleName` inferido del namespace/ruta.
4. Como no tiene registros en `PagePermissions`, aparece en la sección **"Pantallas nuevas (sin permisos)"** con fondo amarillo.
5. El administrador entra en `/security/page-permissions`, ve la nueva pantalla destacada, y asigna los permisos adecuados.

**Importante**: la sincronización es **aditiva** — nunca elimina pantallas de `PageDefinitions` aunque se borre el `.razor`. Si se borra un componente, la entrada queda en BD pero el admin puede marcarla como `IsActive = false`.

---

## 🧪 Tests

Crear en `Tests/Application/Security/`:

```csharp
// Tests sugeridos:
// 1. GetPagePermissionMatrixQueryTests
//    - Devuelve todos los perfiles
//    - Agrupa por módulo
//    - Filtra por ModuleName
//    - Filtra por SearchTerm
//    - Excluye inactivas por defecto

// 2. UpdatePagePermissionCommandTests
//    - Crea permiso nuevo (Read/Write/ReadWrite)
//    - Actualiza permiso existente
//    - Elimina permiso cuando AccessLevel es null
//    - Rechaza AccessLevel inválido
//    - No permite quitar RW al ADMIN en su propia página de permisos

// 3. BulkUpdatePagePermissionsCommandTests
//    - Aplica múltiples cambios en una transacción
//    - Rollback si algún cambio falla

// 4. SyncPageDefinitionsCommandTests (con mock del ensamblado)
//    - Detecta nuevas páginas
//    - No duplica existentes
//    - No elimina páginas que ya no existen como componente

// 5. PageDiscoveryServiceTests
//    - Infiere módulo correctamente
//    - Genera PageName legible
```

---

## 📋 Actualización de documentación del proyecto

### En `PATRON_AUTORIZACION_PAGINAS.md` — Sección 2.5 Módulo de Seguridad

Añadir:

```markdown
| `/security/page-permissions` | `[Authorize(Policy = PolicyConstants.CanManagePagePermissions)]` | Solo ADMIN |
```

### En `Mapa_Autorizacion_GreenTransit.md` — Sección 7.1 Policies

Añadir:

```
CanManagePagePermissions        ADMIN
```

### En `Mapa_Autorizacion_GreenTransit.md` — Sección 4 Matriz

Añadir fila:

```
| **Permisos por Pantalla** | `PageDefinitions` / `PagePermissions` | — | — | — | — | — | — | — | — | **CRUD** |
```

### En `Mapa_Funcionalidades_GreenTransit.md` — Sección 6

Añadir subsección:

```markdown
### 6.6. Gestión de Permisos por Pantalla (`PagePermissions`)

- **Lógica**: matriz interactiva que cruza todas las pantallas de la aplicación con todos los perfiles,
  permitiendo al administrador configurar el nivel de acceso (Lectura, Escritura, Ambos) para cada combinación.
  Las pantallas se auto-descubren al arrancar la aplicación mediante reflexión sobre los componentes Blazor
  con atributo `@page`. Las nuevas pantallas aparecen automáticamente marcadas como "sin permisos configurados".
- **Entidades**: `PageDefinitions`, `PagePermissions`.
- **Ruta**: `/security/page-permissions`
- **Policy**: `CanManagePagePermissions`
- **Funciones**: vista matricial, edición inline, guardado masivo, sincronización bajo demanda,
  filtros por módulo y búsqueda, edición de metadatos de página.
- **Roles**: solo **ADMIN**.
```

### En `COPILOT_CONTEXT.md`

Añadir en la sección de estado:

```
Paso 10 — Gestión de Permisos por Pantalla ✅ COMPLETADO
  - PageDefinitions + PagePermissions (tablas)
  - IPageDiscoveryService (auto-descubrimiento en startup)
  - GetPagePermissionMatrixQuery, UpdatePagePermissionCommand, BulkUpdatePagePermissionsCommand
  - PagePermissionMatrix.razor → /security/page-permissions — Policy: CanManagePagePermissions
  - Entrada en menú lateral: Seguridad → debajo de Perfiles
```

---

## ⚙️ Resumen de archivos a crear/modificar

### Archivos NUEVOS

| Capa | Archivo | Descripción |
|------|---------|-------------|
| Domain | `Entities/PageDefinition.cs` | Entidad de dominio |
| Domain | `Entities/PagePermission.cs` | Entidad de dominio |
| Domain | `Enums/AccessLevel.cs` | Enum Read/Write/ReadWrite |
| Application | `Common/Interfaces/IPageDiscoveryService.cs` | Interfaz del servicio de descubrimiento |
| Application | `Features/Security/DTOs/PageDefinitionDto.cs` | DTO |
| Application | `Features/Security/DTOs/PagePermissionDto.cs` | DTO |
| Application | `Features/Security/DTOs/PagePermissionMatrixDto.cs` | DTO matricial |
| Application | `Features/Security/Queries/GetPagePermissionMatrixQuery.cs` | Query + Handler |
| Application | `Features/Security/Commands/UpdatePagePermissionCommand.cs` | Command + Handler |
| Application | `Features/Security/Commands/BulkUpdatePagePermissionsCommand.cs` | Command + Handler |
| Application | `Features/Security/Commands/UpdatePageDefinitionCommand.cs` | Command + Handler |
| Application | `Features/Security/Commands/SyncPageDefinitionsCommand.cs` | Command + Handler |
| Application | `Features/Security/Validators/UpdatePagePermissionValidator.cs` | FluentValidation |
| Infrastructure | `Persistence/Configurations/PageDefinitionConfiguration.cs` | EF Core config |
| Infrastructure | `Persistence/Configurations/PagePermissionConfiguration.cs` | EF Core config |
| Infrastructure | `Services/PageDiscoveryService.cs` | Implementación del descubrimiento |
| Web | `Components/Pages/Security/PagePermissionMatrix.razor` | Página Blazor principal |
| Tests | `Application/Security/GetPagePermissionMatrixQueryTests.cs` | Tests unitarios |
| Tests | `Application/Security/UpdatePagePermissionCommandTests.cs` | Tests unitarios |
| Tests | `Application/Security/PageDiscoveryServiceTests.cs` | Tests unitarios |

### Archivos a MODIFICAR

| Archivo | Cambio |
|---------|--------|
| `Infrastructure/Persistence/AppDbContext.cs` | Añadir `DbSet<PageDefinition>` y `DbSet<PagePermission>` |
| `Domain/Authorization/PolicyConstants.cs` | Añadir `CanManagePagePermissions` |
| `Web/Program.cs` | Registrar policy + `IPageDiscoveryService` |
| `Infrastructure/Persistence/DbInitializer.cs` | Llamar a `SyncPageDefinitionsAsync()` tras seed de Profiles |
| `Web/Components/Layout/NavMenu.razor` | Añadir enlace a `/security/page-permissions` debajo de `/profiles` |

### Migración EF Core

```bash
dotnet ef migrations add AddPagePermissions --project GreenTransit.Infrastructure --startup-project GreenTransit.Web
dotnet ef database update --project GreenTransit.Infrastructure --startup-project GreenTransit.Web
```

---

## 🚀 Orden de implementación sugerido

1. **Modelo de datos**: crear entidades de dominio + configuraciones EF Core + migración.
2. **Servicio de descubrimiento**: `IPageDiscoveryService` + `PageDiscoveryService` + registro en DI + llamada en `DbInitializer`.
3. **Queries y Commands**: DTOs, `GetPagePermissionMatrixQuery`, `UpdatePagePermissionCommand`, `BulkUpdatePagePermissionsCommand`, `UpdatePageDefinitionCommand`, `SyncPageDefinitionsCommand`.
4. **Validadores**: FluentValidation para los commands.
5. **Policy**: `CanManagePagePermissions` en `PolicyConstants` y `Program.cs`.
6. **Página Blazor**: `PagePermissionMatrix.razor` con la UI matricial completa.
7. **Menú lateral**: entrada en `NavMenu.razor`.
8. **Tests**: unitarios para queries, commands y servicio de descubrimiento.
9. **Documentación**: actualizar `COPILOT_CONTEXT.md`, `PATRON_AUTORIZACION_PAGINAS.md`, `Mapa_Autorizacion_GreenTransit.md`, `Mapa_Funcionalidades_GreenTransit.md`.
