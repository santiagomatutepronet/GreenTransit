using GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;

namespace GreenTransit.Application.Features.EcoDataNet.Services;

/// <summary>
/// Construye el layout de widgets dinámicos a partir de un esquema JSON analizado.
/// </summary>
public interface IDashboardLayoutBuilder
{
    /// <summary>
    /// Genera la lista de widgets a renderizar.
    /// </summary>
    List<DynamicWidgetDescriptor> Build(JsonDataSchema schema);
}
