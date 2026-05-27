# 🤖 Prompt para GitHub Copilot — EDC Data Explorer: Personalización de Data Binding en Gráficos y Tablas Dinámicas

> **Instrucción**: Copia este prompt completo en GitHub Copilot Chat adjuntando los archivos `COPILOT_CONTEXT.md`, `Mapa_Funcionalidades.md`, `DynamicWidgetDescriptor.cs`, `WidgetLayoutOverride.cs`, `DashboardLayoutBuilder.cs`, `LayoutCustomizationService.cs`, `WidgetConfigPanel.razor`, `DynamicChart.razor`, `DynamicDataTable.razor`, `EdcDataExplorer.razor`, `SaveExplorerLayoutConfigCommand.cs` y `SaveExplorerLayoutConfigCommandValidator.cs`.
>
> **Stack**: .NET 10 · Clean Architecture (Domain / Application / Infrastructure / Web) · Blazor Server · Radzen Blazor Components · EF Core · MediatR · FluentValidation · System.Text.Json.
>
> **Prerequisito**: Los módulos "EDC Data Explorer — Dashboard Dinámico" (`Prompt_EDC_DataExplorer_Dashboard_Dinamico.md`) y "EDC Data Explorer — Personalización de Layout con Persistencia" (`Prompt_EDC_DataExplorer_Layout_Customization.md`) deben estar completamente implementados y funcionando. Este prompt **extiende** la personalización de gráficos y tablas existente.
>
> **Ejecuta los prompts en orden**. Cada fase debe compilar antes de pasar a la siguiente.

---

## 📋 ÍNDICE Y ESTADO

| ID | Fase | Descripción | Estado |
|---|---|---|:-:|
| CB-0 | Contexto | Instrucción base — NO genera código | ⬜ |
| CB-1 | DTOs | Ampliar `WidgetLayoutOverride` + nuevos DTOs `ChartFieldBinding` y `TableColumnOverride` | ⬜ |
| CB-2 | WidgetDescriptor | Ampliar `DynamicWidgetDescriptor` con metadatos de campos disponibles | ⬜ |
| CB-3 | LayoutBuilder | Ampliar `DashboardLayoutBuilder` para poblar campos disponibles | ⬜ |
| CB-4 | MergeService | Ampliar `LayoutCustomizationService` para aplicar overrides de binding y columnas | ⬜ |
| CB-5 | DynamicChart | Refactorizar `DynamicChart.razor` para usar binding personalizable | ⬜ |
| CB-6 | DynamicDataTable | Refactorizar `DynamicDataTable.razor` con columnas redimensionables y ocultables | ⬜ |
| CB-7 | UI Gráficos | Ampliar `WidgetConfigPanel.razor` con selectores de campos por tipo de gráfico | ⬜ |
| CB-8 | UI Tablas | Ampliar `WidgetConfigPanel.razor` con selector de columnas visibles para tablas | ⬜ |
| CB-9 | SaveLogic | Ampliar lógica de guardado en `EdcDataExplorer.razor` | ⬜ |
| CB-10 | Validación | Ampliar `SaveExplorerLayoutConfigCommandValidator` | ⬜ |
| CB-11 | Tests | Tests unitarios de binding customization (gráficos + tablas) | ⬜ |

---

## 🎯 OBJETIVO GENERAL

Actualmente el Data Explorer genera gráficos donde el eje X (categoría), el eje Y (valor) y las series se asignan automáticamente por las heurísticas del `DashboardLayoutBuilder`:
- Regla 3: `TemporalProperty` → eje X, `NumericProperties` → series.
- Regla 4: `CategoryProperty` → eje X, `NumericProperties` → series.

El usuario puede cambiar el **tipo de gráfico** (bar → donut, line → area, etc.) desde el `WidgetConfigPanel`, pero **NO puede elegir qué campos del JSON se usan en cada eje/serie**. Esto limita la utilidad cuando el JSON contiene múltiples campos numéricos o de categoría.

Además, las tablas dinámicas (`DynamicDataTable`) muestran TODAS las columnas del array con anchos automáticos. Cuando un array tiene muchas columnas o los datos son largos, las cabeceras se truncan y no se pueden leer, y no hay forma de ocultar columnas irrelevantes.

**Objetivos**:

1. **Gráficos**: permitir al usuario personalizar, para cada gráfico, **qué campo del JSON se usa como categoría (eje X), qué campos se usan como valores (eje Y / series), y en gráficos de Donut/Pie qué campo numérico define los segmentos**.

2. **Tablas**: permitir al usuario **redimensionar columnas arrastrando el borde de la cabecera** y **elegir qué columnas son visibles** (ocultar/mostrar columnas desde el panel de configuración).

3. Toda la personalización se persiste junto con el resto del layout en `ExplorerLayoutConfigs.LayoutConfigJson`.

**Qué varía según tipo de gráfico:**

| Tipo de gráfico | Configuración de campos |
|---|---|
| **BarVertical / BarHorizontal** | Eje X: selector de campo categoría (string o temporal). Eje Y: selector de uno o más campos numéricos (series). |
| **Line / Area** | Eje X: selector de campo temporal o categoría. Series: selector de uno o más campos numéricos. |
| **Donut / Pie** | Categorías (segmentos): selector de campo string. Valor (tamaño del segmento): selector de UN campo numérico. |

**Personalización de tablas:**

| Funcionalidad | Comportamiento |
|---|---|
| **Redimensionar columnas** | Arrastrar el borde derecho de la cabecera de columna para ajustar el ancho. Usa `AllowColumnResize="true"` nativo de RadzenDataGrid. |
| **Ocultar columnas** | Desde el `WidgetConfigPanel`, checklist con todas las columnas disponibles. Las desmarcadas se ocultan de la tabla. |
| **Persistencia** | Los anchos personalizados y las columnas ocultas se guardan en `WidgetLayoutOverride` junto con el resto del layout. |

**Principios clave**:
- No se crean nuevas tablas ni entidades. Se amplía `WidgetLayoutOverride` con campos adicionales.
- Los selectores de campos (gráficos) muestran SOLO campos compatibles con cada rol (categoría → strings/temporales, valor → numéricos).
- Los campos disponibles se extraen del `JsonArrayDescriptor.ItemProperties` del array que alimenta el widget.
- Si el usuario no personaliza nada, todo funciona exactamente igual que ahora (heurísticas automáticas, todas las columnas visibles).
- Cambiar el tipo de gráfico puede resetear la selección de campos si los roles son incompatibles.
- Las columnas de tabla que se ocultan se referencian por `PropertyName` — si el JSON cambia, se ignoran las columnas obsoletas.

---

## CB-0 — Instrucción base (contexto)

```
CONTEXTO DEL PROYECTO:
- Proyecto GreenTransit — .NET 10, Blazor Web App (Server), Radzen Blazor Components, EF Core, SQL Server Azure.
- Clean Architecture: GreenTransit.Domain / Application / Infrastructure / Web / Tests.
- Módulo "EDC Data Explorer" COMPLETAMENTE IMPLEMENTADO con personalización de layout:
  · IJsonSchemaAnalyzer analiza el JSON y produce JsonDataSchema con JsonArrayDescriptor.ItemProperties (lista de JsonPropertyDescriptor por array).
  · IDashboardLayoutBuilder genera List<DynamicWidgetDescriptor> con heurísticas automáticas.
  · DynamicWidgetDescriptor tiene: WidgetId (determinístico), Type (WidgetType), ChartType (ChartSubType), ChartCategoryField, ChartValueFields (List<string>), ChartData (List<Dictionary<string,object?>>).
  · WidgetLayoutOverride tiene: WidgetId, CustomSortOrder?, CustomColumnSpan?, CustomTitle?, IsHidden, CustomChartType?, CustomWidgetType?.
  · LayoutCustomizationService.ApplyOverrides() aplica overrides incluyendo CustomChartType.
  · WidgetConfigPanel.razor permite cambiar tipo de gráfico con RadzenDropDown.
  · DynamicChart.razor renderiza gráficos Radzen según ChartSubType, leyendo ChartCategoryField y ChartValueFields.
  · EdcDataExplorer.razor compara _currentWidgets con _autoGeneratedWidgets para construir overrides en SaveLayout().

OBJETIVO:
Extender la personalización de gráficos para que el usuario pueda elegir qué campos del JSON se usan como categoría (eje X), valores (eje Y / series) y segmentos (Donut/Pie).
Extender la personalización de tablas para que el usuario pueda redimensionar columnas y elegir qué columnas son visibles.
Ambas personalizaciones se persisten en el mismo LayoutConfigJson existente.

REGLAS GENERALES:
1. NO crear nuevas tablas ni entidades de dominio.
2. Ampliar WidgetLayoutOverride con nuevos campos opcionales — se persisten en el mismo LayoutConfigJson.
3. Los selectores de campos (gráficos) se alimentan de la lista de propiedades disponibles en el array fuente.
4. Los selectores de columnas (tablas) se alimentan de TableColumns del widget.
5. Si no hay override de binding → el gráfico/tabla usa exactamente los campos asignados por las heurísticas (comportamiento actual).
6. Nombrar todo en inglés (clases, métodos, variables). Comentarios en español.
7. Usar System.Text.Json para serialización (JsonIgnoreCondition.WhenWritingNull sigue activo).
8. RadzenDataGrid tiene AllowColumnResize nativo — usarlo en lugar de implementar resize manual.

NO generes código aún. Confirma que has entendido el contexto.
```

---

## CB-1 — DTOs: Ampliar WidgetLayoutOverride + ChartFieldBinding + TableColumnOverride

```
FASE CB-1 — Ampliar DTOs para binding de campos en gráficos y personalización de columnas en tablas

OBJETIVO: Añadir DTOs que describan la configuración personalizada de campos de un gráfico
y de columnas de una tabla, e integrarlos en WidgetLayoutOverride.

--- ARCHIVO 1: CREAR GreenTransit.Application/Features/EcoDataNet/DTOs/DataExplorer/ChartFieldBinding.cs ---

namespace GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;

/// <summary>
/// Configuración personalizada de qué campos del JSON alimentan un gráfico.
/// Se almacena como parte de WidgetLayoutOverride cuando el usuario personaliza los ejes/series.
/// Null/vacío en cada propiedad = "usar el campo asignado automáticamente por las heurísticas".
/// </summary>
public class ChartFieldBinding
{
    /// <summary>
    /// Campo personalizado para el eje de categorías (eje X en bar/line, segmentos en donut/pie).
    /// Debe ser un nombre de propiedad existente en el array fuente del gráfico.
    /// Null = usar el CategoryField asignado automáticamente.
    /// </summary>
    public string? CustomCategoryField { get; set; }

    /// <summary>
    /// Campos personalizados para los valores numéricos (eje Y en bar/line, tamaño en donut/pie).
    /// Cada string debe ser un nombre de propiedad numérica existente en el array fuente.
    /// Para Donut/Pie: se usa solo el primer elemento (mono-serie).
    /// Null o vacío = usar los ValueFields asignados automáticamente.
    /// </summary>
    public List<string>? CustomValueFields { get; set; }
}

--- ARCHIVO 2: CREAR GreenTransit.Application/Features/EcoDataNet/DTOs/DataExplorer/TableColumnOverride.cs ---

namespace GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;

/// <summary>
/// Configuración personalizada de una columna individual de una tabla dinámica.
/// Se almacena como parte de WidgetLayoutOverride.CustomTableColumns.
/// Solo se persisten columnas que el usuario ha modificado (ocultar o cambiar ancho).
/// </summary>
public class TableColumnOverride
{
    /// <summary>
    /// Nombre de la propiedad en el JSON (coincide con TableColumnDescriptor.PropertyName).
    /// Es la clave para vincular este override con la columna generada automáticamente.
    /// </summary>
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>
    /// True si el usuario ha ocultado esta columna.
    /// False o null = columna visible (comportamiento por defecto).
    /// </summary>
    public bool IsHidden { get; set; }

    /// <summary>
    /// Ancho personalizado en píxeles tras redimensionar la columna.
    /// Null = usar el ancho automático asignado por el builder.
    /// </summary>
    public int? CustomWidth { get; set; }
}

--- ARCHIVO 3: MODIFICAR GreenTransit.Application/Features/EcoDataNet/DTOs/DataExplorer/WidgetLayoutOverride.cs ---

Añadir DOS nuevas propiedades al final de la clase, ANTES del cierre:

    /// <summary>
    /// Configuración personalizada de los campos del gráfico (ejes, series, segmentos).
    /// Solo aplica a widgets de tipo Chart. Null = usar binding automático.
    /// Se serializa dentro de LayoutConfigJson junto con el resto de overrides.
    /// </summary>
    public ChartFieldBinding? CustomChartBinding { get; set; }

    /// <summary>
    /// Configuración personalizada de columnas de tabla (visibilidad y anchos).
    /// Solo aplica a widgets de tipo DataTable. Null o vacío = todas las columnas visibles con anchos automáticos.
    /// Solo contiene las columnas que el usuario ha modificado (no todas).
    /// </summary>
    public List<TableColumnOverride>? CustomTableColumns { get; set; }

NOTA: Como el JsonSerializerOptions ya tiene JsonIgnoreCondition.WhenWritingNull,
ambos campos se omiten del JSON si no se personalizan → no aumenta el tamaño del LayoutConfigJson
para widgets que no son gráficos/tablas o que no tienen personalización.

VERIFICACIÓN: El proyecto compila. WidgetLayoutOverride tiene 9 propiedades
(WidgetId, CustomSortOrder, CustomColumnSpan, CustomTitle, IsHidden, CustomChartType, CustomWidgetType, CustomChartBinding, CustomTableColumns).
```

---

## CB-2 — WidgetDescriptor: Metadatos de campos disponibles

```
FASE CB-2 — Ampliar DynamicWidgetDescriptor con metadatos de campos disponibles

OBJETIVO: El componente UI necesita saber QUÉ campos están disponibles para seleccionar.
Añadir al DynamicWidgetDescriptor las listas de campos disponibles según tipo.

--- MODIFICAR: GreenTransit.Application/Features/EcoDataNet/DTOs/DataExplorer/DynamicWidgetDescriptor.cs ---

Añadir estas propiedades en la sección "--- Datos para Gráfico ---",
DESPUÉS de ChartData y ANTES de la sección "--- Datos para texto/cabecera ---":

    // --- Metadatos de campos disponibles para personalización de gráfico ---

    /// <summary>
    /// Nombres de todas las propiedades de tipo String o DateTime del array fuente,
    /// candidatas a usarse como eje de categorías (eje X, segmentos).
    /// Se puebla en DashboardLayoutBuilder. Usado por WidgetConfigPanel para el dropdown.
    /// </summary>
    public List<string>? AvailableCategoryFields { get; set; }

    /// <summary>
    /// Nombres de todas las propiedades numéricas del array fuente,
    /// candidatas a usarse como eje de valores (eje Y, series, tamaño de segmento).
    /// Se puebla en DashboardLayoutBuilder. Usado por WidgetConfigPanel para el dropdown.
    /// </summary>
    public List<string>? AvailableValueFields { get; set; }

    /// <summary>
    /// Nombres humanizados de las propiedades disponibles, indexados por nombre de propiedad.
    /// Permite mostrar "Total Tons Processed" en lugar de "totalTonsProcessed" en los dropdowns.
    /// </summary>
    public Dictionary<string, string>? FieldDisplayNames { get; set; }

    /// <summary>
    /// Nombre del array fuente del JSON que alimenta este gráfico.
    /// Se usa para identificar qué array se debe leer si se cambian los campos.
    /// </summary>
    public string? SourceArrayName { get; set; }

VERIFICACIÓN: Compilar. Las nuevas propiedades son nullable → no rompen nada.
```

---

## CB-3 — LayoutBuilder: Poblar campos disponibles

```
FASE CB-3 — Ampliar DashboardLayoutBuilder para poblar metadatos de campos

OBJETIVO: Cuando el builder genera un widget de tipo Chart, debe poblar también
AvailableCategoryFields, AvailableValueFields, FieldDisplayNames y SourceArrayName.

--- MODIFICAR: GreenTransit.Application/Features/EcoDataNet/Services/DashboardLayoutBuilder.cs ---

CAMBIOS en la lógica de generación de widgets Chart (reglas 3, 4 y 8):

Para cada widget de tipo Chart que se genera a partir de un JsonArrayDescriptor:

1. Asignar SourceArrayName = array.Name.

2. Construir AvailableCategoryFields:
   - Incluir TODAS las propiedades del array cuyo PropertyType sea String o DateTime.
   - También incluir propiedades cuyo nombre fue detectado como temporal por el analizador
     (incluso si su tipo es String, como "2026-01" que es string pero temporal).
   - Ordenar: primero la propiedad actualmente usada como ChartCategoryField (para que
     aparezca preseleccionada), luego las demás alfabéticamente.

3. Construir AvailableValueFields:
   - Incluir TODAS las propiedades del array cuyo PropertyType sea Number.
   - Ordenar: primero las que ya están en ChartValueFields (preseleccionadas), luego el resto.

4. Construir FieldDisplayNames:
   - Diccionario con clave = nombre original de propiedad, valor = DisplayName humanizado.
   - Incluir TODAS las propiedades del array (tanto las de categoría como las de valor),
     para que el dropdown pueda mostrar nombres legibles.
   - Usar el mismo método HumanizePropertyName() ya existente.

EJEMPLO con el JSON de prueba:
{
  "wasteByCategory": [
    { "category": "RAEE", "region": "Norte", "tons": 3200, "percentage": 22.5, "cost": 45000 }
  ]
}

Para el gráfico generado a partir de "wasteByCategory":
- AvailableCategoryFields = ["category", "region"]
  (ambas son string con pocos valores únicos)
- AvailableValueFields = ["tons", "percentage", "cost"]
  (todas son numéricas)
- FieldDisplayNames = { "category":"Category", "region":"Region", "tons":"Tons", "percentage":"Percentage", "cost":"Cost" }
- SourceArrayName = "wasteByCategory"
- ChartCategoryField = "category" (el automático, que es la primera string con ≤20 valores únicos)
- ChartValueFields = ["tons", "percentage", "cost"] (todos los numéricos por heurística)

Con estos metadatos, el WidgetConfigPanel podrá mostrar dropdowns con opciones reales.

VERIFICACIÓN: Compilar. Los widgets Chart ahora tienen AvailableCategoryFields y AvailableValueFields
poblados con las propiedades del array fuente.
```

---

## CB-4 — MergeService: Aplicar overrides de binding y columnas

```
FASE CB-4 — Ampliar LayoutCustomizationService para aplicar CustomChartBinding y CustomTableColumns

OBJETIVO: Cuando se aplican overrides, si un widget Chart tiene CustomChartBinding,
aplicar los campos personalizados. Si un widget DataTable tiene CustomTableColumns,
aplicar visibilidad y anchos de columna.

--- MODIFICAR: GreenTransit.Application/Features/EcoDataNet/Services/LayoutCustomizationService.cs ---

En el método ApplyOverrides(), dentro del bloque donde se aplican los overrides de cada widget:

=== PARTE A — GRÁFICOS (DESPUÉS de la línea que aplica CustomChartType) ===

AÑADIR:

      - Si override.CustomChartBinding != null y widget.Type == WidgetType.Chart:
        a. Si CustomChartBinding.CustomCategoryField != null
           Y CustomChartBinding.CustomCategoryField está en widget.AvailableCategoryFields:
           → widget.ChartCategoryField = CustomChartBinding.CustomCategoryField.
        b. Si CustomChartBinding.CustomValueFields != null y tiene al menos 1 elemento:
           → Filtrar: quedarse solo con los campos que existan en widget.AvailableValueFields.
           → Si tras filtrar queda al menos 1 campo:
             widget.ChartValueFields = campos filtrados válidos.
           → Si tras filtrar no queda ninguno (todos eran obsoletos):
             NO modificar widget.ChartValueFields (mantener automáticos).
             Loguear warning: "Custom value fields for widget {WidgetId} contain obsolete fields, using defaults."
        c. Para Donut/Pie: si ChartValueFields tiene más de 1 campo tras el override,
           tomar solo el primero (Donut/Pie es mono-serie).

=== PARTE B — TABLAS (DESPUÉS de la parte A) ===

AÑADIR:

      - Si override.CustomTableColumns != null y widget.Type == WidgetType.DataTable
        y widget.TableColumns != null:
        a. Crear un diccionario de CustomTableColumns por PropertyName para lookup rápido.
        b. Para cada columna en widget.TableColumns:
           - Buscar si existe un TableColumnOverride con ese PropertyName.
           - Si existe y IsHidden == true:
             → Marcar la columna. Para esto, AÑADIR propiedad `bool IsHidden` a `TableColumnDescriptor`
               (ver nota más abajo).
           - Si existe y CustomWidth != null:
             → widget.TableColumns[i].Width = override.CustomWidth.
           - Si NO existe override para esta columna: dejarla tal cual.
        c. Ignorar TableColumnOverrides cuyo PropertyName no coincida con ninguna columna actual
           (columnas obsoletas por cambio de esquema del JSON).

NOTA — Añadir propiedad IsHidden a TableColumnDescriptor:
Modificar GreenTransit.Application/Features/EcoDataNet/DTOs/DataExplorer/DynamicWidgetDescriptor.cs
(donde está definido TableColumnDescriptor), añadir:

    /// <summary>True si el usuario ha ocultado esta columna. La UI no la renderiza.</summary>
    public bool IsHidden { get; set; }

RAZONAMIENTO de la validación:
- Al persistir el binding/columnas, se guardan nombres de propiedades del JSON.
- Si el proveedor cambia la estructura, los bindings/columnas guardados pueden referenciar
  campos que ya no existen.
- Por eso se valida contra las propiedades actuales y se hace fallback al automático.

VERIFICACIÓN:
- Override con CustomCategoryField válido → se aplica.
- Override con CustomCategoryField inválido → se ignora.
- Override con CustomValueFields parcialmente válido → se aplican solo los válidos.
- Override con CustomTableColumns: columna oculta → IsHidden=true, columna con ancho → Width cambia.
- Override con CustomTableColumns referenciando columna obsoleta → se ignora.
- Sin CustomChartBinding ni CustomTableColumns → comportamiento idéntico al actual.
```

---

## CB-5 — DynamicChart: Refactorizar para usar binding personalizable

```
FASE CB-5 — Refactorizar DynamicChart.razor

OBJETIVO: Asegurar que DynamicChart.razor use siempre Widget.ChartCategoryField y
Widget.ChartValueFields para renderizar, que ya reflejan el binding personalizado
(aplicado por LayoutCustomizationService o los valores automáticos originales).

--- REVISAR: GreenTransit.Web/Components/Pages/EcoDataNet/DataExplorer/DynamicChart.razor ---

VERIFICAR que el componente YA usa:
- Widget.ChartCategoryField para el eje X / categorías.
- Widget.ChartValueFields para las series / valores.
- Widget.ChartData para los datos.

Si el componente ya lee estos campos correctamente, NO necesita cambios en esta fase.
Los overrides de binding ya se aplican ANTES del renderizado (en ApplyOverrides del merge service),
así que para DynamicChart el widget llega con los campos correctos ya configurados.

POSIBLE CAMBIO NECESARIO para Donut/Pie:
Si actualmente el componente para Donut/Pie asume que ChartValueFields tiene exactamente 1 elemento,
verificar que funcione correctamente:
- Si ChartValueFields tiene 1 elemento → comportamiento actual (mono-serie). OK.
- Si por algún edge case tiene 0 o null → fallback: buscar la primera propiedad numérica en ChartData.

VERIFICACIÓN: El gráfico se renderiza correctamente con los campos por defecto.
Ningún test existente se rompe.
```

---

## CB-6 — DynamicDataTable: Columnas redimensionables y ocultables

```
FASE CB-6 — Refactorizar DynamicDataTable.razor

OBJETIVO: Habilitar redimensionado de columnas por arrastre y filtrar columnas ocultas.

--- MODIFICAR: GreenTransit.Web/Components/Pages/EcoDataNet/DataExplorer/DynamicDataTable.razor ---

CAMBIO 1 — Habilitar resize nativo de RadzenDataGrid:

Añadir el atributo AllowColumnResize="true" y ColumnResized event al RadzenDataGrid:

    <RadzenDataGrid Data="@Widget.TableData" TItem="Dictionary<string, object?>"
                    AllowPaging="true" PageSize="10"
                    AllowSorting="true" AllowFiltering="true"
                    AllowColumnResize="true"
                    ColumnResized="@OnColumnResized"
                    FilterMode="FilterMode.Simple"
                    Style="width:100%">

NOTA: RadzenDataGrid con AllowColumnResize="true" permite al usuario arrastrar el borde
derecho de cada cabecera de columna para ajustar el ancho. Esto funciona out-of-the-box
con Radzen Blazor Components.

CAMBIO 2 — Filtrar columnas ocultas:

Cambiar el @foreach para excluir columnas con IsHidden=true:

    @foreach (var col in Widget.TableColumns!.Where(c => !c.IsHidden))
    {
        <RadzenDataGridColumn TItem="Dictionary<string, object?>"
                              Title="@col.Title"
                              Property="@col.PropertyName"
                              Width="@(col.Width.HasValue ? $"{col.Width}px" : null)"
                              Sortable="true" Filterable="true"
                              Resizable="true">
            <Template Context="row">
                @FormatCellValue(row, col)
            </Template>
        </RadzenDataGridColumn>
    }

CAMBIO 3 — Capturar evento de resize para propagar el cambio:

Añadir un parámetro callback para notificar al padre cuando una columna se redimensiona:

[Parameter] public EventCallback<(string PropertyName, int NewWidth)> OnColumnWidthChanged { get; set; }

private async Task OnColumnResized(DataGridColumnResizedEventArgs<Dictionary<string, object?>> args)
{
    // args.Column.Property contiene el PropertyName de la columna redimensionada
    // args.Width contiene el nuevo ancho en píxeles
    var propertyName = args.Column.GetFilterProperty(); // o args.Column.Property según la versión de Radzen
    if (!string.IsNullOrEmpty(propertyName) && args.Width.HasValue)
    {
        // Actualizar el ancho en TableColumns del widget
        var col = Widget.TableColumns?.FirstOrDefault(c => c.PropertyName == propertyName);
        if (col != null)
        {
            col.Width = (int)args.Width.Value;
        }

        await OnColumnWidthChanged.InvokeAsync((propertyName, (int)args.Width.Value));
    }
}

NOTA SOBRE RadzenDataGrid.ColumnResized:
- Verificar la API exacta de RadzenDataGrid para el evento ColumnResized.
  En versiones recientes de Radzen Blazor, el evento es:
  `ColumnResized="@OnColumnResized"` con tipo `DataGridColumnResizedEventArgs<TItem>`.
- Si la API difiere, adaptar el nombre del evento y los tipos según la versión instalada.
- Consultar la documentación de Radzen Blazor DataGrid ColumnResize para la firma exacta.

VERIFICACIÓN:
- Las columnas se pueden redimensionar arrastrando el borde derecho de la cabecera.
- Las columnas marcadas como IsHidden=true no se renderan.
- Al redimensionar, el evento propaga el nuevo ancho al padre.
```

---

## CB-7 — UI Gráficos: Selectores de campos en WidgetConfigPanel

```
FASE CB-7 — Ampliar WidgetConfigPanel.razor con selectores de data binding para gráficos

OBJETIVO: Añadir dropdowns de selección de campos (categoría, valores) en el panel de
configuración de widgets de tipo Chart, debajo del selector de tipo de gráfico.

--- MODIFICAR: GreenTransit.Web/Components/Pages/EcoDataNet/DataExplorer/WidgetConfigPanel.razor ---

DENTRO del bloque @if (Widget.Type == WidgetType.Chart), DESPUÉS del dropdown de "Tipo de gráfico"
y ANTES del botón "Ocultar widget", AÑADIR:

        @* --- Sección de personalización de campos del gráfico --- *@
        @if (Widget.AvailableCategoryFields != null && Widget.AvailableCategoryFields.Any())
        {
            <RadzenText TextStyle="TextStyle.Overline" Style="margin-top:0.75rem; margin-bottom:0.25rem; color:var(--gt-stone-green)">
                Datos del gráfico
            </RadzenText>

            @* Selector de campo para eje X / categorías / segmentos *@
            <RadzenFormField Text="@GetCategoryFieldLabel()" Variant="Variant.Text" Style="width:100%">
                <ChildContent>
                    <RadzenDropDown TValue="string"
                                    @bind-Value="Widget.ChartCategoryField"
                                    Data="@GetCategoryFieldOptions()"
                                    TextProperty="Label" ValueProperty="Value"
                                    Style="width:100%"
                                    Change="@(() => NotifyChange())" />
                </ChildContent>
            </RadzenFormField>
        }

        @if (Widget.AvailableValueFields != null && Widget.AvailableValueFields.Any())
        {
            @* Selector de campo(s) para eje Y / valores / tamaño de segmento *@
            @if (IsMonoSeriesChart())
            {
                @* Donut / Pie → solo UN campo numérico *@
                <RadzenFormField Text="Valor (tamaño del segmento)" Variant="Variant.Text" Style="width:100%">
                    <ChildContent>
                        <RadzenDropDown TValue="string"
                                        Value="@GetSingleValueField()"
                                        Data="@GetValueFieldOptions()"
                                        TextProperty="Label" ValueProperty="Value"
                                        Style="width:100%"
                                        Change="@((object val) => OnSingleValueFieldChanged((string)val))" />
                    </ChildContent>
                </RadzenFormField>
            }
            else
            {
                @* Bar / Line / Area → uno o más campos numéricos (multi-select) *@
                <RadzenFormField Text="Series de valores (eje Y)" Variant="Variant.Text" Style="width:100%">
                    <ChildContent>
                        <RadzenDropDown TValue="IEnumerable<string>"
                                        @bind-Value="Widget.ChartValueFields"
                                        Data="@GetValueFieldOptions()"
                                        TextProperty="Label" ValueProperty="Value"
                                        Multiple="true"
                                        Style="width:100%"
                                        Change="@(() => NotifyChange())"
                                        Placeholder="Seleccione al menos un campo" />
                    </ChildContent>
                </RadzenFormField>
            }
        }

MÉTODOS HELPER a añadir en el bloque @code:

/// <summary>
/// Etiqueta del dropdown de categoría según el tipo de gráfico.
/// </summary>
private string GetCategoryFieldLabel() => Widget.ChartType switch
{
    ChartSubType.Donut or ChartSubType.Pie => "Campo de segmentos",
    ChartSubType.BarHorizontal => "Eje Y (categorías)",
    _ => "Eje X (categorías)"  // BarVertical, Line, Area
};

/// <summary>
/// True si el gráfico actual es mono-serie (Donut/Pie → un solo campo numérico).
/// </summary>
private bool IsMonoSeriesChart() =>
    Widget.ChartType is ChartSubType.Donut or ChartSubType.Pie;

/// <summary>
/// Genera las opciones para el dropdown de campo de categoría.
/// Muestra el DisplayName humanizado, devuelve el nombre de propiedad original.
/// </summary>
private List<object> GetCategoryFieldOptions()
{
    if (Widget.AvailableCategoryFields == null) return new();
    return Widget.AvailableCategoryFields.Select(f => (object)new
    {
        Label = Widget.FieldDisplayNames?.GetValueOrDefault(f, f) ?? f,
        Value = f
    }).ToList();
}

/// <summary>
/// Genera las opciones para el dropdown de campos de valor.
/// </summary>
private List<object> GetValueFieldOptions()
{
    if (Widget.AvailableValueFields == null) return new();
    return Widget.AvailableValueFields.Select(f => (object)new
    {
        Label = Widget.FieldDisplayNames?.GetValueOrDefault(f, f) ?? f,
        Value = f
    }).ToList();
}

/// <summary>
/// Para Donut/Pie: obtiene el primer (único) campo de valor seleccionado.
/// </summary>
private string GetSingleValueField() =>
    Widget.ChartValueFields?.FirstOrDefault() ?? "";

/// <summary>
/// Para Donut/Pie: al cambiar el campo de valor, reemplazar la lista con un solo elemento.
/// </summary>
private void OnSingleValueFieldChanged(string newField)
{
    Widget.ChartValueFields = new List<string> { newField };
    NotifyChange();
}

LÓGICA ADICIONAL — Resetear binding al cambiar tipo de gráfico:

MODIFICAR el handler del dropdown de "Tipo de gráfico" existente.
Cuando el usuario cambia de tipo de gráfico, evaluar si los campos actuales siguen siendo válidos:

private void OnChartTypeChanged(object newType)
{
    var newChartType = (ChartSubType)newType;
    var oldChartType = Widget.ChartType;
    Widget.ChartType = newChartType;

    // Si pasa de multi-serie (Bar/Line/Area) a mono-serie (Donut/Pie),
    // recortar ChartValueFields a solo el primer elemento.
    if (IsMonoSeriesChartType(newChartType) && !IsMonoSeriesChartType(oldChartType))
    {
        if (Widget.ChartValueFields?.Count > 1)
        {
            Widget.ChartValueFields = new List<string> { Widget.ChartValueFields.First() };
        }
    }

    NotifyChange();
}

private static bool IsMonoSeriesChartType(ChartSubType? type) =>
    type is ChartSubType.Donut or ChartSubType.Pie;

Reemplazar el Change handler del dropdown de tipo de gráfico existente:
  ANTES:  Change="@(() => NotifyChange())"
  DESPUÉS: Change="@OnChartTypeChanged"

ESTILOS CSS adicionales en .razor.css (si necesario):
- Los nuevos dropdowns heredan el mismo estilo que el de tipo de gráfico.
- No se necesita CSS adicional; los RadzenFormField ya están dentro del .widget-config-panel.

VERIFICACIÓN:
- En modo edición, los gráficos de barras muestran dropdowns de "Eje X" y "Series de valores (eje Y)".
- Los gráficos Donut/Pie muestran "Campo de segmentos" y "Valor (tamaño del segmento)" (mono-select).
- Los gráficos Line/Area muestran "Eje X" y "Series de valores" (multi-select).
- Al cambiar un campo, el gráfico se re-renderiza con los nuevos datos.
- Al cambiar de BarVertical a Donut, las series se recortan a 1.
```

---

## CB-8 — UI Tablas: Selector de columnas visibles en WidgetConfigPanel

```
FASE CB-8 — Ampliar WidgetConfigPanel.razor con selector de columnas para tablas

OBJETIVO: Añadir una sección en el panel de configuración de widgets DataTable
que permita al usuario marcar/desmarcar columnas visibles.

--- MODIFICAR: GreenTransit.Web/Components/Pages/EcoDataNet/DataExplorer/WidgetConfigPanel.razor ---

DESPUÉS del bloque @if (Widget.Type == WidgetType.Chart) { ... } (que contiene los selectores
de gráficos añadidos en CB-7), AÑADIR un bloque para DataTable:

        @* --- Sección de personalización de columnas para tablas --- *@
        @if (Widget.Type == WidgetType.DataTable && Widget.TableColumns != null && Widget.TableColumns.Any())
        {
            <RadzenText TextStyle="TextStyle.Overline"
                        Style="margin-top:0.75rem; margin-bottom:0.25rem; color:var(--gt-stone-green)">
                Columnas visibles
            </RadzenText>

            <div class="table-columns-selector">
                @foreach (var col in Widget.TableColumns)
                {
                    <div class="column-toggle-row">
                        <RadzenCheckBox TValue="bool"
                                        Value="@(!col.IsHidden)"
                                        Change="@((bool visible) => OnColumnVisibilityChanged(col, visible))"
                                        Name="@($"col_{col.PropertyName}")" />
                        <RadzenLabel Text="@col.Title"
                                     Component="@($"col_{col.PropertyName}")"
                                     Style="@(col.IsHidden ? "text-decoration:line-through; color:var(--gt-stone-green)" : "")" />
                    </div>
                }
            </div>

            @* Botones rápidos *@
            <RadzenStack Orientation="Orientation.Horizontal" Gap="0.25rem"
                         Style="margin-top:0.5rem">
                <RadzenButton Text="Todas" Size="ButtonSize.ExtraSmall"
                              ButtonStyle="ButtonStyle.Light" Variant="Variant.Text"
                              Click="@ShowAllColumns" />
                <RadzenButton Text="Ninguna" Size="ButtonSize.ExtraSmall"
                              ButtonStyle="ButtonStyle.Light" Variant="Variant.Text"
                              Click="@HideAllColumns" />
            </RadzenStack>
        }

MÉTODOS HELPER a añadir en el bloque @code:

    private void OnColumnVisibilityChanged(TableColumnDescriptor col, bool visible)
    {
        col.IsHidden = !visible;
        NotifyChange();
    }

    private void ShowAllColumns()
    {
        if (Widget.TableColumns == null) return;
        foreach (var col in Widget.TableColumns)
            col.IsHidden = false;
        NotifyChange();
    }

    private void HideAllColumns()
    {
        if (Widget.TableColumns == null) return;
        // Ocultar todas EXCEPTO la primera (para evitar tabla vacía)
        for (int i = 0; i < Widget.TableColumns.Count; i++)
            Widget.TableColumns[i].IsHidden = i > 0;
        NotifyChange();
    }

ESTILOS CSS adicionales en .razor.css:

.table-columns-selector {
    max-height: 200px;
    overflow-y: auto;
    border: 1px solid var(--rz-base-300);
    border-radius: 4px;
    padding: 0.5rem;
}
.column-toggle-row {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    padding: 2px 0;
}
.column-toggle-row:hover {
    background: var(--rz-base-200);
    border-radius: 4px;
}

VERIFICACIÓN:
- En modo edición, los widgets DataTable muestran una lista de checkboxes con las columnas.
- Desmarcar una columna → se oculta de la tabla inmediatamente.
- "Todas" restaura todas. "Ninguna" oculta todas menos la primera.
- La lista tiene scroll si hay muchas columnas (max-height: 200px).
```

---

## CB-9 — SaveLogic: Ampliar lógica de guardado

```
FASE CB-9 — Ampliar SaveLayout() en EdcDataExplorer.razor

OBJETIVO: Al construir los overrides para guardar, incluir CustomChartBinding si el usuario
ha cambiado los campos del gráfico, y CustomTableColumns si ha modificado columnas de tabla.

--- MODIFICAR: GreenTransit.Web/Components/Pages/EcoDataNet/DataExplorer/EdcDataExplorer.razor ---

En el método SaveLayout(), dentro del bucle foreach que compara _currentWidgets con _autoGeneratedWidgets,
DESPUÉS de la línea que compara ChartType, AÑADIR:

           if (widget.Type == WidgetType.Chart)
           {
               bool categoryChanged = widget.ChartCategoryField != auto.ChartCategoryField;
               bool valuesChanged = !AreValueFieldsEqual(widget.ChartValueFields, auto.ChartValueFields);

               if (categoryChanged || valuesChanged)
               {
                   ov.CustomChartBinding = new ChartFieldBinding();
                   if (categoryChanged)
                       ov.CustomChartBinding.CustomCategoryField = widget.ChartCategoryField;
                   if (valuesChanged)
                       ov.CustomChartBinding.CustomValueFields = widget.ChartValueFields?.ToList();
                   hasChanges = true;
               }
           }

           // Comparar columnas de tabla
           if (widget.Type == WidgetType.DataTable && widget.TableColumns != null && auto.TableColumns != null)
           {
               var tableOverrides = new List<TableColumnOverride>();
               foreach (var col in widget.TableColumns)
               {
                   var autoCol = auto.TableColumns.FirstOrDefault(c => c.PropertyName == col.PropertyName);
                   if (autoCol == null) continue;

                   bool hidden = col.IsHidden;
                   bool widthChanged = col.Width != autoCol.Width;

                   if (hidden || widthChanged)
                   {
                       tableOverrides.Add(new TableColumnOverride
                       {
                           PropertyName = col.PropertyName,
                           IsHidden = hidden,
                           CustomWidth = widthChanged ? col.Width : null
                       });
                   }
               }
               if (tableOverrides.Any())
               {
                   ov.CustomTableColumns = tableOverrides;
                   hasChanges = true;
               }
           }

AÑADIR método helper para manejar el evento de resize de columna propagado desde DynamicDataTable:

    /// <summary>
    /// Callback invocado por DynamicDataTable cuando el usuario redimensiona una columna.
    /// Actualiza el ancho en el widget y marca cambios pendientes.
    /// </summary>
    private void OnTableColumnResized((string PropertyName, int NewWidth) args)
    {
        // Buscar el widget que contiene esta columna — el evento viene del componente hijo
        // La actualización del Width en TableColumns ya se hizo en DynamicDataTable.OnColumnResized
        _hasUnsavedChanges = true;
        StateHasChanged();
    }

ACTUALIZAR el renderizado de DynamicDataTable en el bucle de widgets para pasar el callback:

En el @switch(widget.Type), case WidgetType.DataTable:

    <DynamicDataTable Widget="widget"
                      OnColumnWidthChanged="OnTableColumnResized" />

AÑADIR método helper en la clase:

    /// <summary>
    /// Compara dos listas de campos de valor ignorando orden.
    /// </summary>
    private static bool AreValueFieldsEqual(List<string>? a, List<string>? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        if (a.Count != b.Count) return false;
        return a.OrderBy(x => x).SequenceEqual(b.OrderBy(x => x));
    }

VERIFICACIÓN:
- Si el usuario no cambia los campos del gráfico → no se genera CustomChartBinding.
- Si cambia solo el CategoryField → CustomChartBinding tiene solo CustomCategoryField.
- Si cambia solo los ValueFields → CustomChartBinding tiene solo CustomValueFields.
- Si cambia ambos → CustomChartBinding tiene ambos.
- Si oculta una columna de tabla → se genera TableColumnOverride con IsHidden=true.
- Si redimensiona una columna → se genera TableColumnOverride con CustomWidth.
- Si oculta Y redimensiona → ambos campos en el mismo TableColumnOverride.
- Si no modifica columnas → no se genera CustomTableColumns.
- El JSON resultante en LayoutConfigJson es mínimo (campos null se omiten).
```

---

## CB-10 — Validación: Ampliar FluentValidation

```
FASE CB-10 — Ampliar SaveExplorerLayoutConfigCommandValidator

OBJETIVO: Validar los campos de CustomChartBinding y CustomTableColumns.

--- MODIFICAR: GreenTransit.Application/Features/EcoDataNet/Validators/SaveExplorerLayoutConfigCommandValidator.cs ---

Dentro de la regla que valida cada override (RuleForEach sobre Overrides), AÑADIR:

        // Validación de CustomChartBinding
        When(x => x.CustomChartBinding != null, () =>
        {
            RuleFor(x => x.CustomChartBinding!.CustomCategoryField)
                .MaximumLength(256)
                .When(x => x.CustomChartBinding!.CustomCategoryField != null);

            RuleFor(x => x.CustomChartBinding!.CustomValueFields)
                .Must(fields => fields == null || fields.Count <= 10)
                .WithMessage("Un gráfico no puede tener más de 10 series de valores.")
                .When(x => x.CustomChartBinding!.CustomValueFields != null);

            RuleForEach(x => x.CustomChartBinding!.CustomValueFields)
                .MaximumLength(256)
                .When(x => x.CustomChartBinding!.CustomValueFields != null);
        });

        // Validación de CustomTableColumns
        When(x => x.CustomTableColumns != null, () =>
        {
            RuleFor(x => x.CustomTableColumns)
                .Must(cols => cols == null || cols.Count <= 100)
                .WithMessage("Una tabla no puede tener más de 100 overrides de columna.");

            RuleForEach(x => x.CustomTableColumns!)
                .ChildRules(col =>
                {
                    col.RuleFor(c => c.PropertyName)
                        .NotEmpty()
                        .MaximumLength(256);

                    col.RuleFor(c => c.CustomWidth)
                        .InclusiveBetween(30, 2000)
                        .When(c => c.CustomWidth.HasValue)
                        .WithMessage("El ancho de columna debe estar entre 30 y 2000 píxeles.");
                });
        });

NOTA: No se valida que los nombres de campo existan en el JSON porque:
1. La validación de existencia se hace en LayoutCustomizationService.ApplyOverrides()
   (que ignora campos obsoletos y hace fallback al automático).
2. El JSON del asset puede cambiar entre el guardado y la carga.
3. Los nombres de campo vienen del JSON del proveedor externo — no son controlables.

VERIFICACIÓN: Compilar. El validator acepta CustomChartBinding válido y rechaza
MaximumLength o >10 series.
```

---

## CB-11 — Tests unitarios

```
FASE CB-11 — Tests unitarios de binding customization (gráficos + tablas)

UBICACIÓN: GreenTransit.Tests/Features/EcoDataNet/DataExplorer/

--- ARCHIVO 1: CREAR ChartFieldBindingTests.cs ---

Tests con xUnit para LayoutCustomizationService (ampliar o crear nuevo archivo):

1. ApplyOverrides_CustomCategoryField_Valid_AppliesField:
   Widget con AvailableCategoryFields=["category","region"],
   Override con CustomChartBinding.CustomCategoryField="region".
   Assert: widget.ChartCategoryField == "region".

2. ApplyOverrides_CustomCategoryField_Invalid_IgnoresField:
   Widget con AvailableCategoryFields=["category","region"],
   Override con CustomChartBinding.CustomCategoryField="nonExistentField".
   Assert: widget.ChartCategoryField no cambia (mantiene el automático).

3. ApplyOverrides_CustomValueFields_Valid_AppliesFields:
   Widget con AvailableValueFields=["tons","percentage","cost"],
   Override con CustomChartBinding.CustomValueFields=["cost","tons"].
   Assert: widget.ChartValueFields contiene exactamente ["cost","tons"].

4. ApplyOverrides_CustomValueFields_PartiallyValid_AppliesOnlyValid:
   Widget con AvailableValueFields=["tons","percentage"],
   Override con CustomChartBinding.CustomValueFields=["tons","obsoleteField"].
   Assert: widget.ChartValueFields contiene solo ["tons"].

5. ApplyOverrides_CustomValueFields_AllObsolete_KeepsDefaults:
   Widget con AvailableValueFields=["tons","percentage"],
   Override con CustomChartBinding.CustomValueFields=["foo","bar"].
   Assert: widget.ChartValueFields no cambia (mantiene los automáticos).

6. ApplyOverrides_DonutChart_MultipleValueFields_TakesFirst:
   Widget tipo Donut con AvailableValueFields=["tons","percentage","cost"],
   Override con CustomChartBinding.CustomValueFields=["cost","tons"].
   Assert: widget.ChartValueFields contiene solo ["cost"] (mono-serie).

7. ApplyOverrides_NoChartBinding_NoChange:
   Widget Chart sin CustomChartBinding en el override.
   Assert: ChartCategoryField y ChartValueFields idénticos al automático.

--- ARCHIVO 2: AMPLIAR DashboardLayoutBuilderTests.cs ---

Añadir test:

8. Build_ChartWidget_PopulatesAvailableFields:
   Schema con array que tiene 2 strings ("category","region") y 3 numbers ("tons","pct","cost").
   Assert: widget Chart tiene AvailableCategoryFields.Count == 2,
           AvailableValueFields.Count == 3,
           FieldDisplayNames.Count >= 5,
           SourceArrayName no vacío.

--- ARCHIVO 3: CREAR TableColumnOverrideTests.cs ---

Tests con xUnit para LayoutCustomizationService (personalización de tablas):

9. ApplyOverrides_HiddenColumn_SetsIsHidden:
   Widget DataTable con TableColumns=["category","tons","percentage"],
   Override con CustomTableColumns=[{PropertyName="tons", IsHidden=true}].
   Assert: columna "tons" tiene IsHidden==true, las demás IsHidden==false.

10. ApplyOverrides_CustomColumnWidth_AppliesWidth:
    Widget DataTable con TableColumns (todos Width=null),
    Override con CustomTableColumns=[{PropertyName="category", CustomWidth=200}].
    Assert: columna "category" tiene Width==200, las demás Width==null.

11. ApplyOverrides_HiddenAndResizedColumn_AppliesBoth:
    Override con CustomTableColumns=[{PropertyName="percentage", IsHidden=true, CustomWidth=150}].
    Assert: columna "percentage" tiene IsHidden==true Y Width==150.

12. ApplyOverrides_ObsoleteColumnOverride_Ignored:
    Widget con TableColumns=["category","tons"],
    Override con CustomTableColumns=[{PropertyName="nonExistent", IsHidden=true}].
    Assert: ninguna columna afectada, no hay excepciones.

13. ApplyOverrides_NoTableColumnOverrides_ColumnsUnchanged:
    Widget DataTable sin CustomTableColumns en el override.
    Assert: todas las columnas con IsHidden==false y Width original.

14. ApplyOverrides_MultipleColumnOverrides_AppliedCorrectly:
    Override con CustomTableColumns para 3 columnas (2 ocultas, 1 con ancho personalizado).
    Assert: cada columna tiene el estado correcto.

VERIFICACIÓN: Todos los tests pasan. dotnet test sin errores.
```

---

## 📁 RESUMEN DE ARCHIVOS

| Capa | Archivo | Acción |
|------|---------|--------|
| Application | `Features/EcoDataNet/DTOs/DataExplorer/ChartFieldBinding.cs` | **CREAR** |
| Application | `Features/EcoDataNet/DTOs/DataExplorer/TableColumnOverride.cs` | **CREAR** |
| Application | `Features/EcoDataNet/DTOs/DataExplorer/WidgetLayoutOverride.cs` | **MODIFICAR** (añadir `CustomChartBinding` + `CustomTableColumns`) |
| Application | `Features/EcoDataNet/DTOs/DataExplorer/DynamicWidgetDescriptor.cs` | **MODIFICAR** (añadir `AvailableCategoryFields`, `AvailableValueFields`, `FieldDisplayNames`, `SourceArrayName` en Chart; añadir `IsHidden` en `TableColumnDescriptor`) |
| Application | `Features/EcoDataNet/Services/DashboardLayoutBuilder.cs` | **MODIFICAR** (poblar campos disponibles en widgets Chart) |
| Application | `Features/EcoDataNet/Services/LayoutCustomizationService.cs` | **MODIFICAR** (aplicar `CustomChartBinding` + `CustomTableColumns` con validación) |
| Application | `Features/EcoDataNet/Validators/SaveExplorerLayoutConfigCommandValidator.cs` | **MODIFICAR** (validar `ChartFieldBinding` + `TableColumnOverride`) |
| Web | `Components/Pages/EcoDataNet/DataExplorer/DynamicChart.razor` | **REVISAR** (verificar que usa `ChartCategoryField`/`ChartValueFields`) |
| Web | `Components/Pages/EcoDataNet/DataExplorer/DynamicDataTable.razor` | **MODIFICAR** (añadir `AllowColumnResize`, filtrar columnas ocultas, evento `OnColumnWidthChanged`) |
| Web | `Components/Pages/EcoDataNet/DataExplorer/WidgetConfigPanel.razor` | **MODIFICAR** (añadir dropdowns campos gráfico + checklist columnas tabla) |
| Web | `Components/Pages/EcoDataNet/DataExplorer/EdcDataExplorer.razor` | **MODIFICAR** (ampliar `SaveLayout()` con binding gráficos + columnas tabla, handler resize) |
| Tests | `Features/EcoDataNet/DataExplorer/ChartFieldBindingTests.cs` | **CREAR** |
| Tests | `Features/EcoDataNet/DataExplorer/TableColumnOverrideTests.cs` | **CREAR** |
| Tests | `Features/EcoDataNet/DataExplorer/DashboardLayoutBuilderTests.cs` | **MODIFICAR** (añadir test) |

---

## 🔄 ESTRATEGIA DE EJECUCIÓN

1. **Sesión 1**: CB-0 + CB-1 + CB-2 + CB-3 (contexto + DTOs + builder).
2. **Sesión 2**: CB-4 + CB-5 + CB-6 (merge service + verificar DynamicChart + refactorizar DynamicDataTable).
3. **Sesión 3**: CB-7 + CB-8 (UI selectores gráficos + UI selector columnas tabla).
4. **Sesión 4**: CB-9 + CB-10 + CB-11 (lógica de guardado + validación + tests).

> Al inicio de cada sesión, adjuntar este archivo + `COPILOT_CONTEXT.md` + `Mapa_Funcionalidades.md`.
> En la sesión 1, adjuntar también `DynamicWidgetDescriptor.cs`, `WidgetLayoutOverride.cs`, `DashboardLayoutBuilder.cs`.
> En la sesión 2, adjuntar `LayoutCustomizationService.cs`, `DynamicChart.razor` y `DynamicDataTable.razor`.
> En la sesión 3, adjuntar `WidgetConfigPanel.razor`.
> En la sesión 4, adjuntar `EdcDataExplorer.razor`, `SaveExplorerLayoutConfigCommand.cs` y `SaveExplorerLayoutConfigCommandValidator.cs`.

---

## ⚠️ NOTAS IMPORTANTES

1. **Retrocompatibilidad total**: si no hay `CustomChartBinding` ni `CustomTableColumns` en un override guardado (porque se guardó antes de esta mejora), el comportamiento es exactamente el mismo que antes — los campos automáticos y todas las columnas se mantienen. La deserialización de JSON con `JsonIgnoreCondition.WhenWritingNull` simplemente deja los campos a null.

2. **No se necesita migración de BD**: tanto `CustomChartBinding` como `CustomTableColumns` se serializan DENTRO del campo `LayoutConfigJson` existente (que es `nvarchar(max)`). No hay cambios en el esquema de la tabla `ExplorerLayoutConfigs`.

3. **Validación defensiva en el merge**: siempre validar que los campos/columnas referenciados existan en el widget actual. Si el proveedor EDC cambia el esquema del JSON (renombra campos, elimina columnas), los bindings/overrides obsoletos se ignoran silenciosamente y se usan los automáticos.

4. **Multi-select en Radzen**: `RadzenDropDown` con `Multiple="true"` devuelve `IEnumerable<string>`. Verificar que el binding bidireccional con `Widget.ChartValueFields` (que es `List<string>`) funciona correctamente, o usar un wrapper `IEnumerable<string>` intermedio y convertir a lista en el handler.

5. **Donut/Pie siempre mono-serie**: aunque Radzen permite múltiples series en Donut, en el contexto del Data Explorer un gráfico circular muestra la distribución de UNA métrica entre categorías. Por eso se fuerza un solo `ValueField`.

6. **AllowColumnResize de RadzenDataGrid**: esta es una feature nativa de Radzen Blazor Components. No requiere JavaScript interop ni librerías externas. Verificar que la versión instalada del paquete `Radzen.Blazor` soporta el evento `ColumnResized` (disponible desde v4.x). Si la versión instalada no soporta el evento (versiones antiguas), la alternativa es usar `AllowColumnResize="true"` solo para UX visual y capturar los anchos al momento del guardado recorriendo los Width de cada columna del DataGrid.

7. **Rendimiento**: los dropdowns y checklists se alimentan de listas pequeñas (típicamente < 20 propiedades por array). No hay preocupación de rendimiento.

8. **Modo oscuro/claro**: los nuevos dropdowns y checkboxes usan componentes Radzen que ya respetan las variables CSS del proyecto. No se necesitan estilos adicionales más allá del `.table-columns-selector`.

9. **Columnas de tabla: mínimo 1 visible**: la función "Ninguna" siempre deja la primera columna visible para evitar una tabla vacía. El usuario puede ocultar esa columna manualmente después, pero el botón rápido previene el caso accidental.

10. **Redimensionado de columnas fuera de modo edición**: el `AllowColumnResize="true"` funciona SIEMPRE (no solo en modo edición), porque redimensionar columnas es una operación de visualización, no de edición de layout. Sin embargo, los anchos personalizados solo se persisten cuando el usuario está en modo edición y pulsa "Guardar". Si redimensiona fuera de modo edición, los anchos se pierden al recargar la página.

11. **Futura mejora (NO implementar ahora)**: permitir crear gráficos sobre arrays que actualmente no tienen gráfico (ej: un array que solo generó DataTable porque no tenía CategoryProperty ni TemporalProperty). Requeriría cambiar el tipo de widget de DataTable a Chart, lo cual ya está parcialmente soportado por `CustomWidgetType` pero necesitaría generar los metadatos de campos disponibles también para widgets DataTable.
