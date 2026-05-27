using GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;
using MediatR;

namespace GreenTransit.Application.Features.EcoDataNet.Queries;

/// <summary>
/// Carga la configuración de layout guardada para un asset EDC del usuario actual.
/// Devuelve null si no existe configuración guardada.
/// </summary>
public class GetExplorerLayoutConfigQuery : IRequest<LayoutConfigDto?>
{
    public string AssetId { get; set; } = string.Empty;
    public string ProviderParticipantId { get; set; } = string.Empty;
}
