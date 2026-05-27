using GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;

namespace GreenTransit.Application.Features.EcoDataNet.Services;

/// <summary>
/// Combina el layout generado automáticamente con la configuración de overrides guardada.
/// </summary>
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
