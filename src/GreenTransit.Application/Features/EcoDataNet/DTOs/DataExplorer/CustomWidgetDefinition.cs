namespace GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;

/// <summary>
/// Definición completa de un widget creado por el usuario (no autogenerado).
/// Se serializa dentro de LayoutConfigJson como parte de la lista CustomWidgets.
/// A diferencia de WidgetLayoutOverride (que solo tiene "deltas" respecto al automático),
/// esta clase contiene toda la información necesaria para reconstituir el widget.
/// </summary>
public class CustomWidgetDefinition
{
    /// <summary>
    /// Identificador único del widget creado por el usuario.
    /// Se genera con prefijo "usr_" + 8 caracteres del GUID al crear.
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

    /// <summary>
    /// Nombre del array fuente del JSON que alimenta este gráfico.
    /// Corresponde a JsonArrayDescriptor.Name.
    /// </summary>
    public string? SourceArrayName { get; set; }

    /// <summary>Tipo de gráfico.</summary>
    public ChartSubType? ChartType { get; set; }

    /// <summary>Campo para el eje X / categorías.</summary>
    public string? ChartCategoryField { get; set; }

    /// <summary>Campos para el eje Y / series.</summary>
    public List<string>? ChartValueFields { get; set; }

    // --- Campos para KPI calculado ---

    /// <summary>Definición del KPI calculado (null si es Chart/Map).</summary>
    public CustomKpiDefinition? KpiDefinition { get; set; }

    // --- Campos para Mapa creado por usuario ---

    /// <summary>Campo de latitud del array fuente (null si es Chart/KpiCard).</summary>
    public string? MapLatitudeField { get; set; }

    /// <summary>Campo de longitud del array fuente (null si es Chart/KpiCard).</summary>
    public string? MapLongitudeField { get; set; }

    /// <summary>Campo título del punto en el mapa (null = primer campo string disponible).</summary>
    public string? MapTitleField { get; set; }
}
