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
