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
