using System.Text.Json.Serialization;

namespace GreenTransit.Application.Features.EcoDataNet.DTOs;

/// <summary>
/// Representa el payload que se envía a /management/v3/contractnegotiations
/// siguiendo el formato EDC 0.5+ con Dataspace Protocol (DSP).
/// </summary>
/// <remarks>
/// Campos deliberadamente excluidos (causan HTTP 400 en EDC moderno):
/// - assetId       → no requerido en esta llamada
/// - policy        → no debe incluirse inline; el provider la resuelve por offerId
/// - callbackAddresses vacío → omitir si no se usan callbacks
/// </remarks>
public sealed class EdcContractRequestDto
{
    [JsonPropertyName("@context")]
    public EdcContextDto Context { get; init; } = new();

    [JsonPropertyName("@type")]
    public string Type { get; init; } = "ContractRequest";

    /// <summary>
    /// Endpoint de protocolo DSP del conector provider.
    /// Debe contener <c>/protocol</c> y NO apuntar a <c>/management</c>.
    /// Ejemplo: https://provider.example.com/protocol
    /// </summary>
    [JsonPropertyName("counterPartyAddress")]
    public string CounterPartyAddress { get; init; } = string.Empty;

    [JsonPropertyName("protocol")]
    public string Protocol { get; init; } = "dataspace-protocol-http";

    /// <summary>
    /// @id exacto del nodo <c>odrl:hasPolicy</c> del catálogo del provider.
    /// </summary>
    [JsonPropertyName("offerId")]
    public string OfferId { get; init; } = string.Empty;

    /// <summary>
    /// Identificador del conector provider (participantId).
    /// </summary>
    [JsonPropertyName("connectorId")]
    public string ConnectorId { get; init; } = string.Empty;

    /// <summary>
    /// Valida que los campos obligatorios sean coherentes con el formato EDC DSP.
    /// Lanza <see cref="InvalidOperationException"/> si detecta un problema.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(CounterPartyAddress))
            throw new InvalidOperationException("EdcContractRequestDto: CounterPartyAddress es obligatorio.");

        if (!CounterPartyAddress.Contains("/protocol", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"EdcContractRequestDto: CounterPartyAddress debe apuntar al endpoint /protocol, no a: {CounterPartyAddress}");

        if (CounterPartyAddress.Contains("/management", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"EdcContractRequestDto: CounterPartyAddress no debe apuntar a /management: {CounterPartyAddress}");

        if (string.IsNullOrWhiteSpace(OfferId))
            throw new InvalidOperationException("EdcContractRequestDto: OfferId es obligatorio.");

        if (string.IsNullOrWhiteSpace(ConnectorId))
            throw new InvalidOperationException("EdcContractRequestDto: ConnectorId es obligatorio.");
    }
}

/// <summary>Bloque @context mínimo para las llamadas a EDC Management API.</summary>
public sealed class EdcContextDto
{
    [JsonPropertyName("@vocab")]
    public string Vocab { get; init; } = "https://w3id.org/edc/v0.0.1/ns/";
}
