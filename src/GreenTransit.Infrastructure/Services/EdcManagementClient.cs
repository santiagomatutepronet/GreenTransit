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
    private readonly IHttpClientFactory            _httpClientFactory;
    private readonly IOptions<EdcOptions>          _options;
    private readonly ILogger<EdcManagementClient>  _logger;

    public EdcManagementClient(
        HttpClient                   httpClient,
        IHttpClientFactory           httpClientFactory,
        IOptions<EdcOptions>         options,
        ILogger<EdcManagementClient> logger)
    {
        _httpClient        = httpClient;
        _httpClientFactory = httpClientFactory;
        _options           = options;
        _logger            = logger;
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

    // ── Métodos de negociación ────────────────────────────────────────────────

    public async Task<EdcNegotiationResponse> StartNegotiationAsync(
        string consumerManagementBaseUrl,
        string contractRequestPayload,
        string? apiKey = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"{consumerManagementBaseUrl.TrimEnd('/')}/v3/contractnegotiations";
        _logger.LogInformation("POST {Url} — Iniciando negociación de contrato", url);

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
            httpRequest.Headers.Accept.ParseAdd("application/json");
            httpRequest.Content = new StringContent(contractRequestPayload, Encoding.UTF8, "application/json");
            AddApiKeyHeader(httpRequest, apiKey);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var body     = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var truncatedBody    = body.Length > 2000 ? body[..2000] : body;
                var truncatedPayload = contractRequestPayload.Length > 4000 ? contractRequestPayload[..4000] : contractRequestPayload;

                _logger.LogError(
                    "Negociación fallida: HTTP {StatusCode} | URL: {Url} | ResponseBody: {Body} | PayloadEnviado: {Payload}",
                    (int)response.StatusCode, url, truncatedBody, truncatedPayload);

                return new EdcNegotiationResponse
                {
                    Success        = false,
                    HttpStatusCode = (int)response.StatusCode,
                    ErrorMessage   = $"HTTP {(int)response.StatusCode}: {body} | Payload enviado: {truncatedPayload}"
                };
            }

            using var doc  = JsonDocument.Parse(body);
            var       root = doc.RootElement;
            var       negotiationId = GetJsonLdString(root, "@id");

            _logger.LogInformation(
                "Negociación iniciada correctamente — NegotiationId: {NegotiationId} | State: {State} | ResponseBody: {Body}",
                negotiationId,
                GetJsonLdString(root, "state", "https://w3id.org/edc/v0.0.1/ns/state"),
                body.Length > 1000 ? body[..1000] : body);

            return new EdcNegotiationResponse
            {
                Success        = true,
                NegotiationId  = negotiationId,
                State          = GetJsonLdString(root, "state", "https://w3id.org/edc/v0.0.1/ns/state"),
                HttpStatusCode = (int)response.StatusCode
            };
        }
        catch (TaskCanceledException)
        {
            return new EdcNegotiationResponse { Success = false, ErrorMessage = "Timeout" };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error HTTP al iniciar negociación en {Url}", url);
            return new EdcNegotiationResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<EdcNegotiationStateResponse> GetNegotiationStateAsync(
        string consumerManagementBaseUrl,
        string negotiationId,
        string? apiKey = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"{consumerManagementBaseUrl.TrimEnd('/')}/v3/contractnegotiations/{negotiationId}";

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
            AddApiKeyHeader(httpRequest, apiKey);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var body     = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new EdcNegotiationStateResponse { Success = false, HttpStatusCode = (int)response.StatusCode, ErrorMessage = $"HTTP {(int)response.StatusCode}: {(body.Length > 200 ? body[..200] : body)}" };
            }

            using var doc  = JsonDocument.Parse(body);
            var       root = doc.RootElement;

            var state = GetJsonLdString(root, "state", "https://w3id.org/edc/v0.0.1/ns/state");

            // Intentar leer errorDetail con todos los aliases JSON-LD posibles
            var errorDetail = GetJsonLdString(root,
                "errorDetail",
                "edc:errorDetail",
                "https://w3id.org/edc/v0.0.1/ns/errorDetail",
                "https://w3id.org/edc/v0.0.1/ns/state/errorDetail");

            _logger.LogInformation(
                "Estado negociación {Id}: {State} | errorDetail={Err} | bodyCompleto={Body}",
                negotiationId, state,
                string.IsNullOrEmpty(errorDetail) ? "(vacío)" : errorDetail,
                body.Length > 4000 ? body[..4000] : body);

            return new EdcNegotiationStateResponse
            {
                Success             = true,
                NegotiationId       = negotiationId,
                State               = state,
                ContractAgreementId = GetJsonLdString(root, "contractAgreementId",
                                          "https://w3id.org/edc/v0.0.1/ns/contractAgreementId",
                                          "edc:contractAgreementId"),
                ErrorDetail         = errorDetail,
                HttpStatusCode      = (int)response.StatusCode,
                RawJson             = body
            };
        }
        catch (TaskCanceledException)
        {
            return new EdcNegotiationStateResponse { Success = false, ErrorMessage = "Timeout" };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error HTTP al consultar estado de negociación {Id}", negotiationId);
            return new EdcNegotiationStateResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    // ── Métodos de transferencia ──────────────────────────────────────────────

    public async Task<EdcTransferResponse> StartTransferAsync(
        string consumerManagementBaseUrl,
        string transferRequestPayload,
        string? apiKey = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"{consumerManagementBaseUrl.TrimEnd('/')}/v3/transferprocesses";
        _logger.LogInformation("POST {Url} — Iniciando proceso de transferencia", url);

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
            httpRequest.Content = new StringContent(transferRequestPayload, Encoding.UTF8, "application/json");
            AddApiKeyHeader(httpRequest, apiKey);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var body     = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Transferencia fallida: {StatusCode} — {Body}", (int)response.StatusCode, body.Length > 500 ? body[..500] : body);
                return new EdcTransferResponse { Success = false, HttpStatusCode = (int)response.StatusCode, ErrorMessage = $"HTTP {(int)response.StatusCode}: {(body.Length > 200 ? body[..200] : body)}" };
            }

            using var doc  = JsonDocument.Parse(body);
            var       root = doc.RootElement;

            return new EdcTransferResponse
            {
                Success           = true,
                TransferProcessId = GetJsonLdString(root, "@id"),
                State             = GetJsonLdString(root, "state", "https://w3id.org/edc/v0.0.1/ns/state"),
                HttpStatusCode    = (int)response.StatusCode
            };
        }
        catch (TaskCanceledException)
        {
            return new EdcTransferResponse { Success = false, ErrorMessage = "Timeout" };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error HTTP al iniciar transferencia en {Url}", url);
            return new EdcTransferResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<EdcTransferStateResponse> GetTransferStateAsync(
        string consumerManagementBaseUrl,
        string transferId,
        string? apiKey = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"{consumerManagementBaseUrl.TrimEnd('/')}/v3/transferprocesses/{transferId}";

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
            AddApiKeyHeader(httpRequest, apiKey);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var body     = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new EdcTransferStateResponse { Success = false, HttpStatusCode = (int)response.StatusCode, ErrorMessage = $"HTTP {(int)response.StatusCode}: {(body.Length > 200 ? body[..200] : body)}" };
            }

            using var doc  = JsonDocument.Parse(body);
            var       root = doc.RootElement;

            var state = GetJsonLdString(root, "state", "https://w3id.org/edc/v0.0.1/ns/state");

            _logger.LogInformation("Estado transferencia {Id}: {State}", transferId, state);

            return new EdcTransferStateResponse
            {
                Success           = true,
                TransferProcessId = transferId,
                State             = state,
                ErrorDetail       = GetJsonLdString(root, "errorDetail",
                                        "https://w3id.org/edc/v0.0.1/ns/errorDetail"),
                HttpStatusCode    = (int)response.StatusCode,
                RawJson           = body
            };
        }
        catch (TaskCanceledException)
        {
            return new EdcTransferStateResponse { Success = false, ErrorMessage = "Timeout" };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error HTTP al consultar estado de transferencia {Id}", transferId);
            return new EdcTransferStateResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    // ── EDR y descarga ────────────────────────────────────────────────────────

    public async Task<EdcEndpointDataReferenceResponse> GetEndpointDataReferenceAsync(
        string consumerManagementBaseUrl,
        string transferProcessId,
        string? apiKey = null,
        CancellationToken cancellationToken = default)
    {
        // Endpoint primario EDC >= 0.7.x. Alternativa: /v3/transferprocesses/{id}/dataaddress
        var url = $"{consumerManagementBaseUrl.TrimEnd('/')}/v3/edrs/{transferProcessId}/dataaddress";
        _logger.LogInformation("GET {Url} — Obteniendo EDR", url);

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
            AddApiKeyHeader(httpRequest, apiKey);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var body     = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new EdcEndpointDataReferenceResponse { Success = false, HttpStatusCode = (int)response.StatusCode, ErrorMessage = $"HTTP {(int)response.StatusCode}: {(body.Length > 200 ? body[..200] : body)}", RawJson = body };
            }

            using var doc  = JsonDocument.Parse(body);
            var       root = doc.RootElement;

            var endpoint      = GetJsonLdString(root, "endpoint", "https://w3id.org/edc/v0.0.1/ns/endpoint");
            var authorization = GetJsonLdString(root, "authorization", "https://w3id.org/edc/v0.0.1/ns/authorization");
            var authType      = GetJsonLdString(root, "authType",  "https://w3id.org/edc/v0.0.1/ns/authType");

            // Separar "Bearer eyJ..." en tipo y código si vienen juntos en authorization
            string resolvedType = "Bearer";
            string resolvedCode = authorization;
            if (!string.IsNullOrEmpty(authorization) && authorization.Contains(' '))
            {
                var parts = authorization.Split(' ', 2);
                resolvedType = parts[0];
                resolvedCode = parts[1];
            }
            else if (!string.IsNullOrEmpty(authType))
            {
                resolvedType = authType;
            }

            // Normalizar a Pascal case: "bearer" → "Bearer", "BEARER" → "Bearer"
            if (!string.IsNullOrEmpty(resolvedType))
                resolvedType = char.ToUpperInvariant(resolvedType[0]) + resolvedType[1..].ToLowerInvariant();

            _logger.LogInformation("EDR obtenido para transferencia {Id}: endpoint={Endpoint}", transferProcessId, endpoint);

            return new EdcEndpointDataReferenceResponse
            {
                Success        = true,
                Endpoint       = endpoint,
                AuthType       = resolvedType,
                AuthCode       = resolvedCode,
                HttpStatusCode = (int)response.StatusCode,
                RawJson        = body
            };
        }
        catch (TaskCanceledException)
        {
            return new EdcEndpointDataReferenceResponse { Success = false, ErrorMessage = "Timeout" };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error HTTP al obtener EDR para transferencia {Id}", transferProcessId);
            return new EdcEndpointDataReferenceResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<EdcDataDownloadResponse> DownloadDataAsync(
        string dataPlaneEndpoint,
        string authType,
        string authCode,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("GET {Endpoint} — Descargando datos del data plane | AuthType={AuthType} | TokenHead={TokenHead}",
            dataPlaneEndpoint, authType,
            authCode?.Length > 20 ? authCode[..20] + "…" : authCode);

        try
        {
            // Crear un cliente limpio sin DefaultRequestHeaders (sin Accept: application/json)
            // para no interferir con el backend al que el data plane hace proxy.
            using var dataPlaneClient = _httpClientFactory.CreateClient();
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, dataPlaneEndpoint);

            // El data plane EDC valida el token raw sin prefijo "Bearer ".
            httpRequest.Headers.TryAddWithoutValidation("Authorization", authCode);

            var response    = await dataPlaneClient.SendAsync(httpRequest, cancellationToken);
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";

            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Error descargando datos del data plane {Endpoint}: HTTP {Status} — {Body}",
                    dataPlaneEndpoint, (int)response.StatusCode, errBody);
                return new EdcDataDownloadResponse { Success = false, HttpStatusCode = (int)response.StatusCode, ErrorMessage = $"HTTP {(int)response.StatusCode}: {errBody}" };
            }

            // Leer como texto si es JSON/CSV/texto; como bytes si es binario
            if (contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
                || contentType.Contains("json", StringComparison.OrdinalIgnoreCase)
                || contentType.Contains("xml",  StringComparison.OrdinalIgnoreCase))
            {
                var data = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogInformation("Descarga completada: {Bytes} bytes, ContentType={ContentType}", data.Length, contentType);
                return new EdcDataDownloadResponse { Success = true, ContentType = contentType, Data = data, HttpStatusCode = (int)response.StatusCode };
            }

            var rawData = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            _logger.LogInformation("Descarga completada (binario): {Bytes} bytes, ContentType={ContentType}", rawData.Length, contentType);
            return new EdcDataDownloadResponse { Success = true, ContentType = contentType, RawData = rawData, HttpStatusCode = (int)response.StatusCode };
        }
        catch (TaskCanceledException)
        {
            return new EdcDataDownloadResponse { Success = false, ErrorMessage = "Timeout" };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error HTTP al descargar datos desde {Endpoint}", dataPlaneEndpoint);
            return new EdcDataDownloadResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Añade la API Key como header X-Api-Key. Prioriza la key del conector; si no, usa la global de EdcOptions.</summary>
    private void AddApiKeyHeader(HttpRequestMessage request, string? apiKey = null)
    {
        var key = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : _options.Value.ManagementApiKey;
        if (!string.IsNullOrWhiteSpace(key))
            request.Headers.Add("X-Api-Key", key);
    }

    /// <summary>
    /// Busca una propiedad en un JsonElement por cualquiera de los nombres dados (con y sin prefijo namespace).
    /// </summary>
    private static string GetJsonLdString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.String)
                    return prop.GetString() ?? string.Empty;
                if (prop.ValueKind == JsonValueKind.Object && prop.TryGetProperty("@value", out var val))
                    return val.GetString() ?? string.Empty;
            }
        }
        return string.Empty;
    }
}
