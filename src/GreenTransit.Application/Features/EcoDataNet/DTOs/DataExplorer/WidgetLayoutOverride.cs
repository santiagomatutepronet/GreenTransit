using GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;

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

    /// <summary>
    /// Tipo de widget personalizado (null = usar automático).
    /// Permite convertir una tabla en gráfico o viceversa.
    /// </summary>
    public WidgetType? CustomWidgetType { get; set; }

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

    /// <summary>
    /// Configuración personalizada de los campos del mapa (lat, lon, título, tooltip).
    /// Solo aplica a widgets de tipo Map. Null = usar binding automático.
    /// Se serializa dentro de LayoutConfigJson junto con el resto de overrides.
    /// </summary>
    public MapFieldBinding? CustomMapBinding { get; set; }

    /// <summary>
    /// Indica si se deben mostrar las etiquetas de datos en el gráfico.
    /// Null = usar el valor por defecto (true). Solo aplica a widgets de tipo Chart.
    /// </summary>
    public bool? CustomShowDataLabels { get; set; }
}
