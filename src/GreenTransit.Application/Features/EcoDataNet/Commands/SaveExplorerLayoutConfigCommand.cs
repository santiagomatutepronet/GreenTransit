using GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;
using MediatR;

namespace GreenTransit.Application.Features.EcoDataNet.Commands;

/// <summary>
/// Crea o actualiza la configuración de layout personalizado para un asset EDC.
/// </summary>
public class SaveExplorerLayoutConfigCommand : IRequest<int>
{
    public string AssetId { get; set; } = string.Empty;
    public string ProviderParticipantId { get; set; } = string.Empty;
    public string? DatasetName { get; set; }
    public List<WidgetLayoutOverride> Overrides { get; set; } = new();
    public List<CustomWidgetDefinition> CustomWidgets { get; set; } = new();
    public string? SchemaHash { get; set; }
}
