using MediatR;

namespace GreenTransit.Application.Features.EcoDataNet.Commands;

/// <summary>
/// Elimina la configuración de layout personalizado para un asset EDC (reset a automático).
/// </summary>
public class DeleteExplorerLayoutConfigCommand : IRequest<bool>
{
    public string AssetId { get; set; } = string.Empty;
    public string ProviderParticipantId { get; set; } = string.Empty;
}
