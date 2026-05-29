using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GreenTransit.Application.Common.Models;
using Microsoft.Extensions.Logging;

namespace GreenTransit.Infrastructure.ExternalApis.EcoDataNet;

/// <summary>
/// Cliente HTTP tipado para la API EcoDataNet Waste.
/// Gestiona envío en batch, 200 OK y 207 Multi-Status.
/// </summary>
public class EcoDataNetHttpClient
{
    private readonly HttpClient                    _httpClient;
    private readonly ILogger<EcoDataNetHttpClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented               = false
    };

    public EcoDataNetHttpClient(HttpClient httpClient, ILogger<EcoDataNetHttpClient> logger)
    {
        _httpClient = httpClient;
        _logger     = logger;
    }

    /// <summary>
    /// Envía un batch de items a un endpoint POST de EcoDataNet.
    /// Gestiona 200 OK y 207 Multi-Status.
    /// </summary>
    public async Task<EndpointResult> PostBatchAsync<T>(
        string endpoint, ICollection<T> items, CancellationToken ct)
    {
        var result = new EndpointResult { Endpoint = endpoint, TotalSent = items.Count };

        if (items.Count == 0)
        {
            _logger.LogDebug("EcoDataNet [{Endpoint}]: sin elementos, omitiendo.", endpoint);
            return result;
        }

        var json    = JsonSerializer.Serialize(items, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsync(endpoint, content, ct);
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Error de conexión: {ex.Message}";
            _logger.LogError(ex, "EcoDataNet [{Endpoint}]: error de red.", endpoint);
            return result;
        }

        if (response.StatusCode == HttpStatusCode.OK)
        {
            result.SuccessCount = items.Count;
        }
        else if ((int)response.StatusCode == 207)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            result.ParseMultiStatus(body);
            if (result.ErrorCount > 0 && result.ErrorMessage is not null)
                _logger.LogWarning(
                    "EcoDataNet [{Endpoint}]: {Errors} errores en 207 — primer error: {Msg}",
                    endpoint, result.ErrorCount, result.ErrorMessage);
        }
        else
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            result.ErrorMessage = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";
            result.ErrorDetail  = body.Length > 500 ? body[..500] : body;
            _logger.LogError(
                "EcoDataNet [{Endpoint}]: {Status} — {Detail}",
                endpoint, (int)response.StatusCode, result.ErrorDetail);
        }

        return result;
    }
}
