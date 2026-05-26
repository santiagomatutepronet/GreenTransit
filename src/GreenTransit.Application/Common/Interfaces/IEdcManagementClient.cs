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
    Task<EdcCatalogResult> RequestCatalogAsync(
        string managementBaseUrl,
        string counterPartyProtocolUrl,
        string? apiKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inicia una negociación de contrato con un proveedor EDC.
    /// POST /v3/contractnegotiations contra la Management API del consumidor.
    /// </summary>
    Task<EdcNegotiationResponse> StartNegotiationAsync(
        string consumerManagementBaseUrl,
        string contractRequestPayload,
        string? apiKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Consulta el estado de una negociación de contrato.
    /// GET /v3/contractnegotiations/{negotiationId} contra Management API del consumidor.
    /// </summary>
    Task<EdcNegotiationStateResponse> GetNegotiationStateAsync(
        string consumerManagementBaseUrl,
        string negotiationId,
        string? apiKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inicia un proceso de transferencia de datos.
    /// POST /v3/transferprocesses contra Management API del consumidor.
    /// </summary>
    Task<EdcTransferResponse> StartTransferAsync(
        string consumerManagementBaseUrl,
        string transferRequestPayload,
        string? apiKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Consulta el estado de un proceso de transferencia.
    /// GET /v3/transferprocesses/{transferId} contra Management API del consumidor.
    /// </summary>
    Task<EdcTransferStateResponse> GetTransferStateAsync(
        string consumerManagementBaseUrl,
        string transferId,
        string? apiKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene la referencia de datos del endpoint (EDR) para descargar datos.
    /// GET /v3/edrs/{transferProcessId}/dataaddress contra Management API del consumidor.
    /// Alternativa: GET /v3/transferprocesses/{id}/dataaddress (versiones EDC anteriores a 0.7.x).
    /// </summary>
    Task<EdcEndpointDataReferenceResponse> GetEndpointDataReferenceAsync(
        string consumerManagementBaseUrl,
        string transferProcessId,
        string? apiKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Descarga datos desde el data plane del proveedor usando el endpoint y token del EDR.
    /// GET al endpoint indicado con header Authorization: {authType} {authCode}.
    /// </summary>
    Task<EdcDataDownloadResponse> DownloadDataAsync(
        string dataPlaneEndpoint,
        string authType,
        string authCode,
        CancellationToken cancellationToken = default);
}
