namespace GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;

/// <summary>
/// Modelo de persistencia que agrupa toda la configuración guardada en LayoutConfigJson.
/// Se serializa como un único objeto JSON para mantener retrocompatibilidad:
/// - formato antiguo: el JSON raíz era directamente un array de WidgetLayoutOverride.
/// - formato nuevo: el JSON raíz es este objeto, con "overrides" y "customWidgets".
/// </summary>
public class PersistedLayoutConfig
{
    /// <summary>Overrides aplicados sobre los widgets autogenerados.</summary>
    public List<WidgetLayoutOverride> Overrides { get; set; } = new();

    /// <summary>Widgets creados manualmente por el usuario.</summary>
    public List<CustomWidgetDefinition> CustomWidgets { get; set; } = new();
}
