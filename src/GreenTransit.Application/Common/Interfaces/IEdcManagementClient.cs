using GreenTransit.Application.Features.EcoDataNet.DTOs;

namespace GreenTransit.Application.Common.Interfaces;

/// <summary>
/// Encapsula las llamadas HTTP a la Management API de un conector EDC.
/// Implementado en Infrastructure.
/// </summary>
public interface IEdcManagementClient
{
    /// <summary>
    /// Solicita el catálogo de un proveedor EDC a través de su Management API.
    /// </summary>
    /// <param name="managementBaseUrl">URL base de la Management API (ej: https://mgmt.server/management)</param>
    /// <param name="counterPartyProtocolUrl">URL de la Protocol API como counterPartyAddress (ej: https://proto.server/protocol)</param>
    /// <param name="apiKey">API Key del conector para el header X-Api-Key. Null o vacío = no enviar el header.</param>
    /// <param name="cancellationToken">Token de cancelación</param>
    Task<EdcCatalogResult> RequestCatalogAsync(
        string managementBaseUrl,
        string counterPartyProtocolUrl,
        string? apiKey = null,
        CancellationToken cancellationToken = default);
}
