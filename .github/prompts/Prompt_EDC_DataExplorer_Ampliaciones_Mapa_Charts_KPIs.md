# 🤖 Prompt para GitHub Copilot — EDC Data Explorer: Ampliaciones — Gráfico Mapa, Charts Adicionales y KPIs Calculados

> **Instrucción**: Copia este prompt completo en GitHub Copilot Chat adjuntando los archivos `COPILOT_CONTEXT.md`, `Mapa_Funcionalidades.md`, `Prompt_EDC_DataExplorer_Dashboard_Dinamico.md`, `Prompt_EDC_DataExplorer_Layout_Customization.md`, `Prompt_EDC_DataExplorer_Chart_DataBinding_Customization.md`, `DynamicWidgetDescriptor.cs`, `WidgetLayoutOverride.cs`, `LayoutConfigDto.cs`, `LayoutMergeResult.cs`, `DashboardLayoutBuilder.cs`, `JsonSchemaAnalyzer.cs`, `LayoutCustomizationService.cs`, `WidgetConfigPanel.razor`, `LayoutEditorToolbar.razor`, `EdcDataExplorer.razor`, `DynamicChart.razor`, `SaveExplorerLayoutConfigCommand.cs`, `SaveExplorerLayoutConfigCommandValidator.cs` y `EdcDataExplorerStateService.cs`.
>
> **Stack**: .NET 10 · Clean Architecture (Domain / Application / Infrastructure / Web / Tests) · Blazor Server · Radzen Blazor Components · EF Core · MediatR · FluentValidation · System.Text.Json.
>
> **Prerequisitos**: Los tres módulos anteriores del Data Explorer deben estar completamente implementados:
> 1. **Dashboard Dinámico** (`Prompt_EDC_DataExplorer_Dashboard_Dinamico.md`) — análisis JSON, widgets automáticos, renderizado.
> 2. **Personalización de Layout** (`Prompt_EDC_DataExplorer_Layout_Customization.md`) — modo edición, drag & drop, persistencia en `ExplorerLayoutConfigs`.
> 3. **Personalización de Data Binding** (`Prompt_EDC_DataExplorer_Chart_DataBinding_Customization.md`) — selectores de campos, redimensión de columnas.
>
> **Ejecuta los prompts en orden**. Cada fase debe compilar antes de pasar a la siguiente.

---

## 📋 ÍNDICE Y ESTADO

| ID | Fase | Descripción | Estado |
|---|---|---|:-:|
| AMP-0 | Contexto | Instrucción base — NO genera código | ⬜ |
| **— Ampliación A: Widget Mapa —** | | | |
| MAP-1 | DTOs | Ampliar enums y `DynamicWidgetDescriptor` con tipo `Map` y config de mapa | ⬜ |
| MAP-2 | Heurística | Ampliar `JsonSchemaAnalyzer` para detectar lat/lon en arrays | ⬜ |
| MAP-3 | LayoutBuilder | Ampliar `DashboardLayoutBuilder` para generar widget `Map` por heurística | ⬜ |
| MAP-4 | Override | Ampliar `WidgetLayoutOverride` con configuración de mapa persistible | ⬜ |
| MAP-5 | MergeService | Ampliar `LayoutCustomizationService` para aplicar overrides de mapa | ⬜ |
| MAP-6 | UI Renderizado | Crear `DynamicMap.razor` — componente de mapa con interop JS | ⬜ |
| MAP-7 | UI Config | Ampliar `WidgetConfigPanel.razor` con selectores lat/lon/tooltip | ⬜ |
| MAP-8 | Tests | Tests unitarios de detección lat/lon y generación de widget Map | ⬜ |
| **— Ampliación B: Charts Adicionales Creados por Usuario —** | | | |
| CHART-ADD-1 | DTOs | Ampliar `LayoutConfigDto` / modelo con `CustomWidgets[]` | ⬜ |
| CHART-ADD-2 | UI Toolbar | Botón "Añadir gráfico" en `LayoutEditorToolbar.razor` | ⬜ |
| CHART-ADD-3 | UI Diálogo | Diálogo/panel de creación de gráfico nuevo | ⬜ |
| CHART-ADD-4 | Merge | Ampliar `LayoutCustomizationService` para inyectar custom widgets | ⬜ |
| CHART-ADD-5 | Persistencia | Ampliar lógica de `SaveLayout()` y validador | ⬜ |
| CHART-ADD-6 | Tests | Tests unitarios de charts creados por usuario | ⬜ |
| **— Ampliación C: KPIs Calculados por Usuario —** | | | |
| KPI-USER-1 | DTOs | Crear `CustomKpiDefinition` con operaciones y formato | ⬜ |
| KPI-USER-2 | Servicio | Crear `ICustomKpiCalculator` + `CustomKpiCalculator` | ⬜ |
| KPI-USER-3 | UI Toolbar | Botón "Añadir KPI" en `LayoutEditorToolbar.razor` | ⬜ |
| KPI-USER-4 | UI Diálogo | Diálogo/panel de creación de KPI calculado | ⬜ |
| KPI-USER-5 | Merge + Render | Inyectar KPIs calculados en el dashboard y renderizarlos | ⬜ |
| KPI-USER-6 | Persistencia | Ampliar lógica de guardado y validador | ⬜ |
| KPI-USER-7 | Tests | Tests unitarios de cálculo de KPIs | ⬜ |
| **— Cierre —** | | | |
| COMPAT-1 | Compatibilidad | Verificación de retrocompatibilidad y migración de JSON antiguo | ⬜ |

---

## 🎯 OBJETIVO GENERAL

El Data Explorer ya genera dashboards dinámicos desde JSON con widgets automáticos (KPI cards, tablas, gráficos), personalización de layout (reordenar, ocultar, renombrar, cambiar tipo/ancho), y personalización de data binding (campos de gráficos, columnas de tablas). Todo persiste en `ExplorerLayoutConfigs.LayoutConfigJson`.

Ahora se implementan **tres ampliaciones funcionales**:

**(A) Gráfico Mapa** — nuevo tipo de widget `Map` que renderiza puntos geográficos cuando el JSON contiene coordenadas lat/lon. Configurable (campos lat, lon, tooltip) y persistible.

**(B) Charts Adicionales** — el usuario puede crear gráficos extra en modo edición, eligiendo fuente de datos (array del JSON), tipo de gráfico y campos. Se guardan como "custom widgets" en el `LayoutConfigJson`.

**(C) KPIs Calculados** — el usuario puede crear KPIs con operaciones (SUM, COUNT, AVG, porcentaje parte/total) sobre campos numéricos de arrays del JSON. Se guardan y renderizan como KPI cards personalizadas.

**Principios invariables**:
- NO se crean nuevas tablas. Todo se persiste ampliando el JSON dentro de `ExplorerLayoutConfigs.LayoutConfigJson`.
- Clean Architecture: interfaces en Application, implementaciones en Application (lógica pura) o Infrastructure (si requiere I/O).
- CQRS/MediatR: se reutilizan `GetExplorerLayoutConfigQuery`, `SaveExplorerLayoutConfigCommand`, `DeleteExplorerLayoutConfigCommand`.
- Multi-tenant: filtro por `OwnerId` en todos los handlers.
- Retrocompatibilidad total: si un asset no tiene lat/lon → sin mapa. Si no hay custom widgets → layout automático intacto. Si hay JSON antiguo sin los nuevos campos → deserialización sin errores (campos nullable).

---

## AMP-0 — Instrucción base (contexto)

```
CONTEXTO DEL PROYECTO:
- Proyecto GreenTransit — .NET 10, Blazor Web App (Server), Radzen Blazor Components, EF Core, SQL Server Azure.
- Clean Architecture: GreenTransit.Domain / Application / Infrastructure / Web / Tests.
- MediatR, FluentValidation, Serilog, xUnit ya configurados.
- Módulo "EDC Data Explorer" COMPLETAMENTE IMPLEMENTADO con:
  · IJsonSchemaAnalyzer analiza JSON → JsonDataSchema (RootScalars, Arrays con JsonArrayDescriptor.ItemProperties, NestedObjects).
  · IDashboardLayoutBuilder genera List<DynamicWidgetDescriptor> con heurísticas.
  · DynamicWidgetDescriptor tiene: WidgetId (determinístico), Type (WidgetType), ChartType (ChartSubType), ChartCategoryField, ChartValueFields, ChartData, AvailableCategoryFields, AvailableValueFields, FieldDisplayNames, SourceArrayName, TableColumns, TableData, KpiValue, KpiNumericValue, etc.
  · WidgetType enum: KpiCard, DataTable, Chart, SectionHeader, KeyValueList, InfoText.
  · ChartSubType enum: BarVertical, BarHorizontal, Line, Area, Donut, Pie.
  · WidgetLayoutOverride: WidgetId, CustomSortOrder?, CustomColumnSpan?, CustomTitle?, IsHidden, CustomChartType?, CustomWidgetType?, CustomChartBinding? (ChartFieldBinding), CustomTableColumns? (List<TableColumnOverride>).
  · LayoutCustomizationService.ApplyOverrides() aplica overrides sobre autoWidgets.
  · WidgetConfigPanel.razor permite editar título, ancho, tipo de gráfico, campos de binding (categoría/valor), columnas de tabla.
  · EdcDataExplorer.razor compara _currentWidgets vs _autoGeneratedWidgets para construir overrides en SaveLayout().
  · ExplorerLayoutConfigs.LayoutConfigJson persiste overrides como JSON serializado (nvarchar(max)).
  · Serialización con JsonNamingPolicy.CamelCase y JsonIgnoreCondition.WhenWritingNull.

OBJETIVO:
Implementar 3 ampliaciones: (A) Widget Mapa con lat/lon, (B) Charts adicionales creados por usuario, (C) KPIs calculados configurables. Todo persistible en LayoutConfigJson sin nuevas tablas.

REGLAS GENERALES:
1. NO crear nuevas tablas ni entidades de dominio.
2. Ampliar WidgetLayoutOverride y LayoutConfigDto con campos opcionales para las 3 ampliaciones.
3. Toda la nueva configuración se serializa dentro del LayoutConfigJson existente.
4. Los nuevos widgets creados por usuario (charts + KPIs) se persisten como una lista separada (CustomWidgets) dentro del LayoutConfigJson — NO como overrides de widgets autogenerados.
5. Respetar el patrón de WidgetId determinístico para widgets automáticos y generar WidgetIds con prefijo "usr_" para widgets creados por usuario.
6. Nombrar todo en inglés (clases, métodos, variables). Comentarios en español.
7. Usar System.Text.Json para serialización/deserialización.
8. Reutilizar componentes Radzen Blazor existentes. Para el mapa: elegir la estrategia más sencilla dentro del stack (JS interop con librería ligera tipo Leaflet, o Radzen Maps si está disponible). Minimizar dependencias externas.
9. Los selectores de campos (para mapa, chart nuevo, KPI) se alimentan de JsonArrayDescriptor.ItemProperties del array seleccionado.
10. Si no hay personalización → todo funciona exactamente igual que ahora.

NO generes código aún. Confirma que has entendido el contexto.
```

---

# ═══════════════════════════════════════════════════
# AMPLIACIÓN A — WIDGET MAPA (lat/lon)
# ═══════════════════════════════════════════════════

## MAP-1 — DTOs: Ampliar enums y DynamicWidgetDescriptor con tipo Map

```
FASE MAP-1 — Ampliar modelo de datos para widget tipo Map

OBJETIVO: Añadir el tipo Map al enum WidgetType y las propiedades necesarias en DynamicWidgetDescriptor para renderizar un mapa de puntos.

--- MODIFICAR: GreenTransit.Application/Features/EcoDataNet/DTOs/DataExplorer/DynamicWidgetDescriptor.cs ---

CAMBIO 1: Añadir valor al enum WidgetType:

    /// <summary>Mapa de puntos geográficos (lat/lon) extraídos del JSON.</summary>
    Map

CAMBIO 2: Añadir nueva sección de propiedades en DynamicWidgetDescriptor,
DESPUÉS de la sección "--- Datos para Gráfico ---" y ANTES de "--- Datos para texto/cabecera ---":

    // --- Datos para Mapa ---

    /// <summary>Nombre de la propiedad del array que contiene la latitud.</summary>
    public string? MapLatitudeField { get; set; }

    /// <summary>Nombre de la propiedad del array que contiene la longitud.</summary>
    public string? MapLongitudeField { get; set; }

    /// <summary>
    /// Nombre de la propiedad del array que se usa como título del marcador/punto en el mapa.
    /// Null = usar el primer campo string disponible, o el índice del elemento.
    /// </summary>
    public string? MapTitleField { get; set; }

    /// <summary>
    /// Lista de nombres de propiedades del array que se muestran en el tooltip/popup al hacer clic en un punto.
    /// Null o vacío = mostrar todos los campos del elemento.
    /// </summary>
    public List<string>? MapTooltipFields { get; set; }

    /// <summary>
    /// Datos del mapa (misma estructura que ChartData/TableData): lista de diccionarios con al menos
    /// las propiedades MapLatitudeField y MapLongitudeField.
    /// </summary>
    public List<Dictionary<string, object?>>? MapData { get; set; }

    /// <summary>
    /// Lista de todos los campos string del array fuente, para que la UI muestre opciones
    /// de "campo título" y "campos tooltip" en el panel de configuración.
    /// </summary>
    public List<string>? MapAvailableStringFields { get; set; }

    /// <summary>
    /// Lista de todos los campos del array fuente (string + numéricos), para opciones de tooltip.
    /// </summary>
    public List<string>? MapAvailableAllFields { get; set; }

NOTA: Todas las propiedades son nullable → no rompen la serialización ni los widgets existentes.

VERIFICACIÓN: Compilar sin errores. WidgetType ahora tiene 7 valores.
```

---

## MAP-2 — Heurística: Ampliar JsonSchemaAnalyzer para detectar lat/lon

```
FASE MAP-2 — Detección de campos de geolocalización en arrays

OBJETIVO: Ampliar JsonSchemaAnalyzer para que, al analizar un JsonArrayDescriptor, detecte si contiene campos de latitud y longitud.

--- MODIFICAR: GreenTransit.Application/Features/EcoDataNet/DTOs/DataExplorer/JsonDataSchema.cs ---

CAMBIO: Añadir 2 propiedades a JsonArrayDescriptor:

    /// <summary>
    /// Nombre de la propiedad detectada como latitud (null si no se detecta).
    /// Heurística: nombre contiene "lat", "latitude", "latitud" (case-insensitive) Y tipo es Number.
    /// </summary>
    public string? LatitudeProperty { get; set; }

    /// <summary>
    /// Nombre de la propiedad detectada como longitud (null si no se detecta).
    /// Heurística: nombre contiene "lon", "lng", "longitude", "longitud" (case-insensitive) Y tipo es Number.
    /// </summary>
    public string? LongitudeProperty { get; set; }

    /// <summary>True si se detectaron tanto LatitudeProperty como LongitudeProperty.</summary>
    public bool HasGeoCoordinates => LatitudeProperty != null && LongitudeProperty != null;

--- MODIFICAR: GreenTransit.Application/Features/EcoDataNet/Services/JsonSchemaAnalyzer.cs ---

En el método que procesa arrays homogéneos (donde ya se detectan CategoryProperty, TemporalProperty, NumericProperties),
AÑADIR un bloque de detección de coordenadas geográficas DESPUÉS de la detección existente:

LÓGICA DE DETECCIÓN:

1. Recorrer ItemProperties del array.
2. Para cada propiedad con PropertyType == Number:
   a. Normalizar el nombre a lowercase.
   b. Comprobar si el nombre contiene ALGUNO de estos tokens (detección lat):
      - "latitude", "latitud", "lat" (pero NO "lateral", "platform", "flatrate" — excluir si tras "lat" sigue una letra diferente de 'i').
      Heurística simple: el nombre EXACTO es "lat", "latitude", "latitud", o el nombre CONTIENE "lat" seguido de nada, "_", o "i"/"u"
      (es decir: "lat", "lat_", "lati", "latu" → sí; "platform" → no).
   c. Análoga para longitud: "longitude", "longitud", "lon", "lng"
      (excluir "long" solo como adjetivo: si el campo es EXACTAMENTE "long" aceptar; si es "long_" aceptar;
       si es "longDescription" → no. Heurística: "lon", "lng", "longitude", "longitud", o nombre que empiece por "lon" y NO siga con una vocal que no sea 'g').
   d. Validación de rango (opcional pero recomendada): si hay SampleValues disponibles, comprobar
      que los valores numéricos están en rango plausible: lat ∈ [-90, 90], lon ∈ [-180, 180].
      Si > 50% de las muestras están fuera de rango, descartar como candidato.

3. Asignar:
   - array.LatitudeProperty = primer candidato a lat válido (o null).
   - array.LongitudeProperty = primer candidato a lon válido (o null).

4. Solo considerar que el array "tiene coordenadas" si AMBOS se detectan (HasGeoCoordinates).

EJEMPLO:
JSON: { "locations": [{ "name": "Madrid", "lat": 40.4168, "lon": -3.7038, "tons": 500 }, ...] }
→ LatitudeProperty = "lat", LongitudeProperty = "lon", HasGeoCoordinates = true.

JSON: { "items": [{ "category": "A", "value": 100, "platform": "web" }] }
→ LatitudeProperty = null, LongitudeProperty = null, HasGeoCoordinates = false.

VERIFICACIÓN: Compilar. Ejecutar mentalmente contra los ejemplos.
```

---

## MAP-3 — LayoutBuilder: Generar widget Map por heurística

```
FASE MAP-3 — Ampliar DashboardLayoutBuilder para generar widgets Map

OBJETIVO: Cuando un array tiene HasGeoCoordinates==true, generar un widget de tipo Map
además de (o en lugar de) un gráfico, según la lógica más apropiada.

--- MODIFICAR: GreenTransit.Application/Features/EcoDataNet/Services/DashboardLayoutBuilder.cs ---

AÑADIR una nueva regla heurística (sugerencia: Regla 3.5, entre las reglas de gráficos temporales y las de gráficos de categoría, o como Regla 10 al final — decidir el SortOrder para que el mapa quede después de los KPIs y antes o después de los charts según tenga sentido):

REGLA DE MAPA:
  Condición: array.HasGeoCoordinates == true Y array.ItemCount >= 2.
  Acción: crear un widget Map ADEMÁS del gráfico y la tabla que ya se generan para este array.

  Widget generado:
  - WidgetId = $"map_{array.Name.ToLowerInvariant()}"
    Ejemplo: "map_locations"
  - Type = WidgetType.Map
  - Title = $"{array.DisplayName} — Mapa" (o equivalente humanizado)
  - SortOrder = (entre charts y tablas, p.ej. 35-39, o calcular dinámicamente)
  - ColumnSpan = 12 (mapas necesitan ancho completo)
  - MapLatitudeField = array.LatitudeProperty
  - MapLongitudeField = array.LongitudeProperty
  - MapTitleField = primer campo string del array (CategoryProperty si existe, o el primer ItemProperty con tipo String)
  - MapTooltipFields = null (default: mostrar todos los campos)
  - MapData = array.RawData
  - SourceJsonPath = array.JsonPath
  - MapAvailableStringFields = ItemProperties donde PropertyType == String → lista de nombres
  - MapAvailableAllFields = todas las ItemProperties → lista de nombres

NOTA IMPORTANTE: el widget Map se genera ADEMÁS del Chart y DataTable existentes para el mismo array.
El usuario podrá ocultar el que no quiera desde el modo edición.

NOTA: Si un array tiene HasGeoCoordinates==true pero NO tiene CategoryProperty ni TemporalProperty,
igualmente se genera el mapa (y la tabla). Solo se omite el chart si no hay datos para gráfico.

VERIFICACIÓN: Con un JSON que tiene un array con lat/lon, el builder genera KPIs + Charts + Map + Tables.
Con un JSON sin lat/lon, el builder genera exactamente lo mismo que antes (sin Map).
```

---

## MAP-4 — Override: Ampliar WidgetLayoutOverride con config de mapa

```
FASE MAP-4 — Persistencia de la configuración personalizada de mapa

OBJETIVO: Ampliar WidgetLayoutOverride para que el usuario pueda personalizar qué campos
se usan como lat, lon, título y tooltip del mapa, y que esa personalización se persista.

--- CREAR: GreenTransit.Application/Features/EcoDataNet/DTOs/DataExplorer/MapFieldBinding.cs ---

namespace GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;

/// <summary>
/// Configuración personalizada de los campos que alimentan un widget Mapa.
/// Se almacena como parte de WidgetLayoutOverride.CustomMapBinding.
/// Null en cada propiedad = "usar el campo detectado automáticamente por la heurística".
/// </summary>
public class MapFieldBinding
{
    /// <summary>
    /// Campo personalizado para la latitud. Debe ser numérico y existir en el array fuente.
    /// Null = usar el detectado automáticamente (array.LatitudeProperty).
    /// </summary>
    public string? CustomLatitudeField { get; set; }

    /// <summary>
    /// Campo personalizado para la longitud. Debe ser numérico y existir en el array fuente.
    /// Null = usar el detectado automáticamente (array.LongitudeProperty).
    /// </summary>
    public string? CustomLongitudeField { get; set; }

    /// <summary>
    /// Campo personalizado para el título de cada punto en el mapa.
    /// Null = usar el primer campo string o el automático.
    /// </summary>
    public string? CustomTitleField { get; set; }

    /// <summary>
    /// Campos personalizados para el tooltip/popup al pasar por encima del punto.
    /// Null o vacío = mostrar todos los campos del elemento.
    /// </summary>
    public List<string>? CustomTooltipFields { get; set; }
}

--- MODIFICAR: GreenTransit.Application/Features/EcoDataNet/DTOs/DataExplorer/WidgetLayoutOverride.cs ---

AÑADIR al final de la clase (antes del cierre):

    /// <summary>
    /// Configuración personalizada de los campos del mapa (lat, lon, título, tooltip).
    /// Solo aplica a widgets de tipo Map. Null = usar binding automático.
    /// Se serializa dentro de LayoutConfigJson junto con el resto de overrides.
    /// </summary>
    public MapFieldBinding? CustomMapBinding { get; set; }

VERIFICACIÓN: Compilar. WidgetLayoutOverride ahora tiene 10 propiedades.
No se necesita migración de BD — el campo se serializa dentro de LayoutConfigJson (nvarchar(max)).
```

---

## MAP-5 — MergeService: Aplicar overrides de mapa

```
FASE MAP-5 — Ampliar LayoutCustomizationService para CustomMapBinding

OBJETIVO: Si un widget Map tiene un override con CustomMapBinding, aplicar los campos personalizados.

--- MODIFICAR: GreenTransit.Application/Features/EcoDataNet/Services/LayoutCustomizationService.cs ---

En el método ApplyOverrides(), dentro del bloque donde se aplican overrides por widget,
DESPUÉS de la parte que aplica CustomChartBinding y CustomTableColumns:

AÑADIR:

    - Si override.CustomMapBinding != null y widget.Type == WidgetType.Map:
      a. Si CustomMapBinding.CustomLatitudeField != null
         Y el campo existe en widget.MapAvailableAllFields
         Y el campo es numérico (verificar contra JsonArrayDescriptor o asumir que si está en AllFields es válido):
         → widget.MapLatitudeField = CustomMapBinding.CustomLatitudeField.
      b. Análogo para CustomMapBinding.CustomLongitudeField → widget.MapLongitudeField.
      c. Si CustomMapBinding.CustomTitleField != null
         Y el campo existe en widget.MapAvailableStringFields:
         → widget.MapTitleField = CustomMapBinding.CustomTitleField.
      d. Si CustomMapBinding.CustomTooltipFields != null y tiene al menos 1 elemento:
         → Filtrar: quedarse solo con campos que existan en widget.MapAvailableAllFields.
         → Si tras filtrar queda al menos 1: widget.MapTooltipFields = campos filtrados.
         → Si no queda ninguno: mantener null (mostrar todos).

VERIFICACIÓN:
- Override con CustomLatitudeField válido → se aplica.
- Override con campo inválido (obsoleto) → se ignora.
- Sin CustomMapBinding → widget Map usa campos automáticos.
```

---

## MAP-6 — UI Renderizado: Crear DynamicMap.razor

```
FASE MAP-6 — Componente DynamicMap.razor

OBJETIVO: Crear un componente Blazor que renderice un mapa interactivo con los puntos del widget.

--- CREAR: GreenTransit.Web/Components/Pages/EcoDataNet/DataExplorer/DynamicMap.razor ---

ESTRATEGIA DE IMPLEMENTACIÓN (para que Copilot decida la mejor opción):

OPCIÓN RECOMENDADA: JavaScript Interop con Leaflet.js (librería de mapas open-source, ligera, sin API key).

Pasos:
1. Incluir Leaflet CSS y JS desde CDN en la página o en _Host.cshtml / App.razor:
   - https://unpkg.com/leaflet@1.9.4/dist/leaflet.css
   - https://unpkg.com/leaflet@1.9.4/dist/leaflet.js
   (Verificar que el proyecto permite CDN externos. Si no, descargar y servir desde wwwroot/lib/leaflet/).

2. Crear un archivo JS interop: wwwroot/js/leaflet-interop.js con funciones:
   - initializeMap(containerId, centerLat, centerLon, zoom) → inicializa mapa con tiles de OpenStreetMap.
   - addMarkers(containerId, markersData) → añade marcadores con popup. markersData = [{ lat, lon, title, tooltipHtml }, ...].
   - fitBounds(containerId) → ajusta el zoom para que todos los marcadores sean visibles.
   - destroyMap(containerId) → limpieza al desmontar el componente.

3. El componente DynamicMap.razor:
   - Parámetro: [Parameter] public DynamicWidgetDescriptor Widget { get; set; }
   - Inyectar IJSRuntime.
   - OnAfterRenderAsync(firstRender): si firstRender, llamar a initializeMap, luego addMarkers.
   - Dispose/DisposeAsync: llamar a destroyMap.
   - HTML: un <div> con id único (p.ej. $"map-{Widget.WidgetId}") con alto fijo (400px o configurable) y ancho 100%.

4. Construcción de datos de marcadores desde el componente:
   - Iterar Widget.MapData.
   - Para cada elemento: extraer lat (Widget.MapLatitudeField), lon (Widget.MapLongitudeField).
   - Título: Widget.MapTitleField → buscar en el diccionario. Si null, usar índice.
   - Tooltip: si Widget.MapTooltipFields tiene valores, mostrar solo esos campos.
     Si es null/vacío, mostrar todos los campos del diccionario como pares clave-valor.
   - Formatear tooltip como HTML simple (tabla o lista de <b>campo:</b> valor).

5. Cálculo de centro y zoom:
   - Centro: promedio de todas las latitudes y longitudes.
   - Zoom: usar fitBounds para ajustar automáticamente.

6. Estilo: el contenedor del mapa debe respetar las variables CSS --gt-* para bordes y sombras.
   El mapa en sí usa tiles estándar de OpenStreetMap (no requiere estilizado corporativo).

ALTERNATIVA (si Leaflet no es viable):
- Radzen GoogleMap (requiere API key de Google Maps → preguntar al usuario).
- Otro componente Blazor de mapas compatible con el stack.

Copilot: elige la opción más sencilla y con menos dependencias. Si Leaflet funciona con el stack, úsalo. Documenta la elección.

PATRÓN A SEGUIR: los componentes existentes (DynamicChart.razor, DynamicDataTable.razor) reciben un Widget como parámetro y renderizan en función de sus propiedades. DynamicMap.razor debe seguir el mismo patrón.

--- MODIFICAR: GreenTransit.Web/Components/Pages/EcoDataNet/DataExplorer/EdcDataExplorer.razor ---

En el bloque de renderizado de widgets (switch/if por widget.Type), AÑADIR:

    case WidgetType.Map:
        <DynamicMap Widget="widget" />

VERIFICACIÓN:
- Con JSON que tiene lat/lon, aparece un mapa con marcadores.
- Los marcadores muestran tooltip al hacer clic.
- El zoom se ajusta automáticamente para mostrar todos los puntos.
- Sin lat/lon en el JSON, no aparece ningún mapa (comportamiento actual intacto).
```

---

## MAP-7 — UI Config: Selectores de mapa en WidgetConfigPanel

```
FASE MAP-7 — Ampliar WidgetConfigPanel.razor para widgets Map

OBJETIVO: En modo edición, al abrir el panel de configuración de un widget Map,
mostrar selectores para cambiar los campos de lat, lon, título y tooltip.

--- MODIFICAR: GreenTransit.Web/Components/Pages/EcoDataNet/DataExplorer/WidgetConfigPanel.razor ---

AÑADIR un bloque condicional visible solo si Widget.Type == WidgetType.Map:

SECCIÓN "Configuración del mapa":

1. **Latitud**: RadzenDropDown con las opciones de campos numéricos del array
   (filtrar MapAvailableAllFields por los que sean numéricos — o usar AvailableValueFields si ya están poblados).
   Valor actual: Widget.MapLatitudeField.
   Al cambiar: Widget.MapLatitudeField = nuevo valor; invocar callback OnWidgetChanged.

2. **Longitud**: RadzenDropDown análogo.
   Valor actual: Widget.MapLongitudeField.

3. **Título del punto**: RadzenDropDown con las opciones de Widget.MapAvailableStringFields + opción "(Ninguno / Índice)".
   Valor actual: Widget.MapTitleField.
   Al cambiar: Widget.MapTitleField = nuevo valor.

4. **Campos en tooltip**: RadzenDropDown con Multiple="true" con las opciones de Widget.MapAvailableAllFields.
   Valor actual: Widget.MapTooltipFields (puede ser null = todos).
   Incluir un checkbox o botón "Mostrar todos" que setee MapTooltipFields = null.
   Al cambiar: Widget.MapTooltipFields = selección (o null si "todos").

Los display names de los campos deben usar Widget.FieldDisplayNames si está disponible,
o humanizar el nombre original.

PATRÓN A SEGUIR: idéntico al patrón de los selectores de campos de gráfico (CB-7).
Reutilizar la misma estructura visual (sección colapsable dentro del panel).

VERIFICACIÓN: En modo edición, el engranaje de un widget Map abre el panel con los 4 selectores.
Cambiar un campo y guardar → al reabrir, los campos personalizados aparecen.
```

---

## MAP-8 — Tests unitarios para mapa

```
FASE MAP-8 — Tests unitarios de detección lat/lon y widget Map

OBJETIVO: Verificar las heurísticas de geolocalización y la generación del widget.

--- CREAR: GreenTransit.Tests/Features/EcoDataNet/DataExplorer/MapDetectionTests.cs ---

Tests con xUnit para JsonSchemaAnalyzer:

1. Analyze_ArrayWithLatLon_DetectsGeoCoordinates:
   JSON: { "points": [{"name":"A","lat":40.41,"lon":-3.70},{"name":"B","lat":41.38,"lon":2.17}] }
   Assert: Arrays[0].LatitudeProperty == "lat", LongitudeProperty == "lon", HasGeoCoordinates == true.

2. Analyze_ArrayWithLatitudeLongitude_DetectsGeoCoordinates:
   JSON: { "data": [{"latitude":40.41,"longitude":-3.70}] }
   Assert: HasGeoCoordinates == true.

3. Analyze_ArrayWithLatLng_DetectsGeoCoordinates:
   JSON: { "data": [{"lat":40.41,"lng":-3.70}] }
   Assert: HasGeoCoordinates == true.

4. Analyze_ArrayWithoutLatLon_NoGeoCoordinates:
   JSON: { "items": [{"category":"A","value":100,"platform":"web"}] }
   Assert: HasGeoCoordinates == false.

5. Analyze_ArrayWithLatOnly_NoGeoCoordinates:
   JSON: { "data": [{"lat":40.41,"value":100}] }
   Assert: HasGeoCoordinates == false (falta lon).

6. Analyze_FieldNamedPlatform_NotDetectedAsLat:
   JSON: { "data": [{"platform":"web","lateral":5,"value":100}] }
   Assert: LatitudeProperty == null (platform y lateral no son lat).

--- CREAR: GreenTransit.Tests/Features/EcoDataNet/DataExplorer/MapWidgetBuilderTests.cs ---

Tests para DashboardLayoutBuilder:

7. Build_ArrayWithGeoCoordinates_GeneratesMapWidget:
   Schema con 1 array que tiene HasGeoCoordinates==true.
   Assert: al menos 1 widget con Type==WidgetType.Map,
           MapLatitudeField no vacío, MapLongitudeField no vacío.

8. Build_ArrayWithoutGeoCoordinates_NoMapWidget:
   Schema con 1 array sin coordenadas.
   Assert: ningún widget con Type==WidgetType.Map.

9. Build_ArrayWithGeoCoordinates_AlsoGeneratesChartAndTable:
   Schema con array que tiene HasGeoCoordinates Y CategoryProperty.
   Assert: se generan Map + Chart + DataTable para el mismo array.

--- Tests para LayoutCustomizationService (en archivo existente o nuevo) ---

10. ApplyOverrides_CustomMapBinding_AppliesFields:
    Widget Map con MapLatitudeField="lat", override con CustomMapBinding.CustomLatitudeField="coordY".
    "coordY" está en MapAvailableAllFields.
    Assert: widget.MapLatitudeField == "coordY".

11. ApplyOverrides_CustomMapBinding_ObsoleteField_Ignored:
    Override con CustomMapBinding.CustomTitleField="nonExistent".
    "nonExistent" NO está en MapAvailableStringFields.
    Assert: widget.MapTitleField == valor original (no cambia).

12. ApplyOverrides_NoCustomMapBinding_FieldsUnchanged:
    Widget Map sin override de mapa.
    Assert: MapLatitudeField, MapLongitudeField == valores originales automáticos.

VERIFICACIÓN: Todos los tests pasan. dotnet test sin errores.
```

---

# ═══════════════════════════════════════════════════
# AMPLIACIÓN B — CHARTS ADICIONALES CREADOS POR USUARIO
# ═══════════════════════════════════════════════════

## CHART-ADD-1 — DTOs: Modelo para widgets creados por usuario (CustomWidgets)

```
FASE CHART-ADD-1 — Ampliar modelo de persistencia para widgets creados por usuario

OBJETIVO: El modelo actual de LayoutConfigJson solo guarda OVERRIDES sobre widgets autogenerados.
Los charts creados por usuario no tienen un widget autogenerado de referencia.
Hay que ampliar el modelo para soportar una lista de widgets completamente definidos por el usuario.

PROBLEMA DE DISEÑO:
Actualmente, SaveLayout() compara _currentWidgets con _autoGeneratedWidgets y genera overrides
(lista de WidgetLayoutOverride). Un chart creado por el usuario NO existe en _autoGeneratedWidgets,
así que no se puede representar como un override.

SOLUCIÓN: Ampliar LayoutConfigDto y el formato del LayoutConfigJson para incluir, además de los overrides,
una lista de CustomWidgets (definiciones completas de widgets creados por el usuario).

--- CREAR: GreenTransit.Application/Features/EcoDataNet/DTOs/DataExplorer/CustomWidgetDefinition.cs ---

namespace GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;

/// <summary>
/// Definición completa de un widget creado por el usuario (no autogenerado).
/// Se serializa dentro de LayoutConfigJson como parte de la lista CustomWidgets.
/// A diferencia de WidgetLayoutOverride (que solo tiene "deltas" respecto al automático),
/// esta clase contiene toda la información necesaria para renderizar el widget.
/// </summary>
public class CustomWidgetDefinition
{
    /// <summary>
    /// Identificador único del widget creado por el usuario.
    /// Se genera con prefijo "usr_" + 8 caracteres aleatorios al crear.
    /// Ejemplo: "usr_a1b2c3d4".
    /// </summary>
    public string WidgetId { get; set; } = string.Empty;

    /// <summary>Tipo de widget: Chart, KpiCard o Map.</summary>
    public WidgetType WidgetType { get; set; }

    /// <summary>Título asignado por el usuario.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Orden en el dashboard (null = al final).</summary>
    public int? SortOrder { get; set; }

    /// <summary>Ancho en columnas 1-12.</summary>
    public int ColumnSpan { get; set; } = 6;

    /// <summary>True si el usuario ha ocultado este widget.</summary>
    public bool IsHidden { get; set; }

    // --- Campos para Chart creado por usuario ---

    /// <summary>Nombre del array fuente del JSON que alimenta este gráfico.
    /// Corresponde a JsonArrayDescriptor.Name.</summary>
    public string? SourceArrayName { get; set; }

    /// <summary>Tipo de gráfico.</summary>
    public ChartSubType? ChartType { get; set; }

    /// <summary>Campo para el eje X / categorías.</summary>
    public string? ChartCategoryField { get; set; }

    /// <summary>Campos para el eje Y / series.</summary>
    public List<string>? ChartValueFields { get; set; }

    // --- Campos para KPI calculado (se usarán en Ampliación C) ---

    /// <summary>Definición del KPI calculado (null si es Chart/Map).</summary>
    public CustomKpiDefinition? KpiDefinition { get; set; }
}

--- MODIFICAR: GreenTransit.Application/Features/EcoDataNet/DTOs/DataExplorer/LayoutConfigDto.cs ---

AÑADIR propiedad:

    /// <summary>
    /// Lista de widgets completamente definidos por el usuario (charts y KPIs adicionales).
    /// Estos widgets no tienen correspondencia en los autogenerados; su definición completa
    /// se almacena aquí y se inyecta en el dashboard en el merge.
    /// </summary>
    public List<CustomWidgetDefinition> CustomWidgets { get; set; } = new();

--- CAMBIAR FORMATO de LayoutConfigJson ---

IMPORTANTE: Actualmente LayoutConfigJson se serializa como un ARRAY directo de WidgetLayoutOverride:
  [{ "widgetId": "kpi_total", ... }, ...]

Para soportar CustomWidgets sin romper retrocompatibilidad, hay DOS opciones:

OPCIÓN RECOMENDADA:
Cambiar el formato del JSON a un OBJETO con dos propiedades:
  {
    "overrides": [{ "widgetId": "kpi_total", ... }, ...],
    "customWidgets": [{ "widgetId": "usr_a1b2c3d4", ... }, ...]
  }

Y hacer que la deserialización sea tolerante:
- Si el JSON empieza con '[' → formato antiguo → deserializar como List<WidgetLayoutOverride> y CustomWidgets = [].
- Si el JSON empieza con '{' → formato nuevo → deserializar como objeto con ambas listas.

Esto mantiene compatibilidad con configuraciones guardadas antes de esta ampliación.

--- CREAR: GreenTransit.Application/Features/EcoDataNet/DTOs/DataExplorer/PersistedLayoutConfig.cs ---

/// <summary>
/// Formato de serialización del LayoutConfigJson (formato nuevo, objeto con overrides + customWidgets).
/// </summary>
public class PersistedLayoutConfig
{
    public List<WidgetLayoutOverride> Overrides { get; set; } = new();
    public List<CustomWidgetDefinition> CustomWidgets { get; set; } = new();
}

--- MODIFICAR: GetExplorerLayoutConfigQueryHandler.cs ---

Cambiar la lógica de deserialización para detectar formato antiguo (array) vs nuevo (objeto):

    var json = entity.LayoutConfigJson;
    PersistedLayoutConfig config;
    if (json.TrimStart().StartsWith('['))
    {
        // Formato antiguo: solo overrides como array
        var overrides = JsonSerializer.Deserialize<List<WidgetLayoutOverride>>(json, _jsonOptions);
        config = new PersistedLayoutConfig { Overrides = overrides ?? new(), CustomWidgets = new() };
    }
    else
    {
        // Formato nuevo: objeto con overrides + customWidgets
        config = JsonSerializer.Deserialize<PersistedLayoutConfig>(json, _jsonOptions) ?? new();
    }

--- MODIFICAR: SaveExplorerLayoutConfigCommandHandler.cs ---

Al serializar, usar siempre el formato nuevo (PersistedLayoutConfig):

    var persistedConfig = new PersistedLayoutConfig
    {
        Overrides = command.Overrides,
        CustomWidgets = command.CustomWidgets
    };
    entity.LayoutConfigJson = JsonSerializer.Serialize(persistedConfig, _jsonOptions);

--- MODIFICAR: SaveExplorerLayoutConfigCommand.cs ---

AÑADIR propiedad:

    public List<CustomWidgetDefinition> CustomWidgets { get; set; } = new();

VERIFICACIÓN: Compilar. Guardar y cargar una configuración antigua (array puro) sigue funcionando.
Guardar y cargar una configuración nueva (con customWidgets) también funciona.
```

---

## CHART-ADD-2 — UI Toolbar: Botón "Añadir gráfico"

```
FASE CHART-ADD-2 — Botón en toolbar para añadir gráficos

OBJETIVO: En modo edición, mostrar un botón "Añadir gráfico" en la LayoutEditorToolbar
que abra el diálogo de creación.

--- MODIFICAR: GreenTransit.Web/Components/Pages/EcoDataNet/DataExplorer/LayoutEditorToolbar.razor ---

AÑADIR un botón RadzenButton visible solo en modo edición:
- Texto: "Añadir gráfico" (o icono "add_chart" + texto)
- ButtonStyle: Info o Light
- Posición: al lado del toggle "Ver desactivados", antes de los botones Guardar/Resetear.
- Al hacer clic: invocar un EventCallback (p.ej. OnAddChart) que el padre (EdcDataExplorer.razor) maneja.

--- MODIFICAR: GreenTransit.Web/Components/Pages/EcoDataNet/DataExplorer/EdcDataExplorer.razor ---

Añadir handler para OnAddChart:
- Al hacer clic, abrir un panel/diálogo de creación (implementado en CHART-ADD-3).
- El estado del diálogo se maneja dentro de EdcDataExplorer.razor.

VERIFICACIÓN: En modo edición aparece el botón "Añadir gráfico". Al hacer clic (por ahora) solo activa un flag _showAddChartDialog.
```

---

## CHART-ADD-3 — UI Diálogo: Creación de gráfico nuevo

```
FASE CHART-ADD-3 — Diálogo/panel de creación de gráfico adicional

OBJETIVO: Crear un componente o sección inline que permita al usuario configurar un nuevo gráfico
seleccionando la fuente de datos, el tipo, y los campos.

--- CREAR: GreenTransit.Web/Components/Pages/EcoDataNet/DataExplorer/AddChartDialog.razor ---

Componente (puede ser un RadzenDialog, un panel colapsable inline, o un overlay — elegir lo más consistente con la UI existente).

PARÁMETROS:
  [Parameter] public JsonDataSchema Schema { get; set; }
    → Para obtener la lista de arrays disponibles con sus propiedades.
  [Parameter] public EventCallback<CustomWidgetDefinition> OnChartCreated { get; set; }
    → Callback cuando el usuario confirma la creación.
  [Parameter] public EventCallback OnCancelled { get; set; }

UI DEL DIÁLOGO:

1. **Fuente de datos** (obligatorio):
   RadzenDropDown con los arrays del Schema: Schema.Arrays → lista de opciones.
   Cada opción muestra array.DisplayName + " (" + array.ItemCount + " elementos)".
   Al seleccionar un array, se actualizan los dropdowns de campos.

2. **Tipo de gráfico** (obligatorio):
   RadzenDropDown con ChartSubType: BarVertical, BarHorizontal, Line, Area, Donut, Pie.

3. **Campo de categoría / eje X** (obligatorio):
   RadzenDropDown con los campos del array seleccionado que sean String o DateTime
   (análogo a AvailableCategoryFields — filtrar ItemProperties por PropertyType String/DateTime).
   Mostrar DisplayName humanizado.

4. **Campos de valor / eje Y** (obligatorio, al menos 1):
   RadzenDropDown con Multiple="true" con los campos numéricos del array
   (análogo a AvailableValueFields — filtrar ItemProperties por PropertyType Number).
   Para Donut/Pie: forzar selección de exactamente 1 campo.

5. **Título** (opcional, con valor por defecto):
   Input de texto. Default: "{array.DisplayName} — {ChartSubType}".

6. **Ancho** (opcional):
   RadzenDropDown: 50% (6), 100% (12). Default: 6.

7. **Botones**: "Crear" (habilitado solo si array + tipo + categoría + al menos 1 valor seleccionados) y "Cancelar".

LÓGICA AL CREAR:

    var definition = new CustomWidgetDefinition
    {
        WidgetId = $"usr_{Guid.NewGuid().ToString("N")[..8]}",
        WidgetType = WidgetType.Chart,
        Title = título ingresado,
        ColumnSpan = ancho seleccionado,
        SourceArrayName = array seleccionado.Name,
        ChartType = tipo seleccionado,
        ChartCategoryField = campo categoría seleccionado,
        ChartValueFields = campos valor seleccionados
    };
    await OnChartCreated.InvokeAsync(definition);

--- MODIFICAR: GreenTransit.Web/Components/Pages/EcoDataNet/DataExplorer/EdcDataExplorer.razor ---

Handler de OnChartCreated:

1. Recibir el CustomWidgetDefinition.
2. Construir un DynamicWidgetDescriptor completo a partir de la definición:
   - Tipo, WidgetId, Title, ColumnSpan del definition.
   - ChartCategoryField, ChartValueFields del definition.
   - ChartData: buscar el array con nombre == definition.SourceArrayName en _explorerResult.Schema.Arrays,
     obtener su RawData.
   - AvailableCategoryFields, AvailableValueFields, FieldDisplayNames: poblar desde el array encontrado.
   - SortOrder: asignar el máximo SortOrder actual + 10 (al final del dashboard).
3. Añadir el widget a _currentWidgets.
4. Añadir el CustomWidgetDefinition a una lista interna _customWidgets.
5. Marcar _hasUnsavedChanges = true.
6. Cerrar el diálogo.

NOTA IMPORTANTE: El widget recién creado es editable con WidgetConfigPanel (cambiar tipo, campos, título, ancho, ocultar)
y eliminable (añadir botón "Eliminar" en el panel config, solo visible para widgets con WidgetId que empiece por "usr_").

VERIFICACIÓN:
- En modo edición, "Añadir gráfico" abre el diálogo.
- Seleccionar un array, tipo y campos → "Crear" → aparece un nuevo gráfico en el dashboard.
- El gráfico muestra datos correctos del array seleccionado.
- El gráfico es movible (drag&drop), configurable (WidgetConfigPanel) y ocultable.
```

---

## CHART-ADD-4 — Merge: Inyectar custom widgets en el dashboard

```
FASE CHART-ADD-4 — Ampliar LayoutCustomizationService para inyectar CustomWidgets

OBJETIVO: Al cargar una configuración guardada, además de aplicar overrides sobre widgets automáticos,
inyectar los CustomWidgets como widgets adicionales en la lista final.

--- MODIFICAR: GreenTransit.Application/Features/EcoDataNet/Services/ILayoutCustomizationService.cs ---

Ampliar la firma de ApplyOverrides (o crear un método adicional):

OPCIÓN A (ampliar firma):
    LayoutMergeResult ApplyOverrides(
        List<DynamicWidgetDescriptor> autoWidgets,
        List<WidgetLayoutOverride> overrides,
        List<CustomWidgetDefinition> customWidgets,  // NUEVO
        string? savedSchemaHash,
        string currentSchemaHash,
        JsonDataSchema currentSchema);  // NUEVO — para obtener RawData de los arrays

OPCIÓN B (método separado): crear un segundo método InjectCustomWidgets(). Copilot: elige la opción más limpia.

--- MODIFICAR: GreenTransit.Application/Features/EcoDataNet/Services/LayoutCustomizationService.cs ---

LÓGICA para inyectar CustomWidgets:

Para cada CustomWidgetDefinition en la lista:

1. Buscar el array fuente por nombre: currentSchema.Arrays.FirstOrDefault(a => a.Name == def.SourceArrayName).
2. Si el array no existe (schema cambió, array eliminado) → loguear warning, OMITIR este custom widget.
3. Si el array existe → construir un DynamicWidgetDescriptor completo:
   - WidgetId = def.WidgetId (empieza por "usr_")
   - Type = def.WidgetType (Chart)
   - Title = def.Title
   - SortOrder = def.SortOrder ?? (maxSortOrder actual + 10)
   - ColumnSpan = def.ColumnSpan
   - IsHidden = def.IsHidden
   - ChartType = def.ChartType
   - ChartCategoryField = def.ChartCategoryField (validar que el campo existe en el array actual)
   - ChartValueFields = def.ChartValueFields (filtrar los que existan)
   - ChartData = array.RawData
   - AvailableCategoryFields, AvailableValueFields, FieldDisplayNames = construir desde array.ItemProperties
   - SourceArrayName = def.SourceArrayName
4. Añadir el widget construido a la lista de resultados.

5. Si algún campo del CustomWidgetDefinition es obsoleto (campo eliminado del array), hacer fallback:
   - ChartCategoryField obsoleto → usar array.CategoryProperty o el primer string.
   - ChartValueFields todos obsoletos → usar array.NumericProperties.
   - Marcar en LayoutMergeResult (opcional: nuevo flag o listado de warnings).

VERIFICACIÓN: Guardar un chart personalizado. Cerrar y reabrir el asset.
El chart creado por usuario aparece en el dashboard con los datos correctos.
```

---

## CHART-ADD-5 — Persistencia: Ampliar SaveLayout() y validador

```
FASE CHART-ADD-5 — Ampliar lógica de guardado para custom widgets

OBJETIVO: Al guardar, incluir los CustomWidgetDefinitions en el command.

--- MODIFICAR: GreenTransit.Web/Components/Pages/EcoDataNet/DataExplorer/EdcDataExplorer.razor ---

En el método SaveLayout():

Actualmente se construyen los overrides comparando _currentWidgets con _autoGeneratedWidgets.
AMPLIAR para:

1. Separar _currentWidgets en dos grupos:
   a. Widgets autogenerados: aquellos cuyo WidgetId NO empieza por "usr_".
      → Generar overrides comparando con _autoGeneratedWidgets (lógica existente).
   b. Widgets de usuario: aquellos cuyo WidgetId empieza por "usr_".
      → Convertir cada uno a CustomWidgetDefinition con su estado actual (título, tipo, campos, ancho, oculto, sortOrder).

2. Enviar el SaveExplorerLayoutConfigCommand con:
   - Overrides = overrides de widgets autogenerados.
   - CustomWidgets = lista de CustomWidgetDefinition.

3. Al eliminar un widget de usuario (botón "Eliminar" en WidgetConfigPanel):
   - Eliminar de _currentWidgets.
   - Eliminar de la lista interna _customWidgets.
   - Marcar _hasUnsavedChanges = true.

--- MODIFICAR: GreenTransit.Application/Features/EcoDataNet/Validators/SaveExplorerLayoutConfigCommandValidator.cs ---

AÑADIR validación para CustomWidgets:

    RuleForEach(x => x.CustomWidgets).ChildRules(cw =>
    {
        cw.RuleFor(x => x.WidgetId).NotEmpty().Must(id => id.StartsWith("usr_"))
          .WithMessage("Los widgets de usuario deben tener WidgetId con prefijo 'usr_'.");
        cw.RuleFor(x => x.Title).NotEmpty().MaximumLength(256);
        cw.RuleFor(x => x.ColumnSpan).InclusiveBetween(1, 12);
        cw.RuleFor(x => x.SourceArrayName).NotEmpty()
          .When(x => x.WidgetType == WidgetType.Chart);
        cw.RuleFor(x => x.ChartType).NotNull()
          .When(x => x.WidgetType == WidgetType.Chart);
        cw.RuleFor(x => x.ChartCategoryField).NotEmpty()
          .When(x => x.WidgetType == WidgetType.Chart);
        cw.RuleFor(x => x.ChartValueFields).NotNull().NotEmpty()
          .When(x => x.WidgetType == WidgetType.Chart);
    });

VERIFICACIÓN:
- Crear un chart de usuario, guardar, reabrir → el chart persiste.
- Eliminar el chart de usuario, guardar → al reabrir ya no aparece.
- Crear chart + modificar un override de widget automático + guardar → ambos persisten.
```

---

## CHART-ADD-6 — Tests unitarios para charts creados por usuario

```
FASE CHART-ADD-6 — Tests unitarios

--- CREAR: GreenTransit.Tests/Features/EcoDataNet/DataExplorer/CustomWidgetTests.cs ---

1. InjectCustomWidgets_ValidChart_AddsToWidgets:
   CustomWidgetDefinition con SourceArrayName existente en Schema.
   Assert: widget inyectado en resultado, Type==Chart, datos del array correcto.

2. InjectCustomWidgets_ObsoleteArrayName_OmitsWidget:
   CustomWidgetDefinition con SourceArrayName que NO existe en Schema actual.
   Assert: widget no inyectado, warning logueado.

3. InjectCustomWidgets_ObsoleteFields_FallbackToAutomatic:
   CustomWidgetDefinition con ChartCategoryField que no existe en el array actual.
   Assert: widget inyectado con fallback a CategoryProperty del array.

4. InjectCustomWidgets_EmptyList_NoChange:
   CustomWidgets vacía.
   Assert: resultado idéntico al merge normal.

5. SaveAndLoad_CustomWidget_RoundTrip:
   Serializar PersistedLayoutConfig con 1 override + 1 CustomWidget.
   Deserializar.
   Assert: 1 override + 1 CustomWidget, campos intactos.

6. Deserialize_OldFormat_ArrayOnly_ReturnsEmptyCustomWidgets:
   JSON formato antiguo: [{ "widgetId": "kpi_total", ... }].
   Assert: Overrides.Count == 1, CustomWidgets.Count == 0.

7. Deserialize_NewFormat_BothLists:
   JSON formato nuevo: { "overrides": [...], "customWidgets": [...] }.
   Assert: ambas listas correctas.

VERIFICACIÓN: Todos los tests pasan.
```

---

# ═══════════════════════════════════════════════════
# AMPLIACIÓN C — KPIs CALCULADOS POR USUARIO
# ═══════════════════════════════════════════════════

## KPI-USER-1 — DTOs: Crear CustomKpiDefinition

```
FASE KPI-USER-1 — DTO para definición de KPIs calculados

OBJETIVO: Crear el DTO que describe un KPI calculado configurable por el usuario.

--- CREAR: GreenTransit.Application/Features/EcoDataNet/DTOs/DataExplorer/CustomKpiDefinition.cs ---

namespace GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;

/// <summary>
/// Define un KPI calculado por el usuario a partir de datos de un array del JSON.
/// Se almacena como parte de CustomWidgetDefinition.KpiDefinition.
/// </summary>
public class CustomKpiDefinition
{
    /// <summary>
    /// Nombre del array fuente del JSON del que se extraen los datos para el cálculo.
    /// Corresponde a JsonArrayDescriptor.Name.
    /// </summary>
    public string SourceArrayName { get; set; } = string.Empty;

    /// <summary>Operación a aplicar sobre el campo primario.</summary>
    public KpiOperation Operation { get; set; }

    /// <summary>
    /// Campo numérico principal sobre el que se aplica la operación.
    /// Debe ser un nombre de propiedad numérica existente en el array fuente.
    /// </summary>
    public string PrimaryField { get; set; } = string.Empty;

    /// <summary>
    /// Campo numérico secundario, solo para operaciones de porcentaje.
    /// En operación Percentage: resultado = SUM(PrimaryField) / SUM(SecondaryField) * 100.
    /// Null si la operación no es Percentage.
    /// </summary>
    public string? SecondaryField { get; set; }

    /// <summary>
    /// Formato de presentación del resultado.
    /// </summary>
    public KpiDisplayFormat DisplayFormat { get; set; } = KpiDisplayFormat.Number;

    /// <summary>
    /// Número de decimales a mostrar (0-4). Default: 2.
    /// </summary>
    public int DecimalPlaces { get; set; } = 2;

    /// <summary>
    /// Sufijo personalizado para el valor (ej: "t", "€", "kg", "%").
    /// Para porcentaje: si es null, se añade "%" automáticamente.
    /// </summary>
    public string? CustomSuffix { get; set; }

    /// <summary>
    /// Icono Material Design personalizado (ej: "calculate", "percent", "euro").
    /// Si es null, se asigna uno por defecto según la operación.
    /// </summary>
    public string? CustomIcon { get; set; }
}

/// <summary>
/// Operaciones disponibles para KPIs calculados.
/// </summary>
public enum KpiOperation
{
    /// <summary>Suma de todos los valores del campo en el array.</summary>
    Sum,
    /// <summary>Número de elementos en el array.</summary>
    Count,
    /// <summary>Media aritmética del campo en el array.</summary>
    Average,
    /// <summary>Valor mínimo del campo en el array.</summary>
    Min,
    /// <summary>Valor máximo del campo en el array.</summary>
    Max,
    /// <summary>Porcentaje: SUM(PrimaryField) / SUM(SecondaryField) * 100.</summary>
    Percentage
}

/// <summary>
/// Formato de presentación del KPI calculado.
/// </summary>
public enum KpiDisplayFormat
{
    /// <summary>Número con separador de miles y decimales (es-ES).</summary>
    Number,
    /// <summary>Porcentaje (añade sufijo "%").</summary>
    Percent,
    /// <summary>Moneda (añade sufijo "€").</summary>
    Currency
}

VERIFICACIÓN: Compilar. CustomKpiDefinition está listo para ser referenciado desde CustomWidgetDefinition.KpiDefinition.
```

---

## KPI-USER-2 — Servicio: Crear ICustomKpiCalculator

```
FASE KPI-USER-2 — Servicio de cálculo de KPIs

OBJETIVO: Crear un servicio que ejecute la operación definida en un CustomKpiDefinition
sobre los datos de un array del JSON y devuelva el resultado formateado.

--- CREAR: GreenTransit.Application/Features/EcoDataNet/Services/ICustomKpiCalculator.cs ---

namespace GreenTransit.Application.Features.EcoDataNet.Services;

/// <summary>
/// Calcula el valor de un KPI definido por el usuario a partir de datos de un array JSON.
/// </summary>
public interface ICustomKpiCalculator
{
    /// <summary>
    /// Ejecuta la operación del KPI sobre los datos del array y devuelve el resultado.
    /// </summary>
    /// <param name="definition">Definición del KPI (operación, campos, formato).</param>
    /// <param name="arrayData">Datos crudos del array (List&lt;Dictionary&lt;string,object?&gt;&gt;).</param>
    /// <returns>Resultado calculado con valor formateado, valor numérico, unidad e icono.</returns>
    CustomKpiResult Calculate(CustomKpiDefinition definition, List<Dictionary<string, object?>> arrayData);
}

/// <summary>
/// Resultado de un cálculo de KPI.
/// </summary>
public class CustomKpiResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public double NumericValue { get; set; }
    public string FormattedValue { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string Icon { get; set; } = "calculate";
}

--- CREAR: GreenTransit.Application/Features/EcoDataNet/Services/CustomKpiCalculator.cs ---

Implementación:

1. Extraer los valores numéricos del PrimaryField del arrayData:
   - Para cada diccionario en arrayData: obtener el valor de PrimaryField.
   - Convertir a double (manejar int, long, decimal, double, string parseable).
   - Ignorar null/no parseable (con warning si hay muchos).

2. Ejecutar la operación:
   - Sum: values.Sum()
   - Count: arrayData.Count (no necesita PrimaryField, pero igualmente contar)
   - Average: values.Average()
   - Min: values.Min()
   - Max: values.Max()
   - Percentage:
     a. Extraer valores del SecondaryField (mismo procedimiento).
     b. Si SecondaryField es null o vacío → error.
     c. Calcular: SUM(primaryValues) / SUM(secondaryValues) * 100.
     d. Si el denominador es 0 → devolver "N/A" o 0 con warning.

3. Formatear el resultado (cultura es-ES):
   - DisplayFormat.Number: usar "N{DecimalPlaces}" (ej: "N2" → "14.250,50").
   - DisplayFormat.Percent: resultado + "%" (ej: "87,30 %").
   - DisplayFormat.Currency: resultado + "€" (ej: "45.000,00 €").
   - Aplicar CustomSuffix si está definido (sobreescribe el sufijo automático).

4. Icono:
   - Si CustomIcon está definido → usarlo.
   - Si no: Sum → "functions", Count → "tag", Average → "calculate",
     Min → "arrow_downward", Max → "arrow_upward", Percentage → "percent".

5. Devolver CustomKpiResult con Success=true, NumericValue, FormattedValue, Unit, Icon.
   Si hay error → Success=false, ErrorMessage descriptivo.

REGISTRAR en DI: ICustomKpiCalculator como Transient.

VERIFICACIÓN: Compilar. El servicio es lógica pura (Application layer), sin dependencias de infraestructura.
```

---

## KPI-USER-3 — UI Toolbar: Botón "Añadir KPI"

```
FASE KPI-USER-3 — Botón en toolbar para añadir KPIs

OBJETIVO: En modo edición, mostrar un botón "Añadir KPI" junto al de "Añadir gráfico".

--- MODIFICAR: GreenTransit.Web/Components/Pages/EcoDataNet/DataExplorer/LayoutEditorToolbar.razor ---

AÑADIR un botón RadzenButton:
- Texto: "Añadir KPI" (o icono "speed" + texto)
- ButtonStyle: Info o Light
- Posición: al lado de "Añadir gráfico", antes de Guardar/Resetear.
- Al hacer clic: invocar EventCallback OnAddKpi.

VERIFICACIÓN: En modo edición aparecen ambos botones: "Añadir gráfico" y "Añadir KPI".
```

---

## KPI-USER-4 — UI Diálogo: Creación de KPI calculado

```
FASE KPI-USER-4 — Diálogo/panel de creación de KPI calculado

--- CREAR: GreenTransit.Web/Components/Pages/EcoDataNet/DataExplorer/AddKpiDialog.razor ---

PARÁMETROS:
  [Parameter] public JsonDataSchema Schema { get; set; }
  [Parameter] public EventCallback<CustomWidgetDefinition> OnKpiCreated { get; set; }
  [Parameter] public EventCallback OnCancelled { get; set; }

UI DEL DIÁLOGO:

1. **Fuente de datos** (obligatorio):
   RadzenDropDown con Schema.Arrays (igual que en AddChartDialog).
   Al seleccionar: se actualiza la lista de campos numéricos disponibles.

2. **Operación** (obligatorio):
   RadzenDropDown con KpiOperation: Suma, Conteo, Media, Mínimo, Máximo, Porcentaje.
   Etiquetas en español: "Suma (SUM)", "Conteo (COUNT)", "Media (AVG)", "Mínimo (MIN)", "Máximo (MAX)", "Porcentaje (A / B × 100)".

3. **Campo principal** (obligatorio, excepto para Count):
   RadzenDropDown con campos numéricos del array seleccionado.
   Para Count: este selector se oculta o se muestra deshabilitado con nota "El conteo no requiere campo".

4. **Campo secundario** (solo visible si operación == Percentage):
   RadzenDropDown con campos numéricos del array seleccionado (excluyendo el primario si se quiere, o permitiéndolo).
   Etiqueta: "Denominador (total)".

5. **Formato** (obligatorio, default según operación):
   RadzenDropDown con KpiDisplayFormat.
   Default: Number para Sum/Count/Min/Max/Avg, Percent para Percentage.

6. **Decimales** (optional, default 2):
   RadzenNumeric min=0, max=4.

7. **Sufijo** (opcional):
   Input de texto. Placeholder: "t", "€", "kg", etc.

8. **Título** (obligatorio, con default):
   Input de texto. Default: "{Operación} de {Campo}" (ej: "Suma de Tons").

9. **Botones**: "Crear" y "Cancelar".

LÓGICA AL CREAR:

    var definition = new CustomWidgetDefinition
    {
        WidgetId = $"usr_{Guid.NewGuid().ToString("N")[..8]}",
        WidgetType = WidgetType.KpiCard,
        Title = título ingresado,
        ColumnSpan = 4, // KPI cards suelen ser 1/3 de ancho
        SourceArrayName = array seleccionado.Name,
        KpiDefinition = new CustomKpiDefinition
        {
            SourceArrayName = array seleccionado.Name,
            Operation = operación seleccionada,
            PrimaryField = campo primario,
            SecondaryField = campo secundario (o null),
            DisplayFormat = formato seleccionado,
            DecimalPlaces = decimales,
            CustomSuffix = sufijo (o null)
        }
    };
    await OnKpiCreated.InvokeAsync(definition);

VERIFICACIÓN: El diálogo se abre, la selección dinámica de campos funciona,
y al crear se invoca el callback con una definición válida.
```

---

## KPI-USER-5 — Merge + Render: Inyectar y renderizar KPIs calculados

```
FASE KPI-USER-5 — Inyectar KPIs calculados en el dashboard

OBJETIVO: Los KPIs calculados se almacenan como CustomWidgetDefinition con WidgetType=KpiCard
y KpiDefinition != null. Al cargar (merge), hay que calcular el valor y construir el widget.

--- MODIFICAR: GreenTransit.Application/Features/EcoDataNet/Services/LayoutCustomizationService.cs ---

En la lógica que inyecta CustomWidgets (ya implementada en CHART-ADD-4),
AÑADIR manejo para CustomWidgets de tipo KpiCard:

Para cada CustomWidgetDefinition donde WidgetType == KpiCard y KpiDefinition != null:

1. Buscar el array fuente: currentSchema.Arrays.FirstOrDefault(a => a.Name == def.KpiDefinition.SourceArrayName).
2. Si no existe → omitir con warning.
3. Inyectar ICustomKpiCalculator (o recibirlo como dependencia del servicio).
4. Calcular: var result = _kpiCalculator.Calculate(def.KpiDefinition, array.RawData).
5. Si result.Success:
   Construir DynamicWidgetDescriptor:
   - WidgetId = def.WidgetId
   - Type = WidgetType.KpiCard
   - Title = def.Title
   - SortOrder = def.SortOrder ?? 15 (entre los KPIs automáticos, ej SortOrder 15-19)
   - ColumnSpan = def.ColumnSpan (default 4)
   - IsHidden = def.IsHidden
   - KpiValue = result.FormattedValue
   - KpiNumericValue = result.NumericValue
   - KpiUnit = result.Unit
   - KpiIcon = result.Icon
   - KpiColor = (elegir un color de la paleta que no colisione con los KPIs automáticos, o usar un color fijo como "#535497")
6. Si result.Success == false:
   Construir widget KPI con KpiValue = "Error" y Subtitle = result.ErrorMessage.

NOTA: ILayoutCustomizationService necesita la dependencia de ICustomKpiCalculator.
Inyectar en el constructor. Como es lógica pura (Application layer), no hay problema.

--- MODIFICAR: GreenTransit.Web/Components/Pages/EcoDataNet/DataExplorer/EdcDataExplorer.razor ---

Handler de OnKpiCreated (análogo a OnChartCreated):

1. Recibir CustomWidgetDefinition.
2. Calcular el KPI inmediatamente (inyectar ICustomKpiCalculator en la página o llamar al servicio de merge).
3. Construir DynamicWidgetDescriptor con los resultados.
4. Añadir a _currentWidgets.
5. Añadir a _customWidgets.
6. _hasUnsavedChanges = true.

El KPI se renderiza con DynamicKpiCard.razor existente (misma tarjeta que los KPIs automáticos).

VERIFICACIÓN:
- Crear un KPI "Suma de Tons" sobre un array → aparece una KPI card con el valor correcto.
- El valor se recalcula al recargar (porque se relee el JSON y se recalcula en el merge).
- El KPI es editable (título, ancho, ocultar) y eliminable desde WidgetConfigPanel.
```

---

## KPI-USER-6 — Persistencia: Ampliar validador para KPIs

```
FASE KPI-USER-6 — Validación de KPIs en el command

--- MODIFICAR: GreenTransit.Application/Features/EcoDataNet/Validators/SaveExplorerLayoutConfigCommandValidator.cs ---

AMPLIAR la validación de CustomWidgets para KPIs:

Dentro del bloque RuleForEach para CustomWidgets, AÑADIR:

    cw.RuleFor(x => x.KpiDefinition).NotNull()
      .When(x => x.WidgetType == WidgetType.KpiCard)
      .WithMessage("Los KPIs calculados requieren KpiDefinition.");

    cw.RuleFor(x => x.KpiDefinition!.SourceArrayName).NotEmpty()
      .When(x => x.WidgetType == WidgetType.KpiCard && x.KpiDefinition != null);

    cw.RuleFor(x => x.KpiDefinition!.PrimaryField).NotEmpty()
      .When(x => x.WidgetType == WidgetType.KpiCard
                 && x.KpiDefinition != null
                 && x.KpiDefinition.Operation != KpiOperation.Count);

    cw.RuleFor(x => x.KpiDefinition!.SecondaryField).NotEmpty()
      .When(x => x.WidgetType == WidgetType.KpiCard
                 && x.KpiDefinition != null
                 && x.KpiDefinition.Operation == KpiOperation.Percentage)
      .WithMessage("La operación Porcentaje requiere un campo secundario (denominador).");

    cw.RuleFor(x => x.KpiDefinition!.DecimalPlaces).InclusiveBetween(0, 4)
      .When(x => x.WidgetType == WidgetType.KpiCard && x.KpiDefinition != null);

VERIFICACIÓN: Compilar. Un KPI sin PrimaryField (excepto Count) falla validación.
Un Percentage sin SecondaryField falla validación.
```

---

## KPI-USER-7 — Tests unitarios para KPIs calculados

```
FASE KPI-USER-7 — Tests unitarios de cálculo de KPIs

--- CREAR: GreenTransit.Tests/Features/EcoDataNet/DataExplorer/CustomKpiCalculatorTests.cs ---

Tests con xUnit:

1. Calculate_Sum_ReturnsCorrectValue:
   ArrayData: [{ "tons": 100 }, { "tons": 200 }, { "tons": 300 }]
   Definition: Operation=Sum, PrimaryField="tons".
   Assert: NumericValue == 600, Success == true.

2. Calculate_Count_ReturnsElementCount:
   ArrayData: 5 elementos.
   Definition: Operation=Count, PrimaryField="" (no importa).
   Assert: NumericValue == 5.

3. Calculate_Average_ReturnsCorrectValue:
   ArrayData: [{ "tons": 100 }, { "tons": 200 }, { "tons": 300 }]
   Definition: Operation=Average, PrimaryField="tons".
   Assert: NumericValue == 200.

4. Calculate_Min_ReturnsMinimum:
   ArrayData: [{ "tons": 100 }, { "tons": 50 }, { "tons": 300 }]
   Assert: NumericValue == 50.

5. Calculate_Max_ReturnsMaximum:
   Assert: NumericValue == 300.

6. Calculate_Percentage_ReturnsCorrectPercentage:
   ArrayData: [{ "recycled": 80, "total": 200 }, { "recycled": 120, "total": 300 }]
   Definition: Operation=Percentage, PrimaryField="recycled", SecondaryField="total".
   Assert: NumericValue == 40.0 (200/500*100).

7. Calculate_Percentage_DenominatorZero_ReturnsError:
   ArrayData: [{ "recycled": 80, "total": 0 }]
   Assert: Success == false o NumericValue == 0 con warning.

8. Calculate_EmptyArray_ReturnsZeroOrError:
   ArrayData: []
   Assert: NumericValue == 0 o Success == false con mensaje descriptivo.

9. Calculate_FieldNotFound_ReturnsError:
   Definition con PrimaryField="nonExistent".
   Assert: Success == false, ErrorMessage no vacío.

10. Calculate_MixedTypes_ParsesCorrectly:
    ArrayData: [{ "tons": 100 }, { "tons": "200" }, { "tons": null }]
    Definition: Operation=Sum, PrimaryField="tons".
    Assert: NumericValue == 300 (ignora null, parsea string).

11. Calculate_FormatNumber_CorrectSpanishFormat:
    Result: 14250.5, DisplayFormat=Number, DecimalPlaces=2.
    Assert: FormattedValue contiene "14.250,50" (formato es-ES).

12. Calculate_FormatPercent_AddsSuffix:
    Result: 87.3, DisplayFormat=Percent.
    Assert: FormattedValue contiene "%" o Unit == "%".

VERIFICACIÓN: Todos los tests pasan. dotnet test sin errores.
```

---

# ═══════════════════════════════════════════════════
# CIERRE — COMPATIBILIDAD Y MIGRACIÓN
# ═══════════════════════════════════════════════════

## COMPAT-1 — Verificación de retrocompatibilidad

```
FASE COMPAT-1 — Verificación de retrocompatibilidad y migración de JSON antiguo

OBJETIVO: Asegurar que ninguna de las 3 ampliaciones rompe nada existente.

CHECKLIST DE RETROCOMPATIBILIDAD:

1. ☐ JSON sin lat/lon:
   - DashboardLayoutBuilder NO genera widgets Map.
   - Todo el dashboard es idéntico al anterior a esta ampliación.
   - Test: usar un JSON de prueba sin campos de geolocalización → 0 widgets Map.

2. ☐ LayoutConfigJson formato antiguo (array puro):
   - GetExplorerLayoutConfigQueryHandler detecta formato antiguo → Overrides = array, CustomWidgets = [].
   - No hay crash, no hay pérdida de datos.
   - Test: insertar en BD un LayoutConfigJson con formato [{ "widgetId":... }] → se deserializa bien.

3. ☐ LayoutConfigJson sin campos nuevos (CustomMapBinding, CustomWidgets, KpiDefinition):
   - La deserialización con JsonIgnoreCondition.WhenWritingNull deja los campos a null.
   - No hay crash.
   - Test: deserializar un override guardado antes de esta ampliación → los campos nuevos son null, todo funciona.

4. ☐ Widget automático existente sin modificar:
   - WidgetType.Map no interfiere con KpiCard, DataTable, Chart, SectionHeader, KeyValueList, InfoText.
   - El switch de renderizado en EdcDataExplorer.razor tiene un caso para Map; los demás siguen igual.
   - Test: JSON sin lat/lon renderiza exactamente como antes.

5. ☐ Guardar formato nuevo y recargar:
   - Guardar con customWidgets → recargar → customWidgets aparecen correctamente.
   - Test: crear chart + KPI + guardar → cerrar → reabrir → ambos presentes.

6. ☐ Migración implícita:
   - La primera vez que un usuario con config antigua guarda, el JSON se convierte a formato nuevo
     (PersistedLayoutConfig con overrides + customWidgets vacío) automáticamente.
   - No se necesita script de migración explícito.

7. ☐ Schema del asset cambia (proveedor modifica JSON):
   - Widgets Map con lat/lon eliminado: el mapa desaparece de los autogenerados.
     Si había override de mapa guardado → se ignora (obsolete override).
   - CustomWidget Chart referenciando array eliminado → se omite con warning.
   - CustomWidget KPI referenciando campo eliminado → se calcula con error y muestra "Error" en la tarjeta.
   - SchemaChanged badge sigue funcionando.

8. ☐ DI y registro:
   - ICustomKpiCalculator registrado como Transient.
   - ILayoutCustomizationService ahora requiere ICustomKpiCalculator → actualizar el registro DI.
   - Verificar que Program.cs o la extensión de DI incluya los nuevos servicios.

VERIFICACIÓN FINAL:
- dotnet build sin errores ni warnings nuevos.
- dotnet test: todos los tests existentes siguen pasando + todos los nuevos tests pasan.
- Ejecución manual: abrir un asset sin lat/lon → dashboard normal. Abrir un asset CON lat/lon → dashboard + mapa.
  En modo edición: crear chart extra + KPI calculado + guardar + recargar → todo persiste.
```

---

## 📁 RESUMEN DE ARCHIVOS

| Capa | Archivo | Acción | Ampliación |
|------|---------|--------|------------|
| Application | `DTOs/DataExplorer/DynamicWidgetDescriptor.cs` | **MODIFICAR** (WidgetType.Map + propiedades mapa) | A |
| Application | `DTOs/DataExplorer/JsonDataSchema.cs` | **MODIFICAR** (LatitudeProperty, LongitudeProperty en JsonArrayDescriptor) | A |
| Application | `DTOs/DataExplorer/MapFieldBinding.cs` | **CREAR** | A |
| Application | `DTOs/DataExplorer/WidgetLayoutOverride.cs` | **MODIFICAR** (CustomMapBinding) | A |
| Application | `DTOs/DataExplorer/CustomWidgetDefinition.cs` | **CREAR** | B+C |
| Application | `DTOs/DataExplorer/PersistedLayoutConfig.cs` | **CREAR** | B |
| Application | `DTOs/DataExplorer/CustomKpiDefinition.cs` | **CREAR** | C |
| Application | `DTOs/DataExplorer/LayoutConfigDto.cs` | **MODIFICAR** (CustomWidgets list) | B |
| Application | `Services/JsonSchemaAnalyzer.cs` | **MODIFICAR** (detección lat/lon) | A |
| Application | `Services/DashboardLayoutBuilder.cs` | **MODIFICAR** (regla Map) | A |
| Application | `Services/ILayoutCustomizationService.cs` | **MODIFICAR** (firma con customWidgets + schema) | B |
| Application | `Services/LayoutCustomizationService.cs` | **MODIFICAR** (merge MapBinding + inyectar custom widgets + calcular KPIs) | A+B+C |
| Application | `Services/ICustomKpiCalculator.cs` | **CREAR** | C |
| Application | `Services/CustomKpiCalculator.cs` | **CREAR** | C |
| Application | `Queries/GetExplorerLayoutConfigQueryHandler.cs` | **MODIFICAR** (deserialización formato antiguo/nuevo) | B |
| Application | `Commands/SaveExplorerLayoutConfigCommand.cs` | **MODIFICAR** (añadir CustomWidgets) | B |
| Application | `Commands/SaveExplorerLayoutConfigCommandHandler.cs` | **MODIFICAR** (serializar PersistedLayoutConfig) | B |
| Application | `Validators/SaveExplorerLayoutConfigCommandValidator.cs` | **MODIFICAR** (validar CustomWidgets + KPIs) | B+C |
| Web | `DataExplorer/DynamicMap.razor` | **CREAR** | A |
| Web | `DataExplorer/AddChartDialog.razor` | **CREAR** | B |
| Web | `DataExplorer/AddKpiDialog.razor` | **CREAR** | C |
| Web | `DataExplorer/WidgetConfigPanel.razor` | **MODIFICAR** (selectores mapa + botón eliminar usr_) | A+B+C |
| Web | `DataExplorer/LayoutEditorToolbar.razor` | **MODIFICAR** (botones Añadir gráfico + Añadir KPI) | B+C |
| Web | `DataExplorer/EdcDataExplorer.razor` | **MODIFICAR** (renderizar Map + handlers crear/eliminar custom widgets) | A+B+C |
| Web | `wwwroot/js/leaflet-interop.js` | **CREAR** (si se elige Leaflet) | A |
| Web | `Program.cs` (o extensión DI) | **MODIFICAR** (registrar ICustomKpiCalculator) | C |
| Tests | `DataExplorer/MapDetectionTests.cs` | **CREAR** | A |
| Tests | `DataExplorer/MapWidgetBuilderTests.cs` | **CREAR** | A |
| Tests | `DataExplorer/CustomWidgetTests.cs` | **CREAR** | B |
| Tests | `DataExplorer/CustomKpiCalculatorTests.cs` | **CREAR** | C |

---

## 🔄 ESTRATEGIA DE EJECUCIÓN

1. **Sesión 1**: AMP-0 + MAP-1 + MAP-2 + MAP-3 (contexto + DTOs mapa + heurística + builder).
2. **Sesión 2**: MAP-4 + MAP-5 + MAP-6 (override mapa + merge + componente DynamicMap).
3. **Sesión 3**: MAP-7 + MAP-8 (UI config mapa + tests mapa).
4. **Sesión 4**: CHART-ADD-1 + CHART-ADD-2 + CHART-ADD-3 (DTOs custom widgets + toolbar + diálogo chart).
5. **Sesión 5**: CHART-ADD-4 + CHART-ADD-5 + CHART-ADD-6 (merge custom widgets + persistencia + tests).
6. **Sesión 6**: KPI-USER-1 + KPI-USER-2 + KPI-USER-3 + KPI-USER-4 (DTOs KPI + calculador + toolbar + diálogo KPI).
7. **Sesión 7**: KPI-USER-5 + KPI-USER-6 + KPI-USER-7 (merge KPIs + validación + tests).
8. **Sesión 8**: COMPAT-1 (verificación completa de retrocompatibilidad).

> Al inicio de cada sesión, adjuntar este archivo + `COPILOT_CONTEXT.md` + `Mapa_Funcionalidades.md`.
> En las sesiones 2-3, adjuntar los archivos de Application creados en sesión 1.
> En las sesiones 4-5, adjuntar además `LayoutConfigDto.cs`, `PersistedLayoutConfig.cs`, `LayoutCustomizationService.cs`, `EdcDataExplorer.razor`.
> En las sesiones 6-7, adjuntar además `CustomWidgetDefinition.cs`, `CustomKpiDefinition.cs`, `CustomKpiCalculator.cs`.
> En la sesión 8, adjuntar todos los archivos modificados para verificación cruzada.

---

## ⚠️ NOTAS IMPORTANTES

1. **No se crean nuevas tablas.** Las 3 ampliaciones amplían el JSON dentro de `ExplorerLayoutConfigs.LayoutConfigJson` (nvarchar(max)). El tamaño típico sigue siendo < 20 KB incluso con 10+ custom widgets.

2. **Formato de LayoutConfigJson: migración implícita.** Se pasa de array puro `[...]` a objeto `{ "overrides": [...], "customWidgets": [...] }`. La deserialización detecta el formato por el primer carácter (`[` vs `{`). La primera vez que el usuario guarda, se convierte automáticamente a formato nuevo.

3. **WidgetId para widgets de usuario: prefijo "usr_".** Esto permite distinguirlos de los automáticos en SaveLayout() y en la lógica de eliminación. Ejemplo: `"usr_a1b2c3d4"`.

4. **Mapa y librería JS**: Leaflet.js es la opción recomendada (open-source, ligera, sin API key). Si el equipo prefiere otra librería, el componente DynamicMap.razor abstrae la implementación vía JS interop. El patrón es el mismo que DynamicChart.razor con Radzen.

5. **KPIs calculados: recalculados cada vez.** Los valores de los KPIs de usuario se calculan en cada carga del dashboard (al aplicar el merge), porque dependen de los datos actuales del JSON. No se persiste el valor calculado, solo la definición del KPI.

6. **Eliminación de custom widgets**: se hace desde el WidgetConfigPanel (botón "Eliminar widget", visible solo para widgets con WidgetId que empiece por "usr_"). Los widgets automáticos NO se pueden eliminar, solo ocultar.

7. **Count no requiere campo**: en KPI-USER-4, si la operación es Count, el selector de campo primario se deshabilita porque se cuenta el número de elementos del array independientemente del campo.

8. **Donut/Pie en charts de usuario**: igual que en la personalización de binding existente, se fuerza mono-serie (un solo ChartValueField).

9. **Responsive**: los diálogos de creación (AddChartDialog, AddKpiDialog) deben funcionar en pantallas ≥ 768px. En móvil (< 768px) el modo edición ya está desactivado, así que los botones de añadir no aparecen.

10. **Futura mejora (NO implementar ahora)**: permitir crear widgets Map como custom widgets (sin heurística, definiendo lat/lon manualmente). Actualmente solo se crean mapas automáticamente por heurística o se configuran los existentes.
