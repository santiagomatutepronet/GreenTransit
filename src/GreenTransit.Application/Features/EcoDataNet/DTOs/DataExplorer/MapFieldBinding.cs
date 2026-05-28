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
