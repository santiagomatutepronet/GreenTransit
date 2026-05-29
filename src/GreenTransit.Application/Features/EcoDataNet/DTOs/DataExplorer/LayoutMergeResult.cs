namespace GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;

/// <summary>
/// Resultado del merge entre el layout automático y la configuración guardada.
/// </summary>
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
