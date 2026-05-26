using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Common.Options;
using GreenTransit.Application.Features.EcoDataNet.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GreenTransit.Infrastructure.Services;

/// <summary>
/// Implementación de <see cref="IEdcManagementClient"/> que realiza llamadas HTTP
/// a la Management API de un conector EDC.
/// </summary>
public sealed class EdcManagementClient : IEdcManagementClient
{
    private readonly HttpClient                    _httpClient;
    private readonly IOptions<EdcOptions>          _options;
    private readonly ILogger<EdcManagementClient>  _logger;

    public EdcManagementClient(
        HttpClient                   httpClient,
        IOptions<EdcOptions>         options,
        ILogger<EdcManagementClient> logger)
    {
        _httpClient = httpClient;
        _options    = options;
        _logger     = logger;
    }

    public async Task<EdcCatalogResult> RequestCatalogAsync(
        string managementBaseUrl,
        string counterPartyProtocolUrl,
        string? apiKey = null,
        CancellationToken cancellationToken = default)
    {
        var requestUrl = $"{managementBaseUrl.TrimEnd('/')}/v3/catalog/request";

        // Construir body JSON-LD con claves "@" usando Dictionary para preservarlas
        var requestBody = new Dictionary<string, object>
        {
            ["@context"] = new Dictionary<string, string>
            {
                ["@vocab"] = "https://w3id.org/edc/v0.0.1/ns/"
            },
            ["@type"]               = "CatalogRequest",
            ["counterPartyAddress"] = counterPartyProtocolUrl,
            ["protocol"]            = "dataspace-protocol-http"
        };

        var jsonContent = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
        {
            WriteIndented = false
        });

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUrl);
        httpRequest.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        // API Key del conector — requerida por la Management API del servidor EDC
        var effectiveApiKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : _options.Value.ManagementApiKey;
        if (!string.IsNullOrWhiteSpace(effectiveApiKey))
            httpRequest.Headers.Add("X-Api-Key", effectiveApiKey);

        _logger.LogInformation(
            "Solicitando catálogo EDC: POST {Url} | counterParty: {CounterParty}",
            requestUrl, counterPartyProtocolUrl);

        try
        {
            var response     = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var truncated = responseBody.Length > 500
                    ? responseBody[..500]
                    : responseBody;

                _logger.LogWarning(
                    "Respuesta no exitosa de EDC Management API: {StatusCode} | URL: {Url} | Body: {Body}",
                    (int)response.StatusCode, requestUrl, truncated);

                return new EdcCatalogResult
                {
                    Success        = false,
                    HttpStatusCode = (int)response.StatusCode,
                    ErrorMessage   = $"HTTP {(int)response.StatusCode}: {(responseBody.Length > 200 ? responseBody[..200] : responseBody)}",
                    RawJson        = responseBody
                };
            }

            var datasetCount = CountDatasetsFromJson(responseBody);

            _logger.LogInformation(
                "Catálogo EDC recibido con éxito: {DatasetCount} datasets | URL: {Url}",
                datasetCount, requestUrl);

            return new EdcCatalogResult
            {
                Success        = true,
                HttpStatusCode = (int)response.StatusCode,
                RawJson        = responseBody,
                DatasetCount   = datasetCount
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error de conexión con EDC Management API: {Url}", requestUrl);
            return new EdcCatalogResult
            {
                Success      = false,
                ErrorMessage = $"Error de conexión: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Cuenta datasets del JSON de respuesta del catálogo de forma heurística.
    /// </summary>
    private int CountDatasetsFromJson(string json)
    {
        try
        {
            using var doc  = JsonDocument.Parse(json);
            var       root = doc.RootElement;

            // Intentar "dcat:dataset" como array
            if (root.TryGetProperty("dcat:dataset", out var datasetProp))
            {
                return datasetProp.ValueKind == JsonValueKind.Array
                    ? datasetProp.GetArrayLength()
                    : 1;
            }

            // Intentar forma expandida
            if (root.TryGetProperty("https://www.w3.org/ns/dcat#dataset", out var datasetProp2))
            {
                return datasetProp2.ValueKind == JsonValueKind.Array
                    ? datasetProp2.GetArrayLength()
                    : 1;
            }

            // Intentar "@graph" (JSON-LD expandido)
            if (root.TryGetProperty("@graph", out var graphProp)
                && graphProp.ValueKind == JsonValueKind.Array)
            {
                return graphProp.EnumerateArray()
                    .Count(e => e.TryGetProperty("@type", out var t)
                             && t.GetString()?.Contains("Dataset", StringComparison.OrdinalIgnoreCase) == true);
            }

            _logger.LogDebug("No se pudo determinar el número de datasets del catálogo EDC");
            return 0;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Error parseando JSON del catálogo EDC para contar datasets");
            return 0;
        }
    }
}
