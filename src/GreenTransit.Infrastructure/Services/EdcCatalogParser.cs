using System.Text.Json;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.EcoDataNet.DTOs;
using Microsoft.Extensions.Logging;

namespace GreenTransit.Infrastructure.Services;

/// <summary>
/// Parsea el JSON bruto de un catálogo EDC (DCAT/ODRL) a DTOs tipados.
/// Soporta prefijos compactos (dcat:dataset) e IRIs completas.
/// </summary>
public class EdcCatalogParser : IEdcCatalogParser
{
    private readonly ILogger<EdcCatalogParser> _logger;

    public EdcCatalogParser(ILogger<EdcCatalogParser> logger)
    {
        _logger = logger;
    }

    public EdcCatalogDto? ParseCatalog(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return null;

        try
        {
            using var doc  = JsonDocument.Parse(rawJson);
            var root       = doc.RootElement;

            var catalog = new EdcCatalogDto
            {
                CatalogId     = GetStringProperty(root, "@id", "id"),
                ParticipantId = GetStringProperty(root,
                    "dspace:participantId",
                    "https://w3id.org/dspace/v0.8/participantId",
                    "participantId")
            };

            // Parsear servicios para resolver endpointUrl de distribuciones
            var services = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var servicesElement = GetProperty(root, "dcat:service",
                "https://www.w3.org/ns/dcat#service");
            if (servicesElement.HasValue)
            {
                foreach (var svc in NormalizeToArray(servicesElement.Value))
                {
                    var svcId      = GetStringProperty(svc, "@id", "id");
                    var endpointUrl = GetStringProperty(svc,
                        "dcat:endpointUrl",
                        "https://www.w3.org/ns/dcat#endpointUrl");
                    if (!string.IsNullOrEmpty(svcId) && !string.IsNullOrEmpty(endpointUrl))
                        services[svcId] = endpointUrl;
                }
            }

            // Parsear datasets
            var datasetsElement = GetProperty(root,
                "dcat:dataset",
                "https://www.w3.org/ns/dcat#dataset");

            if (datasetsElement.HasValue)
            {
                catalog.Datasets = NormalizeToArray(datasetsElement.Value)
                    .Select(ParseDataset)
                    .Where(d => d != null)
                    .Select(d => d!)
                    .ToList();
            }

            // Resolver endpointUrl en distribuciones
            foreach (var dataset in catalog.Datasets)
            {
                foreach (var dist in dataset.Distributions)
                {
                    if (!string.IsNullOrEmpty(dist.AccessServiceId)
                        && services.TryGetValue(dist.AccessServiceId, out var url))
                    {
                        dist.EndpointUrl = url;
                    }
                }
            }

            _logger.LogInformation(
                "Catálogo EDC parseado: {DatasetCount} datasets, participantId: {ParticipantId}",
                catalog.Datasets.Count, catalog.ParticipantId);

            return catalog;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parseando JSON del catálogo EDC DCAT");
            return null;
        }
    }

    // ── Parsers privados ──────────────────────────────────────────────────

    private EdcDatasetDto? ParseDataset(JsonElement element)
    {
        try
        {
            var dataset = new EdcDatasetDto
            {
                DatasetId   = GetStringProperty(element, "@id", "id"),
                Name        = GetStringProperty(element, "name",
                    "https://w3id.org/edc/v0.0.1/ns/name"),
                Version     = NullIfEmpty(GetStringProperty(element, "version",
                    "https://w3id.org/edc/v0.0.1/ns/version")),
                ContentType = NullIfEmpty(GetStringProperty(element, "contenttype",
                    "https://w3id.org/edc/v0.0.1/ns/contenttype")),
                Description = NullIfEmpty(GetStringProperty(element, "description",
                    "https://w3id.org/edc/v0.0.1/ns/description"))
            };

            // Oferta(s) ODRL — usar la primera si hay varias
            var policyElement = GetProperty(element, "odrl:hasPolicy",
                "hasPolicy",
                "http://www.w3.org/ns/odrl/2/hasPolicy");
            if (policyElement.HasValue)
            {
                var policies = NormalizeToArray(policyElement.Value);
                if (policies.Count > 0)
                    dataset.Offer = ParseOffer(policies[0]);
            }

            // Distribuciones
            var distElement = GetProperty(element, "dcat:distribution",
                "https://www.w3.org/ns/dcat#distribution");
            if (distElement.HasValue)
            {
                dataset.Distributions = NormalizeToArray(distElement.Value)
                    .Select(ParseDistribution)
                    .Where(d => d != null)
                    .Select(d => d!)
                    .ToList();
            }

            return dataset;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parseando dataset EDC individual");
            return null;
        }
    }

    private EdcOfferDto ParseOffer(JsonElement element)
    {
        var offer = new EdcOfferDto
        {
            OfferId      = GetStringProperty(element, "@id", "id"),
            RawOfferJson = element.GetRawText()
        };

        var permElement = GetProperty(element, "odrl:permission",
            "permission",
            "http://www.w3.org/ns/odrl/2/permission");
        if (permElement.HasValue)
            offer.Permissions = NormalizeToArray(permElement.Value)
                .Select(ParsePermission)
                .ToList();

        var prohibElement = GetProperty(element, "odrl:prohibition",
            "prohibition",
            "http://www.w3.org/ns/odrl/2/prohibition");
        if (prohibElement.HasValue)
            offer.Prohibitions = NormalizeToArray(prohibElement.Value)
                .Select(e => ParseActionAndConstraints(e, (a, c) => new EdcProhibitionDto { Action = a, Constraints = c }))
                .ToList();

        var obligElement = GetProperty(element, "odrl:obligation",
            "obligation",
            "http://www.w3.org/ns/odrl/2/obligation");
        if (obligElement.HasValue)
            offer.Obligations = NormalizeToArray(obligElement.Value)
                .Select(e => ParseActionAndConstraints(e, (a, c) => new EdcObligationDto { Action = a, Constraints = c }))
                .ToList();

        return offer;
    }

    private EdcPermissionDto ParsePermission(JsonElement element)
    {
        var perm = new EdcPermissionDto();

        var actionElement = GetProperty(element, "odrl:action",
            "action",
            "http://www.w3.org/ns/odrl/2/action");
        if (actionElement.HasValue)
        {
            perm.Action = actionElement.Value.ValueKind == JsonValueKind.Object
                ? GetStringProperty(actionElement.Value, "@id", "id")
                : actionElement.Value.GetString() ?? string.Empty;
        }

        var constraintElement = GetProperty(element, "odrl:constraint",
            "constraint",
            "http://www.w3.org/ns/odrl/2/constraint");
        if (constraintElement.HasValue)
            perm.Constraints = NormalizeToArray(constraintElement.Value)
                .Select(ParseConstraint)
                .ToList();

        return perm;
    }

    private T ParseActionAndConstraints<T>(JsonElement element, Func<string, List<EdcConstraintDto>, T> factory)
    {
        var action = string.Empty;
        var actionElement = GetProperty(element, "odrl:action",
            "action",
            "http://www.w3.org/ns/odrl/2/action");
        if (actionElement.HasValue)
        {
            action = actionElement.Value.ValueKind == JsonValueKind.Object
                ? GetStringProperty(actionElement.Value, "@id", "id")
                : actionElement.Value.GetString() ?? string.Empty;
        }

        var constraints = new List<EdcConstraintDto>();
        var constraintElement = GetProperty(element, "odrl:constraint",
            "constraint",
            "http://www.w3.org/ns/odrl/2/constraint");
        if (constraintElement.HasValue)
            constraints = NormalizeToArray(constraintElement.Value)
                .Select(ParseConstraint)
                .ToList();

        return factory(action, constraints);
    }

    private EdcConstraintDto ParseConstraint(JsonElement element)
    {
        return new EdcConstraintDto
        {
            LeftOperand  = ExtractIdOrValue(GetProperty(element,
                "odrl:leftOperand", "leftOperand", "http://www.w3.org/ns/odrl/2/leftOperand")),
            Operator     = ExtractIdOrValue(GetProperty(element,
                "odrl:operator", "operator", "http://www.w3.org/ns/odrl/2/operator")),
            RightOperand = ExtractIdOrValue(GetProperty(element,
                "odrl:rightOperand", "rightOperand", "http://www.w3.org/ns/odrl/2/rightOperand"))
        };
    }

    private EdcDistributionDto? ParseDistribution(JsonElement element)
    {
        try
        {
            var dist = new EdcDistributionDto();

            var formatElement = GetProperty(element, "dct:format",
                "http://purl.org/dc/terms/format");
            if (formatElement.HasValue)
            {
                dist.Format = formatElement.Value.ValueKind == JsonValueKind.Object
                    ? GetStringProperty(formatElement.Value, "@id", "id")
                    : formatElement.Value.GetString() ?? string.Empty;
            }

            var accessElement = GetProperty(element, "dcat:accessService",
                "https://www.w3.org/ns/dcat#accessService");
            if (accessElement.HasValue)
            {
                dist.AccessServiceId = accessElement.Value.ValueKind == JsonValueKind.Object
                    ? GetStringProperty(accessElement.Value, "@id", "id")
                    : accessElement.Value.GetString();
            }

            return dist;
        }
        catch
        {
            return null;
        }
    }

    // ── Utilidades JSON-LD ────────────────────────────────────────────────

    private static JsonElement? GetProperty(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var prop))
                return prop;
        }
        return null;
    }

    private static string GetStringProperty(JsonElement element, params string[] names)
    {
        var prop = GetProperty(element, names);
        if (!prop.HasValue) return string.Empty;
        return prop.Value.ValueKind == JsonValueKind.String
            ? prop.Value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static List<JsonElement> NormalizeToArray(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
            return element.EnumerateArray().ToList();
        if (element.ValueKind == JsonValueKind.Object)
            return [element];
        return [];
    }

    private static string ExtractIdOrValue(JsonElement? element)
    {
        if (!element.HasValue) return string.Empty;
        if (element.Value.ValueKind == JsonValueKind.Object)
            return GetStringProperty(element.Value, "@id", "id");
        if (element.Value.ValueKind == JsonValueKind.String)
            return element.Value.GetString() ?? string.Empty;
        return string.Empty;
    }

    private static string? NullIfEmpty(string value)
        => string.IsNullOrEmpty(value) ? null : value;
}
