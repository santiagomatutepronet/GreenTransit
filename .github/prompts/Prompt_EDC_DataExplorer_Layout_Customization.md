# 🤖 Prompt para GitHub Copilot — EDC Data Explorer: Personalización de Layout con Persistencia

> **Instrucción**: Copia este prompt completo en GitHub Copilot Chat adjuntando los archivos `COPILOT_CONTEXT.md`, `Mapa_Funcionalidades.md`, `Crear_BD_v4_1.sql`, `EdcDataExplorer.razor`, `DynamicWidgetDescriptor.cs`, `EdcDataExplorerResult.cs`, `EdcDataExplorerStateService.cs`, `DashboardLayoutBuilder.cs`, `ConsumeData.razor` y `AppDbContext.cs`.
>
> **Stack**: .NET 10 · Clean Architecture (Domain / Application / Infrastructure / Web) · Blazor Server · Radzen Blazor Components · EF Core · MediatR · FluentValidation · System.Text.Json.
>
> **Prerequisito**: El módulo "EDC Data Explorer" (prompt `Prompt_EDC_DataExplorer_Dashboard_Dinamico.md`) debe estar completamente implementado y funcionando. Este prompt **extiende** esa funcionalidad añadiendo personalización y persistencia del layout.
>
> **Ejecuta los prompts en orden**. Cada fase debe compilar antes de pasar a la siguiente.

---

## 📋 ÍNDICE Y ESTADO

| ID | Fase | Descripción | Estado |
|---|---|---|:-:|
| LC-0 | Contexto | Instrucción base — NO genera código | ⬜ |
| LC-1 | Modelo de datos | Entidad `ExplorerLayoutConfig` + tabla SQL + EF Core | ⬜ |
| LC-2 | DTOs | DTOs para configuración de layout serializable | ⬜ |
| LC-3 | CQRS Lectura | `GetExplorerLayoutConfigQuery` — cargar config guardada | ⬜ |
| LC-4 | CQRS Escritura | `SaveExplorerLayoutConfigCommand` — guardar config | ⬜ |
| LC-5 | Servicio | `ILayoutCustomizationService` — merge de config guardada con widgets generados | ⬜ |
| LC-6 | UI Editor | `LayoutEditorToolbar.razor` — barra de herramientas de personalización | ⬜ |
| LC-7 | UI Drag & Drop | Reordenación de widgets por arrastre | ⬜ |
| LC-8 | UI Widget Config | `WidgetConfigPanel.razor` — panel de configuración por widget | ⬜ |
| LC-9 | Integración | Modificar `EdcDataExplorer.razor` para modo edición + persistencia | ⬜ |
| LC-10 | Tests | Tests unitarios del servicio de merge y de los commands | ⬜ |

---

## 🎯 OBJETIVO GENERAL

El Data Explorer ya genera un dashboard automático a partir del JSON descargado. Ahora queremos que el usuario pueda **personalizar el layout generado** y que esa personalización se **guarde vinculada al AssetId** del catálogo EDC, de modo que la próxima vez que se descargue el mismo asset, el layout ya esté personalizado.

**Funcionalidades a implementar**:
1. **Reordenar widgets** — arrastrar y soltar para cambiar el orden de los widgets.
2. **Ocultar widgets** — marcar widgets como "no visibles" (sin perder la configuración).
3. **Cambiar tipo de gráfico** — convertir un bar chart en donut, un line chart en area, etc.
4. **Cambiar el ancho de columna** — ajustar el ColumnSpan (3, 4, 6, 12).
5. **Renombrar widgets** — editar el título visible de cualquier widget.
6. **Guardar configuración** — persistir la configuración personalizada vinculada a `OwnerId` + `AssetId` + `UserId`.
7. **Cargar configuración** — al abrir el Data Explorer con un AssetId ya conocido, aplicar la configuración guardada.
8. **Resetear a automático** — botón para descartar la personalización y volver al layout generado por las heurísticas.

**Principios clave**:
- Se crea UNA nueva tabla (`ExplorerLayoutConfigs`) — es la única excepción a la regla de "no nuevas entidades" porque la personalización requiere persistencia.
- La configuración se serializa como JSON dentro de un campo `nvarchar(max)` — no se crea una tabla por widget.
- Multi-tenant: la configuración es por `OwnerId` + `UserId` + `AssetId`.
- Si no existe configuración guardada, el dashboard se genera automáticamente (comportamiento actual).
- Si existe configuración guardada pero el JSON del asset ha cambiado de estructura (nuevos campos, campos eliminados), el servicio de merge combina la config guardada con la nueva estructura.

---

## LC-0 — Instrucción base (contexto)

```
CONTEXTO DEL PROYECTO:
- Proyecto GreenTransit — .NET 10, Blazor Web App (Server), Radzen Blazor Components, EF Core, SQL Server Azure.
- Clean Architecture: GreenTransit.Domain / Application / Infrastructure / Web / Tests.
- MediatR, FluentValidation, Serilog, xUnit ya configurados.
- Módulo "EDC Data Explorer" COMPLETAMENTE IMPLEMENTADO:
  · IJsonSchemaAnalyzer analiza el JSON y produce JsonDataSchema.
  · IDashboardLayoutBuilder genera List<DynamicWidgetDescriptor> con heurísticas automáticas.
  · AnalyzeEdcDataQuery orquesta análisis + layout vía MediatR.
  · EdcDataExplorer.razor renderiza widgets dinámicos (KpiCard, DataTable, Chart, SectionHeader, KeyValueList, InfoText).
  · EdcDataExplorerStateService (Scoped) transporta el JSON entre ConsumeData.razor y EdcDataExplorer.razor.
  · DynamicWidgetDescriptor tiene: WidgetId, Type (enum WidgetType), Title, SortOrder, ColumnSpan, ChartType (enum ChartSubType), y datos específicos por tipo.
- El flujo EDC existente: catálogo → selección de dataset (EdcDatasetDto con DatasetId) → negociación → transferencia → descarga → JSON disponible.
- El EdcNegotiationSelection contiene: SelectedDatasetId (string), SelectedOfferId (string), ProviderParticipantId (string).
- Multi-tenant: filtro por OwnerId en todas las queries.
- Autorización dinámica vía PageDefinitions/PagePermissions.

OBJETIVO:
Extender el Data Explorer con personalización de layout persistida por AssetId.

REGLAS GENERALES:
1. Crear UNA sola tabla nueva (ExplorerLayoutConfigs) — justificación: la personalización requiere persistencia.
2. La configuración se almacena como JSON serializado en un campo nvarchar(max) para flexibilidad.
3. Interfaces en Application, implementaciones de persistencia en Infrastructure.
4. Usar System.Text.Json para serialización/deserialización.
5. Nombrar todo en inglés (clases, métodos, variables). Comentarios en español.
6. Respetar el patrón CQRS con MediatR.
7. El drag & drop se implementa con JavaScript interop mínimo + estado Blazor (NO librerías externas de drag & drop).

NO generes código aún. Confirma que has entendido el contexto.
```

---

## LC-1 — Modelo de datos: Entidad + Tabla SQL + EF Core

```
FASE LC-1 — Modelo de datos para persistencia de layout

OBJETIVO: Crear la entidad de dominio, la tabla SQL, la configuración EF Core y la migración.

--- ARCHIVO 1: GreenTransit.Domain/Entities/ExplorerLayoutConfig.cs ---

namespace GreenTransit.Domain.Entities;

/// <summary>
/// Configuración personalizada del layout del Data Explorer, vinculada a un asset EDC.
/// Cada combinación OwnerId + UserId + AssetId tiene como máximo una configuración.
/// </summary>
public class ExplorerLayoutConfig
{
    /// <summary>PK auto-incremental.</summary>
    public int Id { get; set; }
    
    /// <summary>Tenant al que pertenece la configuración.</summary>
    public Guid OwnerId { get; set; }
    
    /// <summary>ID del usuario que creó/modificó la configuración.</summary>
    public int UserId { get; set; }
    
    /// <summary>
    /// Identificador del asset EDC en el catálogo DCAT del proveedor.
    /// Corresponde a EdcDatasetDto.DatasetId.
    /// </summary>
    public string AssetId { get; set; } = string.Empty;
    
    /// <summary>
    /// Identificador del participante proveedor del asset (para distinguir assets
    /// con mismo ID de distintos proveedores).
    /// Corresponde a EdcNegotiationSelection.ProviderParticipantId.
    /// </summary>
    public string ProviderParticipantId { get; set; } = string.Empty;
    
    /// <summary>
    /// Nombre descriptivo del dataset (para UI, no como clave).
    /// </summary>
    public string? DatasetName { get; set; }
    
    /// <summary>
    /// JSON serializado con la configuración de widgets personalizada.
    /// Contiene un array de WidgetLayoutOverride serializado.
    /// </summary>
    public string LayoutConfigJson { get; set; } = "[]";
    
    /// <summary>Hash MD5 del esquema JSON detectado la última vez que se guardó.
    /// Permite detectar si la estructura del asset ha cambiado.</summary>
    public string? SchemaHash { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

--- ARCHIVO 2: SQL para la tabla ---

Genera la migración EF Core que cree esta tabla. El SQL resultante debe ser equivalente a:

CREATE TABLE [dbo].[ExplorerLayoutConfigs] (
    [Id]                      INT IDENTITY(1,1) NOT NULL,
    [OwnerId]                 UNIQUEIDENTIFIER  NOT NULL,
    [UserId]                  INT               NOT NULL,
    [AssetId]                 NVARCHAR(512)     NOT NULL,
    [ProviderParticipantId]   NVARCHAR(512)     NOT NULL,
    [DatasetName]             NVARCHAR(256)     NULL,
    [LayoutConfigJson]        NVARCHAR(MAX)     NOT NULL DEFAULT '[]',
    [SchemaHash]              NVARCHAR(64)      NULL,
    [CreatedAt]               DATETIME2(0)      NOT NULL DEFAULT (SYSUTCDATETIME()),
    [UpdatedAt]               DATETIME2(0)      NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_ExplorerLayoutConfigs] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_ExplorerLayoutConfigs_Tenant_User_Asset] 
        UNIQUE NONCLUSTERED ([OwnerId], [UserId], [AssetId], [ProviderParticipantId])
);
GO
CREATE NONCLUSTERED INDEX [IX_ExplorerLayoutConfigs_OwnerId] 
    ON [dbo].[ExplorerLayoutConfigs] ([OwnerId]) INCLUDE ([AssetId], [UserId]);
GO

--- ARCHIVO 3: GreenTransit.Infrastructure/Persistence/Configurations/ExplorerLayoutConfigConfiguration.cs ---

Configuración EF Core con Fluent API:
- Table name: "ExplorerLayoutConfigs"
- Property AssetId: MaxLength(512), IsRequired
- Property ProviderParticipantId: MaxLength(512), IsRequired
- Property DatasetName: MaxLength(256)
- Property LayoutConfigJson: tipo columna nvarchar(max), default "[]"
- Property SchemaHash: MaxLength(64)
- HasIndex sobre { OwnerId, UserId, AssetId, ProviderParticipantId } con IsUnique

--- ARCHIVO 4: Registrar DbSet en AppDbContext.cs ---

Añadir en AppDbContext:
public DbSet<ExplorerLayoutConfig> ExplorerLayoutConfigs => Set<ExplorerLayoutConfig>();

--- ARCHIVO 5: Migración EF Core ---

Ejecutar: dotnet ef migrations add AddExplorerLayoutConfigs --project src/GreenTransit.Infrastructure --startup-project src/GreenTransit.Web

VERIFICACIÓN: 
- La migración se genera correctamente.
- dotnet ef database update aplica sin errores.
- La tabla tiene el índice único sobre (OwnerId, UserId, AssetId, ProviderParticipantId).
```

---

## LC-2 — DTOs para configuración de layout serializable

```
FASE LC-2 — DTOs para serialización del layout personalizado

OBJETIVO: Crear los DTOs que se serializan/deserializan dentro de ExplorerLayoutConfig.LayoutConfigJson.

UBICACIÓN: GreenTransit.Application/Features/EcoDataNet/DTOs/DataExplorer/

--- ARCHIVO 1: WidgetLayoutOverride.cs ---

Representa la personalización de un widget individual. Se serializa como array JSON
dentro de ExplorerLayoutConfig.LayoutConfigJson.

namespace GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;

/// <summary>
/// Personalización de un widget individual dentro del layout del Data Explorer.
/// Se almacena como JSON dentro de ExplorerLayoutConfig.LayoutConfigJson.
/// Solo contiene los OVERRIDES (diferencias respecto al layout generado automáticamente).
/// </summary>
public class WidgetLayoutOverride
{
    /// <summary>
    /// WidgetId del DynamicWidgetDescriptor original al que aplica este override.
    /// Se genera determinísticamente a partir del JsonPath del dato fuente
    /// para que sea estable entre ejecuciones del analizador.
    /// </summary>
    public string WidgetId { get; set; } = string.Empty;
    
    /// <summary>Orden personalizado (null = usar el automático).</summary>
    public int? CustomSortOrder { get; set; }
    
    /// <summary>Ancho personalizado en columnas 1-12 (null = usar automático).</summary>
    public int? CustomColumnSpan { get; set; }
    
    /// <summary>Título personalizado (null = usar el generado).</summary>
    public string? CustomTitle { get; set; }
    
    /// <summary>True si el usuario ha ocultado este widget.</summary>
    public bool IsHidden { get; set; }
    
    /// <summary>Tipo de gráfico personalizado (null = usar automático). Solo aplica a widgets Chart.</summary>
    public ChartSubType? CustomChartType { get; set; }
    
    /// <summary>Tipo de widget personalizado (null = usar automático).
    /// Permite convertir una tabla en gráfico o viceversa.</summary>
    public WidgetType? CustomWidgetType { get; set; }
}

--- ARCHIVO 2: LayoutConfigDto.cs ---

DTO de transporte entre UI y backend, incluye metadatos.

namespace GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;

/// <summary>
/// DTO completo de configuración de layout para transporte entre capas.
/// </summary>
public class LayoutConfigDto
{
    /// <summary>ID del registro en BD (0 si es nueva).</summary>
    public int Id { get; set; }
    
    /// <summary>AssetId del catálogo EDC.</summary>
    public string AssetId { get; set; } = string.Empty;
    
    /// <summary>ID del participante proveedor.</summary>
    public string ProviderParticipantId { get; set; } = string.Empty;
    
    /// <summary>Nombre descriptivo del dataset.</summary>
    public string? DatasetName { get; set; }
    
    /// <summary>Lista de overrides por widget.</summary>
    public List<WidgetLayoutOverride> Overrides { get; set; } = new();
    
    /// <summary>Hash del esquema cuando se guardó.</summary>
    public string? SchemaHash { get; set; }
    
    /// <summary>True si existe una configuración guardada para este asset.</summary>
    public bool HasSavedConfig { get; set; }
    
    /// <summary>Fecha de la última modificación.</summary>
    public DateTime? LastUpdated { get; set; }
}

--- ARCHIVO 3: Modificar DynamicWidgetDescriptor.cs ---

Añadir una propiedad para que el WidgetId sea DETERMINÍSTICO y estable entre ejecuciones.

CAMBIO: Eliminar la inicialización aleatoria del WidgetId:
  ANTES:  public string WidgetId { get; set; } = Guid.NewGuid().ToString("N")[..8];
  DESPUÉS: public string WidgetId { get; set; } = string.Empty;

CAMBIO: Añadir propiedad auxiliar:
  /// <summary>Ruta JSON del dato fuente (para generar WidgetId determinístico).</summary>
  public string SourceJsonPath { get; set; } = string.Empty;

--- ARCHIVO 4: Modificar DashboardLayoutBuilder.cs ---

Al crear cada DynamicWidgetDescriptor, generar el WidgetId de forma determinística:
- Para KpiCards: WidgetId = $"kpi_{HumanizePropertyName(property.Name).ToLowerInvariant().Replace(" ", "_")}"
  Ejemplo: "kpi_total_tons_processed"
- Para Charts de arrays: WidgetId = $"chart_{arrayName.ToLowerInvariant()}_{chartType.ToString().ToLowerInvariant()}"
  Ejemplo: "chart_waste_by_category_donut"
- Para DataTables: WidgetId = $"table_{arrayName.ToLowerInvariant()}"
  Ejemplo: "table_waste_by_category"
- Para SectionHeaders: WidgetId = "header_root"
- Para KeyValueLists: WidgetId = $"kvlist_{objectName.ToLowerInvariant()}"
- Para InfoText: WidgetId = $"info_{propertyName.ToLowerInvariant()}"

También asignar SourceJsonPath al JsonPath del dato fuente.

--- ARCHIVO 5: Modificar JsonSchemaAnalyzer.cs ---

Añadir un método para generar un hash MD5 del esquema detectado:

public string ComputeSchemaHash(JsonDataSchema schema)
{
    // Serializar solo la estructura (nombres de propiedades + tipos + arrays + paths)
    // sin los datos, para detectar cambios en la estructura del JSON.
    var structureSignature = new StringBuilder();
    foreach (var s in schema.RootScalars.OrderBy(p => p.Name))
        structureSignature.Append($"{s.Name}:{s.PropertyType}|");
    foreach (var a in schema.Arrays.OrderBy(a => a.Name))
    {
        structureSignature.Append($"[{a.Name}:");
        foreach (var p in a.ItemProperties.OrderBy(p => p.Name))
            structureSignature.Append($"{p.Name}:{p.PropertyType},");
        structureSignature.Append("]|");
    }
    foreach (var o in schema.NestedObjects.OrderBy(o => o.Name))
    {
        structureSignature.Append($"{{{o.Name}:");
        foreach (var p in o.Properties.OrderBy(p => p.Name))
            structureSignature.Append($"{p.Name}:{p.PropertyType},");
        structureSignature.Append("}|");
    }
    
    using var md5 = System.Security.Cryptography.MD5.Create();
    var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(structureSignature.ToString()));
    return Convert.ToHexString(hashBytes).ToLowerInvariant();
}

Añadir este método a la interfaz IJsonSchemaAnalyzer también.

VERIFICACIÓN: Compilar. Los WidgetIds ahora son determinísticos y estables.
```

---

## LC-3 — Query: Cargar configuración guardada

```
FASE LC-3 — GetExplorerLayoutConfigQuery

OBJETIVO: Query CQRS para cargar la configuración de layout guardada para un asset.

--- ARCHIVO 1: GreenTransit.Application/Features/EcoDataNet/Queries/GetExplorerLayoutConfigQuery.cs ---

public class GetExplorerLayoutConfigQuery : IRequest<LayoutConfigDto?>
{
    public string AssetId { get; set; } = string.Empty;
    public string ProviderParticipantId { get; set; } = string.Empty;
}

--- ARCHIVO 2: GreenTransit.Application/Features/EcoDataNet/Queries/GetExplorerLayoutConfigQueryHandler.cs ---

Handler que:
1. Obtiene OwnerId y UserId de ICurrentUserService.
2. Busca en ExplorerLayoutConfigs un registro que coincida con (OwnerId, UserId, AssetId, ProviderParticipantId).
3. Si no existe → devuelve null (el Data Explorer usará el layout automático).
4. Si existe → deserializa LayoutConfigJson a List<WidgetLayoutOverride> y construye LayoutConfigDto.
5. Usa System.Text.Json para deserialización. Si el JSON es inválido, loguea warning y devuelve null.

Inyectar: AppDbContext (readonly), ICurrentUserService, ILogger.

Multi-tenant: SIEMPRE filtrar por OwnerId incluso si el índice único ya incluye OwnerId.

VERIFICACIÓN: Compilar. Si no hay configuración guardada, devuelve null.
```

---

## LC-4 — Command: Guardar configuración

```
FASE LC-4 — SaveExplorerLayoutConfigCommand

OBJETIVO: Command CQRS para crear o actualizar la configuración de layout.

--- ARCHIVO 1: GreenTransit.Application/Features/EcoDataNet/Commands/SaveExplorerLayoutConfigCommand.cs ---

public class SaveExplorerLayoutConfigCommand : IRequest<int>
{
    public string AssetId { get; set; } = string.Empty;
    public string ProviderParticipantId { get; set; } = string.Empty;
    public string? DatasetName { get; set; }
    public List<WidgetLayoutOverride> Overrides { get; set; } = new();
    public string? SchemaHash { get; set; }
}

--- ARCHIVO 2: Handler ---

Lógica (patrón Upsert):
1. Obtener OwnerId y UserId de ICurrentUserService.
2. Buscar registro existente con (OwnerId, UserId, AssetId, ProviderParticipantId).
3. Serializar Overrides a JSON con System.Text.Json (opciones: WriteIndented=false, PropertyNamingPolicy=CamelCase).
4. Si existe → actualizar LayoutConfigJson, SchemaHash, DatasetName, UpdatedAt.
5. Si no existe → crear nuevo registro con todos los campos.
6. SaveChangesAsync.
7. Devolver el Id del registro (creado o actualizado).

--- ARCHIVO 3: Validator (FluentValidation) ---

GreenTransit.Application/Features/EcoDataNet/Validators/SaveExplorerLayoutConfigCommandValidator.cs

Reglas:
- AssetId: NotEmpty, MaximumLength(512).
- ProviderParticipantId: NotEmpty, MaximumLength(512).
- Overrides: NotNull. Cada override: WidgetId NotEmpty; si CustomColumnSpan tiene valor, debe estar entre 1-12; CustomTitle, si tiene valor, MaximumLength(256).
- DatasetName: MaximumLength(256) si tiene valor.

--- ARCHIVO 4: DeleteExplorerLayoutConfigCommand.cs ---

Command para eliminar una configuración (reset a automático):

public class DeleteExplorerLayoutConfigCommand : IRequest<bool>
{
    public string AssetId { get; set; } = string.Empty;
    public string ProviderParticipantId { get; set; } = string.Empty;
}

Handler:
1. Obtener OwnerId y UserId.
2. Buscar y eliminar el registro si existe.
3. Devolver true si se eliminó, false si no existía.

VERIFICACIÓN: Compilar. Crear un registro, leerlo, actualizarlo, eliminarlo — todo sin errores.
```

---

## LC-5 — Servicio: Merge de configuración con widgets generados

```
FASE LC-5 — ILayoutCustomizationService

OBJETIVO: Servicio que combina el layout generado automáticamente con la configuración guardada.

--- ARCHIVO 1: GreenTransit.Application/Features/EcoDataNet/Services/ILayoutCustomizationService.cs ---

namespace GreenTransit.Application.Features.EcoDataNet.Services;

public interface ILayoutCustomizationService
{
    /// <summary>
    /// Aplica los overrides guardados sobre la lista de widgets generados automáticamente.
    /// Maneja cambios de esquema: widgets nuevos se añaden al final, widgets obsoletos se ignoran.
    /// </summary>
    /// <param name="autoWidgets">Widgets generados por DashboardLayoutBuilder.</param>
    /// <param name="overrides">Overrides guardados (puede estar vacío).</param>
    /// <param name="savedSchemaHash">Hash del esquema cuando se guardaron los overrides.</param>
    /// <param name="currentSchemaHash">Hash del esquema actual del JSON.</param>
    /// <returns>Lista de widgets con overrides aplicados + flag de schema mismatch.</returns>
    LayoutMergeResult ApplyOverrides(
        List<DynamicWidgetDescriptor> autoWidgets,
        List<WidgetLayoutOverride> overrides,
        string? savedSchemaHash,
        string currentSchemaHash);
}

--- ARCHIVO 2: LayoutMergeResult.cs (en DTOs/DataExplorer/) ---

public class LayoutMergeResult
{
    /// <summary>Widgets con overrides aplicados, ordenados por SortOrder efectivo.</summary>
    public List<DynamicWidgetDescriptor> Widgets { get; set; } = new();
    
    /// <summary>True si el esquema del JSON ha cambiado desde la última vez que se guardó la configuración.</summary>
    public bool SchemaChanged { get; set; }
    
    /// <summary>Widgets nuevos que no existían cuando se guardó la configuración.</summary>
    public List<string> NewWidgetIds { get; set; } = new();
    
    /// <summary>WidgetIds de la configuración guardada que ya no existen en el JSON actual.</summary>
    public List<string> ObsoleteWidgetIds { get; set; } = new();
}

--- ARCHIVO 3: GreenTransit.Application/Features/EcoDataNet/Services/LayoutCustomizationService.cs ---

Implementación con esta lógica:

1. Si overrides está vacío → devolver autoWidgets tal cual, SchemaChanged=false.

2. Crear un diccionario de overrides por WidgetId para lookup rápido.

3. Detectar schema change: comparar savedSchemaHash con currentSchemaHash.

4. Para cada widget en autoWidgets:
   a. Buscar si existe un override con ese WidgetId.
   b. Si existe:
      - Si IsHidden=true: marcar el widget (añadir propiedad IsHidden al DynamicWidgetDescriptor, ver nota).
      - Si CustomSortOrder != null: aplicar al SortOrder.
      - Si CustomColumnSpan != null: aplicar al ColumnSpan.
      - Si CustomTitle != null: aplicar al Title.
      - Si CustomChartType != null y widget.Type==Chart: aplicar al ChartType.
      - Si CustomWidgetType != null: aplicar al Type (conversión de tipo).
   c. Si NO existe override: dejar el widget tal cual (es nuevo o nunca fue personalizado).

5. Identificar NewWidgetIds: widgets en autoWidgets que no tienen override.
6. Identificar ObsoleteWidgetIds: overrides cuyo WidgetId no coincide con ningún widget actual.

7. Ordenar por SortOrder efectivo (personalizado > automático).

8. Devolver LayoutMergeResult.

NOTA — Añadir propiedad IsHidden a DynamicWidgetDescriptor:
  /// <summary>True si el usuario ha ocultado este widget. La UI decide si lo oculta o lo muestra en gris.</summary>
  public bool IsHidden { get; set; }

Registrar en DI: services.AddTransient<ILayoutCustomizationService, LayoutCustomizationService>();

VERIFICACIÓN: 
- Si no hay overrides → los widgets salen idénticos al automático.
- Si se oculta un widget → su IsHidden=true.
- Si el esquema cambia → SchemaChanged=true y NewWidgetIds contiene los nuevos.
```

---

## LC-6 — UI: Barra de herramientas de personalización

```
FASE LC-6 — LayoutEditorToolbar.razor

OBJETIVO: Barra de herramientas que permite activar/desactivar el modo edición del layout.

UBICACIÓN: GreenTransit.Web/Components/Pages/EcoDataNet/DataExplorer/LayoutEditorToolbar.razor

COMPONENTE:

Parámetros:
[Parameter] public bool IsEditMode { get; set; }
[Parameter] public EventCallback<bool> IsEditModeChanged { get; set; }
[Parameter] public bool HasSavedConfig { get; set; }
[Parameter] public bool HasUnsavedChanges { get; set; }
[Parameter] public bool SchemaChanged { get; set; }
[Parameter] public EventCallback OnSave { get; set; }
[Parameter] public EventCallback OnReset { get; set; }
[Parameter] public EventCallback OnToggleHidden { get; set; }
[Parameter] public bool ShowHiddenWidgets { get; set; }

Layout:

<RadzenStack Orientation="Orientation.Horizontal" AlignItems="AlignItems.Center" 
             Gap="0.5rem" class="layout-editor-toolbar">
    
    @* Indicador de configuración guardada *@
    @if (HasSavedConfig)
    {
        <RadzenBadge Text="Layout personalizado" BadgeStyle="BadgeStyle.Info" />
    }
    
    @* Alerta si el esquema cambió *@
    @if (SchemaChanged)
    {
        <RadzenBadge Text="⚠ Estructura del asset modificada" BadgeStyle="BadgeStyle.Warning"
                     title="El JSON del asset tiene campos nuevos o eliminados desde la última personalización" />
    }
    
    @* Botón toggle modo edición *@
    <RadzenButton Text="@(IsEditMode ? "Finalizar edición" : "Personalizar layout")"
                  Icon="@(IsEditMode ? "check" : "dashboard_customize")"
                  ButtonStyle="@(IsEditMode ? ButtonStyle.Success : ButtonStyle.Secondary)"
                  Size="ButtonSize.Small"
                  Click="@(() => IsEditModeChanged.InvokeAsync(!IsEditMode))" />
    
    @if (IsEditMode)
    {
        @* Toggle mostrar/ocultar widgets ocultos *@
        <RadzenButton Text="@(ShowHiddenWidgets ? "Ocultar desactivados" : "Ver desactivados")"
                      Icon="@(ShowHiddenWidgets ? "visibility_off" : "visibility")"
                      ButtonStyle="ButtonStyle.Light" Size="ButtonSize.Small"
                      Click="OnToggleHidden" />
        
        @* Guardar *@
        <RadzenButton Text="Guardar" Icon="save"
                      ButtonStyle="ButtonStyle.Primary" Size="ButtonSize.Small"
                      Disabled="@(!HasUnsavedChanges)"
                      Click="OnSave" />
        
        @* Resetear *@
        <RadzenButton Text="Resetear" Icon="restart_alt"
                      ButtonStyle="ButtonStyle.Danger" Size="ButtonSize.Small"
                      Disabled="@(!HasSavedConfig)"
                      Click="OnReset"
                      title="Descartar la personalización y volver al layout automático" />
    }
</RadzenStack>

CSS del toolbar (.razor.css):
- .layout-editor-toolbar: background var(--rz-base-200), padding 0.5rem 1rem, border-radius 8px, margin-bottom 1rem, display flex, flex-wrap wrap.

VERIFICACIÓN: El componente renderiza sin errores.
```

---

## LC-7 — UI: Drag & Drop para reordenación

```
FASE LC-7 — Reordenación de widgets por arrastre

OBJETIVO: Implementar drag & drop nativo HTML5 para reordenar widgets en modo edición.

ENFOQUE: Usar los eventos nativos de HTML5 (dragstart, dragover, drop) con Blazor event handlers. 
NO instalar librerías externas de drag & drop.

CAMBIOS EN EdcDataExplorer.razor:

1. Cada widget wrapper div recibe atributos de drag cuando IsEditMode==true:

<div class="widget-wrapper @(IsEditMode ? "editable" : "") @(widget.IsHidden ? "hidden-widget" : "")"
     style="grid-column: span @widget.ColumnSpan"
     draggable="@(IsEditMode ? "true" : "false")"
     @ondragstart="() => OnDragStart(widget)"
     @ondragover:preventDefault
     @ondragenter="() => OnDragEnter(widget)"
     @ondrop="() => OnDrop(widget)">
    
    @if (IsEditMode)
    {
        <div class="drag-handle">
            <RadzenIcon Icon="drag_indicator" Style="cursor:grab; color:var(--gt-stone-green)" />
        </div>
    }
    
    @* Renderizado del widget según tipo (switch existente) *@
    ...
</div>

2. Variables de estado para drag & drop:

private DynamicWidgetDescriptor? _draggedWidget;
private DynamicWidgetDescriptor? _dragOverWidget;

3. Métodos:

private void OnDragStart(DynamicWidgetDescriptor widget)
{
    _draggedWidget = widget;
}

private void OnDragEnter(DynamicWidgetDescriptor widget)
{
    if (_draggedWidget == null || _draggedWidget == widget) return;
    _dragOverWidget = widget;
}

private void OnDrop(DynamicWidgetDescriptor targetWidget)
{
    if (_draggedWidget == null || _draggedWidget == targetWidget) return;
    
    // Intercambiar SortOrder entre los dos widgets
    var visibleWidgets = _currentWidgets.Where(w => !w.IsHidden || _showHiddenWidgets).OrderBy(w => w.SortOrder).ToList();
    var draggedIndex = visibleWidgets.IndexOf(_draggedWidget);
    var targetIndex = visibleWidgets.IndexOf(targetWidget);
    
    if (draggedIndex < 0 || targetIndex < 0) return;
    
    // Reasignar SortOrder secuencialmente tras mover
    visibleWidgets.RemoveAt(draggedIndex);
    visibleWidgets.Insert(targetIndex, _draggedWidget);
    for (int i = 0; i < visibleWidgets.Count; i++)
        visibleWidgets[i].SortOrder = i * 10;
    
    _draggedWidget = null;
    _dragOverWidget = null;
    _hasUnsavedChanges = true;
    StateHasChanged();
}

4. CSS para feedback visual:

.widget-wrapper.editable {
    position: relative;
    border: 2px dashed transparent;
    transition: border-color 0.2s, opacity 0.2s;
}
.widget-wrapper.editable:hover {
    border-color: var(--gt-secondary-1);
}
.widget-wrapper.hidden-widget {
    opacity: 0.4;
    border-color: var(--gt-secondary-5);
}
.drag-handle {
    position: absolute;
    top: 4px;
    left: 4px;
    z-index: 10;
    background: var(--rz-base-100);
    border-radius: 4px;
    padding: 2px;
}

VERIFICACIÓN: En modo edición, los widgets muestran handle de arrastre y pueden reordenarse.
```

---

## LC-8 — UI: Panel de configuración por widget

```
FASE LC-8 — WidgetConfigPanel.razor

OBJETIVO: Panel desplegable que aparece en cada widget en modo edición con opciones de personalización.

UBICACIÓN: GreenTransit.Web/Components/Pages/EcoDataNet/DataExplorer/WidgetConfigPanel.razor

PARÁMETROS:
[Parameter] public DynamicWidgetDescriptor Widget { get; set; } = null!;
[Parameter] public EventCallback<DynamicWidgetDescriptor> OnWidgetChanged { get; set; }
[Parameter] public EventCallback<string> OnWidgetHidden { get; set; }

ESTRUCTURA:

Un botón de engranaje en la esquina superior derecha del widget que al pulsar despliega un mini-panel
(RadzenPanel o div posicionado absolutamente):

<div class="widget-config-trigger">
    <RadzenButton Icon="settings" Size="ButtonSize.ExtraSmall" 
                  ButtonStyle="ButtonStyle.Light" Variant="Variant.Text"
                  Click="@(() => _showConfig = !_showConfig)" />
</div>

@if (_showConfig)
{
    <div class="widget-config-panel">
        @* Título editable *@
        <RadzenFormField Text="Título" Variant="Variant.Text" Style="width:100%">
            <ChildContent>
                <RadzenTextBox @bind-Value="Widget.Title" Style="width:100%"
                               Change="@(() => NotifyChange())" />
            </ChildContent>
        </RadzenFormField>
        
        @* Ancho (ColumnSpan) *@
        <RadzenFormField Text="Ancho" Variant="Variant.Text" Style="width:100%">
            <ChildContent>
                <RadzenDropDown @bind-Value="Widget.ColumnSpan" Data="@_columnSpanOptions"
                                TextProperty="Label" ValueProperty="Value"
                                Style="width:100%" Change="@(() => NotifyChange())" />
            </ChildContent>
        </RadzenFormField>
        
        @* Tipo de gráfico (solo si es Chart) *@
        @if (Widget.Type == WidgetType.Chart)
        {
            <RadzenFormField Text="Tipo de gráfico" Variant="Variant.Text" Style="width:100%">
                <ChildContent>
                    <RadzenDropDown @bind-Value="Widget.ChartType" 
                                    Data="@_chartTypeOptions"
                                    TextProperty="Label" ValueProperty="Value"
                                    Style="width:100%" Change="@(() => NotifyChange())" />
                </ChildContent>
            </RadzenFormField>
        }
        
        @* Botón ocultar *@
        <RadzenButton Text="Ocultar widget" Icon="visibility_off"
                      ButtonStyle="ButtonStyle.Danger" Size="ButtonSize.Small"
                      Variant="Variant.Outlined" Style="width:100%; margin-top:0.5rem"
                      Click="@(() => OnWidgetHidden.InvokeAsync(Widget.WidgetId))" />
    </div>
}

Opciones de ancho:
private readonly List<dynamic> _columnSpanOptions = new()
{
    new { Label = "25% (3 columnas)", Value = 3 },
    new { Label = "33% (4 columnas)", Value = 4 },
    new { Label = "50% (6 columnas)", Value = 6 },
    new { Label = "100% (12 columnas)", Value = 12 }
};

Opciones de gráfico:
private readonly List<dynamic> _chartTypeOptions = new()
{
    new { Label = "Barras verticales", Value = ChartSubType.BarVertical },
    new { Label = "Barras horizontales", Value = ChartSubType.BarHorizontal },
    new { Label = "Líneas", Value = ChartSubType.Line },
    new { Label = "Área", Value = ChartSubType.Area },
    new { Label = "Donut", Value = ChartSubType.Donut },
    new { Label = "Tarta", Value = ChartSubType.Pie }
};

CSS (.razor.css):
.widget-config-trigger { position:absolute; top:4px; right:4px; z-index:10; }
.widget-config-panel { 
    position:absolute; top:36px; right:4px; z-index:20;
    background:var(--rz-base-100); border:1px solid var(--rz-base-300);
    border-radius:8px; padding:0.75rem; min-width:220px;
    box-shadow: 0 4px 12px rgba(0,0,0,0.15);
}

VERIFICACIÓN: En modo edición, cada widget muestra el botón de engranaje. Al pulsar, aparece el panel de configuración.
```

---

## LC-9 — Integración: Modificar EdcDataExplorer.razor

```
FASE LC-9 — Integración completa en EdcDataExplorer.razor

OBJETIVO: Modificar la página principal para soportar modo edición, carga/guardado de configuración,
y aplicación de overrides.

CAMBIOS PRINCIPALES EN EdcDataExplorer.razor:

1. INYECCIONES NUEVAS:
   @inject IMediator Mediator  // (si no estaba ya)

2. VARIABLES DE ESTADO NUEVAS:
   private bool _isEditMode;
   private bool _hasUnsavedChanges;
   private bool _showHiddenWidgets;
   private bool _schemaChanged;
   private bool _hasSavedConfig;
   private string? _currentSchemaHash;
   private string? _savedSchemaHash;
   private LayoutConfigDto? _savedConfig;
   private List<DynamicWidgetDescriptor> _currentWidgets = new();
   private List<DynamicWidgetDescriptor> _autoGeneratedWidgets = new(); // backup del layout automático

3. AMPLIAR EdcDataExplorerStateService — añadir propiedades:
   public string? AssetId { get; set; }
   public string? ProviderParticipantId { get; set; }

   Y en ConsumeData.razor, al navegar al Data Explorer, rellenar estos campos
   desde la selección EDC (EdcNegotiationSelection.SelectedDatasetId y ProviderParticipantId).

4. LÓGICA MODIFICADA EN OnInitializedAsync:

   a. Obtener JSON y ejecutar AnalyzeEdcDataQuery (sin cambios).
   b. Guardar los widgets automáticos: _autoGeneratedWidgets = result.Widgets.Select(DeepClone).ToList();
   c. Calcular el hash del esquema: _currentSchemaHash = _schemaAnalyzer.ComputeSchemaHash(result.Schema);
   d. Si hay AssetId y ProviderParticipantId disponibles en el estado:
      - Ejecutar GetExplorerLayoutConfigQuery para buscar config guardada.
      - Si existe config guardada:
        · _hasSavedConfig = true
        · _savedSchemaHash = config.SchemaHash
        · Inyectar ILayoutCustomizationService y ejecutar ApplyOverrides(autoWidgets, config.Overrides, savedSchemaHash, currentSchemaHash)
        · _schemaChanged = mergeResult.SchemaChanged
        · _currentWidgets = mergeResult.Widgets
      - Si NO existe config guardada:
        · _currentWidgets = result.Widgets (comportamiento actual)

5. MÉTODO SAVE:

   private async Task SaveLayout()
   {
       // Construir los overrides comparando _currentWidgets con _autoGeneratedWidgets
       var overrides = new List<WidgetLayoutOverride>();
       foreach (var widget in _currentWidgets)
       {
           var auto = _autoGeneratedWidgets.FirstOrDefault(w => w.WidgetId == widget.WidgetId);
           if (auto == null) continue; // widget obsoleto, ignorar
           
           var ov = new WidgetLayoutOverride { WidgetId = widget.WidgetId };
           bool hasChanges = false;
           
           if (widget.SortOrder != auto.SortOrder) { ov.CustomSortOrder = widget.SortOrder; hasChanges = true; }
           if (widget.ColumnSpan != auto.ColumnSpan) { ov.CustomColumnSpan = widget.ColumnSpan; hasChanges = true; }
           if (widget.Title != auto.Title) { ov.CustomTitle = widget.Title; hasChanges = true; }
           if (widget.IsHidden) { ov.IsHidden = true; hasChanges = true; }
           if (widget.Type == WidgetType.Chart && widget.ChartType != auto.ChartType) 
               { ov.CustomChartType = widget.ChartType; hasChanges = true; }
           
           if (hasChanges) overrides.Add(ov);
       }
       
       await Mediator.Send(new SaveExplorerLayoutConfigCommand
       {
           AssetId = DataExplorerState.AssetId!,
           ProviderParticipantId = DataExplorerState.ProviderParticipantId!,
           DatasetName = DataExplorerState.DatasetName,
           Overrides = overrides,
           SchemaHash = _currentSchemaHash
       });
       
       _hasUnsavedChanges = false;
       _hasSavedConfig = true;
       _schemaChanged = false;
       _savedSchemaHash = _currentSchemaHash;
       
       // Mostrar notificación de éxito con RadzenNotificationService
   }

6. MÉTODO RESET:

   private async Task ResetLayout()
   {
       // Confirmar con diálogo
       var confirmed = await DialogService.Confirm(
           "¿Deseas descartar la personalización y volver al layout automático?",
           "Resetear layout",
           new ConfirmOptions { OkButtonText = "Sí, resetear", CancelButtonText = "Cancelar" });
       
       if (confirmed != true) return;
       
       await Mediator.Send(new DeleteExplorerLayoutConfigCommand
       {
           AssetId = DataExplorerState.AssetId!,
           ProviderParticipantId = DataExplorerState.ProviderParticipantId!
       });
       
       _currentWidgets = _autoGeneratedWidgets.Select(DeepClone).ToList();
       _hasSavedConfig = false;
       _hasUnsavedChanges = false;
       _schemaChanged = false;
       _isEditMode = false;
   }

7. MÉTODO HIDE WIDGET:

   private void HideWidget(string widgetId)
   {
       var widget = _currentWidgets.FirstOrDefault(w => w.WidgetId == widgetId);
       if (widget != null)
       {
           widget.IsHidden = true;
           _hasUnsavedChanges = true;
       }
   }

8. MÉTODO UNHIDE (restaurar widget oculto):

   Al hacer clic en un widget oculto en modo _showHiddenWidgets:
   widget.IsHidden = false; _hasUnsavedChanges = true;

9. HELPER DeepClone:

   private static DynamicWidgetDescriptor DeepClone(DynamicWidgetDescriptor source)
   {
       var json = System.Text.Json.JsonSerializer.Serialize(source);
       return System.Text.Json.JsonSerializer.Deserialize<DynamicWidgetDescriptor>(json)!;
   }

10. RENDERIZADO — En el bucle de widgets, añadir filtro:

    @foreach (var widget in _currentWidgets
        .Where(w => !w.IsHidden || (_isEditMode && _showHiddenWidgets))
        .OrderBy(w => w.SortOrder))
    {
        <div class="widget-wrapper ..." ...>
            @if (_isEditMode)
            {
                <WidgetConfigPanel Widget="widget" 
                                   OnWidgetChanged="OnWidgetEdited"
                                   OnWidgetHidden="HideWidget" />
            }
            @* Renderizado según tipo *@
        </div>
    }

11. TOOLBAR — Añadir justo encima del grid de widgets:

    <LayoutEditorToolbar @bind-IsEditMode="_isEditMode"
                         HasSavedConfig="_hasSavedConfig"
                         HasUnsavedChanges="_hasUnsavedChanges"
                         SchemaChanged="_schemaChanged"
                         ShowHiddenWidgets="_showHiddenWidgets"
                         OnSave="SaveLayout"
                         OnReset="ResetLayout"
                         OnToggleHidden="@(() => { _showHiddenWidgets = !_showHiddenWidgets; StateHasChanged(); })" />

12. MODIFICAR ConsumeData.razor — En NavigateToDataExplorer, añadir AssetId y ProviderParticipantId:

    DataExplorerState.AssetId = _selectedDatasetId;              // de EdcNegotiationSelection.SelectedDatasetId
    DataExplorerState.ProviderParticipantId = _selectedProviderId; // de EdcNegotiationSelection.ProviderParticipantId

    Adaptar los nombres de variables a los reales del código existente.

VERIFICACIÓN:
- Sin config guardada: el dashboard se muestra como antes (automático).
- Pulsar "Personalizar layout" → modo edición activo, handles de arrastre visibles, engranajes en cada widget.
- Cambiar título, ancho, tipo de gráfico → _hasUnsavedChanges = true.
- Arrastrar widgets → se reordenan.
- Ocultar un widget → desaparece (o se muestra en gris si "Ver desactivados" está activo).
- Pulsar "Guardar" → se persiste en BD.
- Recargar la página con el mismo AssetId → el layout personalizado se carga automáticamente.
- Pulsar "Resetear" → se elimina la config y vuelve al automático.
- Si el JSON del asset tiene campos nuevos → badge de advertencia y los widgets nuevos aparecen al final.
```

---

## LC-10 — Tests unitarios

```
FASE LC-10 — Tests unitarios

UBICACIÓN: GreenTransit.Tests/Features/EcoDataNet/DataExplorer/

--- ARCHIVO 1: LayoutCustomizationServiceTests.cs ---

1. ApplyOverrides_NoOverrides_ReturnsAutoWidgets:
   Assert: Widgets idénticos, SchemaChanged=false.

2. ApplyOverrides_HiddenWidget_SetsIsHidden:
   Override con IsHidden=true para un WidgetId.
   Assert: widget.IsHidden == true.

3. ApplyOverrides_CustomSortOrder_ReordersWidgets:
   Override que cambia SortOrder.
   Assert: widgets reordenados.

4. ApplyOverrides_CustomTitle_OverridesTitle:
   Override con CustomTitle = "Mi título".
   Assert: widget.Title == "Mi título".

5. ApplyOverrides_SchemaChanged_DetectsNewWidgets:
   Overrides basados en esquema viejo, widgets actuales incluyen uno nuevo.
   Assert: SchemaChanged=true, NewWidgetIds contiene el nuevo.

6. ApplyOverrides_ObsoleteOverride_Ignored:
   Override para un WidgetId que ya no existe.
   Assert: ObsoleteWidgetIds contiene el ID, widgets no afectados.

7. ApplyOverrides_CustomChartType_ChangesChartType:
   Override con CustomChartType = ChartSubType.Donut sobre un widget que era BarVertical.
   Assert: widget.ChartType == ChartSubType.Donut.

--- ARCHIVO 2: SaveExplorerLayoutConfigCommandTests.cs ---

Usar InMemoryDatabase para EF Core.

1. Save_NewConfig_CreatesRecord:
   Assert: registro creado con el AssetId correcto.

2. Save_ExistingConfig_UpdatesRecord:
   Crear un registro, luego enviar update con nuevos overrides.
   Assert: solo 1 registro, LayoutConfigJson actualizado.

3. Delete_ExistingConfig_RemovesRecord:
   Crear y luego eliminar.
   Assert: count == 0.

4. Delete_NonExistent_ReturnsFalse:
   Assert: resultado == false.

--- ARCHIVO 3: SchemaHashTests.cs ---

1. ComputeSchemaHash_SameSchema_SameHash:
   Dos JSONs con misma estructura pero datos diferentes.
   Assert: hashes iguales.

2. ComputeSchemaHash_DifferentSchema_DifferentHash:
   Dos JSONs con estructura diferente.
   Assert: hashes distintos.

3. ComputeSchemaHash_PropertyOrder_DoesNotAffectHash:
   Mismo JSON con propiedades en distinto orden.
   Assert: hashes iguales (porque el hash ordena por nombre).

VERIFICACIÓN: Todos los tests pasan. dotnet test sin errores.
```

---

## 📁 RESUMEN DE ARCHIVOS

| Capa | Archivo | Acción |
|------|---------|--------|
| Domain | `Entities/ExplorerLayoutConfig.cs` | **CREAR** |
| Infrastructure | `Persistence/Configurations/ExplorerLayoutConfigConfiguration.cs` | **CREAR** |
| Infrastructure | `Persistence/AppDbContext.cs` | **MODIFICAR** (añadir DbSet) |
| Infrastructure | Migración EF Core `AddExplorerLayoutConfigs` | **CREAR** |
| Application | `Features/EcoDataNet/DTOs/DataExplorer/WidgetLayoutOverride.cs` | **CREAR** |
| Application | `Features/EcoDataNet/DTOs/DataExplorer/LayoutConfigDto.cs` | **CREAR** |
| Application | `Features/EcoDataNet/DTOs/DataExplorer/LayoutMergeResult.cs` | **CREAR** |
| Application | `Features/EcoDataNet/DTOs/DataExplorer/DynamicWidgetDescriptor.cs` | **MODIFICAR** (WidgetId determinístico + IsHidden + SourceJsonPath) |
| Application | `Features/EcoDataNet/Services/IJsonSchemaAnalyzer.cs` | **MODIFICAR** (añadir ComputeSchemaHash) |
| Application | `Features/EcoDataNet/Services/JsonSchemaAnalyzer.cs` | **MODIFICAR** (implementar ComputeSchemaHash) |
| Application | `Features/EcoDataNet/Services/DashboardLayoutBuilder.cs` | **MODIFICAR** (WidgetId determinístico) |
| Application | `Features/EcoDataNet/Services/ILayoutCustomizationService.cs` | **CREAR** |
| Application | `Features/EcoDataNet/Services/LayoutCustomizationService.cs` | **CREAR** |
| Application | `Features/EcoDataNet/Services/EdcDataExplorerStateService.cs` | **MODIFICAR** (añadir AssetId, ProviderParticipantId) |
| Application | `Features/EcoDataNet/Queries/GetExplorerLayoutConfigQuery.cs` | **CREAR** |
| Application | `Features/EcoDataNet/Queries/GetExplorerLayoutConfigQueryHandler.cs` | **CREAR** |
| Application | `Features/EcoDataNet/Commands/SaveExplorerLayoutConfigCommand.cs` | **CREAR** |
| Application | `Features/EcoDataNet/Commands/SaveExplorerLayoutConfigCommandHandler.cs` | **CREAR** |
| Application | `Features/EcoDataNet/Commands/DeleteExplorerLayoutConfigCommand.cs` | **CREAR** |
| Application | `Features/EcoDataNet/Commands/DeleteExplorerLayoutConfigCommandHandler.cs` | **CREAR** |
| Application | `Features/EcoDataNet/Validators/SaveExplorerLayoutConfigCommandValidator.cs` | **CREAR** |
| Web | `Components/Pages/EcoDataNet/DataExplorer/LayoutEditorToolbar.razor` | **CREAR** |
| Web | `Components/Pages/EcoDataNet/DataExplorer/WidgetConfigPanel.razor` | **CREAR** |
| Web | `Components/Pages/EcoDataNet/DataExplorer/EdcDataExplorer.razor` | **MODIFICAR** (modo edición + drag&drop + persistencia) |
| Web | `Components/Pages/EcoDataNet/ConsumeData.razor` | **MODIFICAR** (pasar AssetId + ProviderParticipantId) |
| Web | `Program.cs` o equivalente | **MODIFICAR** (registrar DI nuevos servicios) |
| Tests | `Features/EcoDataNet/DataExplorer/LayoutCustomizationServiceTests.cs` | **CREAR** |
| Tests | `Features/EcoDataNet/DataExplorer/SaveExplorerLayoutConfigCommandTests.cs` | **CREAR** |
| Tests | `Features/EcoDataNet/DataExplorer/SchemaHashTests.cs` | **CREAR** |

---

## 🔄 ESTRATEGIA DE EJECUCIÓN

1. **Sesión 1**: LC-0 + LC-1 + LC-2 (contexto + modelo de datos + DTOs + modificar WidgetId).
2. **Sesión 2**: LC-3 + LC-4 (CQRS lectura + escritura + validator + DI).
3. **Sesión 3**: LC-5 (servicio de merge).
4. **Sesión 4**: LC-6 + LC-7 + LC-8 (UI: toolbar + drag&drop + panel de config).
5. **Sesión 5**: LC-9 (integración completa en EdcDataExplorer + ConsumeData).
6. **Sesión 6**: LC-10 (tests).

> Al inicio de cada sesión, adjuntar este archivo + `COPILOT_CONTEXT.md` + `Mapa_Funcionalidades.md`.
> En la sesión 1, adjuntar también `Crear_BD_v4_1.sql` y `AppDbContext.cs`.
> En las sesiones 4-5, adjuntar los archivos de la capa Application creados en sesiones anteriores + `EdcDataExplorer.razor` + `ConsumeData.razor`.

---

## ⚠️ NOTAS IMPORTANTES

1. **WidgetId determinístico es CRÍTICO**: si los WidgetIds cambian entre ejecuciones del analizador, los overrides guardados no podrán vincularse a los widgets correctos. El WidgetId debe generarse siempre igual para la misma propiedad/array del JSON, independientemente del orden de procesamiento.

2. **Merge robusto ante cambios de esquema**: cuando el JSON de un asset cambia (el proveedor añade/elimina campos), el servicio de merge debe: (a) mantener los overrides válidos, (b) mostrar los widgets nuevos con layout automático al final, (c) ignorar overrides de widgets que ya no existen, (d) notificar al usuario con el badge de advertencia.

3. **Serialización del LayoutConfigJson**: usar `JsonSerializerOptions` con `PropertyNamingPolicy = JsonNamingPolicy.CamelCase` y `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull` para minimizar el tamaño del JSON almacenado. Solo se guardan los campos con valor.

4. **Límite de tamaño**: el LayoutConfigJson típico será pequeño (< 10 KB incluso con 50 widgets). El campo `nvarchar(max)` es suficiente y no hay riesgo de rendimiento.

5. **Multi-tenant estricto**: la configuración es por OwnerId + UserId + AssetId + ProviderParticipantId. Un usuario no puede ver ni modificar configuraciones de otro usuario ni de otro tenant. El handler SIEMPRE filtra por OwnerId del ICurrentUserService.

6. **Drag & Drop en Blazor Server**: los eventos HTML5 drag nativos funcionan bien en Blazor Server porque son eventos del DOM que se manejan con JavaScript interop transparente. No se necesita JSRuntime manual. Puede haber un ligero lag en conexiones lentas — aceptable para esta funcionalidad.

7. **No implementar en esta fase**: exportar/importar configuraciones entre usuarios, plantillas de layout compartidas, ni undo/redo. Son mejoras futuras.

8. **Responsive**: en móvil (< 768px), el modo edición debe desactivarse o mostrar un aviso de que la personalización está disponible solo en escritorio. El drag & drop no funciona bien en touch — es aceptable como limitación.

9. **La tabla ExplorerLayoutConfigs NO necesita OwnerId como FK**: OwnerId es un Guid que viene del claim OIDC, no de la tabla Entities. Se usa como filtro de aislamiento, no como foreign key.

10. **PagePermissions**: la nueva tabla ExplorerLayoutConfigs NO necesita entrada en PageDefinitions — no es una pantalla nueva, es persistencia interna de la funcionalidad existente del Data Explorer.
