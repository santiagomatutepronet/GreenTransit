using FluentValidation;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.EcoDataNet.DTOs;
using GreenTransit.Domain.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace GreenTransit.Application.Features.EcoDataNet.Commands;

// ── Command ───────────────────────────────────────────────────────────────────

public sealed record StartContractNegotiationCommand : IRequest<EdcNegotiationResponse>
{
    public int ConsumerUserId { get; init; }
    public string DatasetId { get; init; } = string.Empty;
    public string OfferId { get; init; } = string.Empty;
    public string ProviderParticipantId { get; init; } = string.Empty;
    public string ProviderProtocolEndpoint { get; init; } = string.Empty;
    public string? RawOfferJson { get; init; }
    public EdcOfferDto? Offer { get; init; }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class StartContractNegotiationCommandValidator
    : AbstractValidator<StartContractNegotiationCommand>
{
    public StartContractNegotiationCommandValidator()
    {
        RuleFor(x => x.ConsumerUserId).GreaterThan(0);
        RuleFor(x => x.DatasetId).NotEmpty();
        RuleFor(x => x.OfferId).NotEmpty();
        RuleFor(x => x.ProviderParticipantId).NotEmpty();
        RuleFor(x => x.ProviderProtocolEndpoint).NotEmpty()
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("ProviderProtocolEndpoint debe ser una URL válida.");
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class StartContractNegotiationCommandHandler
    : IRequestHandler<StartContractNegotiationCommand, EdcNegotiationResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService   _currentUser;
    private readonly IEdcManagementClient  _edcClient;
    private readonly ILogger<StartContractNegotiationCommandHandler> _logger;

    public StartContractNegotiationCommandHandler(
        IApplicationDbContext  db,
        ICurrentUserService    currentUser,
        IEdcManagementClient   edcClient,
        ILogger<StartContractNegotiationCommandHandler> logger)
    {
        _db          = db;
        _currentUser = currentUser;
        _edcClient   = edcClient;
        _logger      = logger;
    }

    public async Task<EdcNegotiationResponse> Handle(
        StartContractNegotiationCommand request, CancellationToken ct)
    {
        var isAdmin = _currentUser.IsInProfile(ProfileConstants.Admin);

        // 1. SEGURIDAD: no-ADMIN solo puede usar su propio conector
        if (!isAdmin && request.ConsumerUserId != _currentUser.IdUser)
            throw new ValidationException("No tiene permiso para negociar en nombre de otro usuario.");

        // 2. OBTENER CONECTOR DEL CONSUMIDOR (multi-tenant: filtrar por OwnerId)
        var consumerConnector = await _db.UserEDCConnectors
            .AsNoTracking()
            .Include(c => c.User)
            .FirstOrDefaultAsync(
                c => c.UserId == request.ConsumerUserId
                  && c.User.OwnerId == _currentUser.OwnerId, ct);

        if (consumerConnector is null)
            throw new ValidationException(
                "El usuario consumidor no tiene un conector EDC configurado.");

        var consumerMgmtUrl = $"https://mgmt.{consumerConnector.EDCServerName}/management";

        // 3. CONSTRUIR PAYLOAD
        var payload = BuildContractRequestPayload(request);

        // Diagnóstico: comparar OfferId con el @id real de la RawOfferJson capturada
        // del catálogo. Una desviación aquí provoca TERMINATED en el provider.
        var rawOfferAtId = TryExtractRawOfferAtId(request.RawOfferJson ?? request.Offer?.RawOfferJson);
        if (!string.IsNullOrEmpty(rawOfferAtId) && rawOfferAtId != request.OfferId)
        {
            _logger.LogWarning(
                "El OfferId del command ({CommandOfferId}) NO coincide con el @id del RawOfferJson ({RawAtId}). " +
                "El provider terminará la negociación. Usa el @id exacto del nodo odrl:hasPolicy del catálogo.",
                request.OfferId, rawOfferAtId);
        }

        _logger.LogInformation(
            "Iniciando negociación: Consumer={Server}, Provider={Endpoint}, Asset={Asset}, Offer={Offer}, RawOfferAtId={RawAtId}",
            consumerConnector.EDCServerName, request.ProviderProtocolEndpoint,
            request.DatasetId, request.OfferId, rawOfferAtId ?? "(sin RawOfferJson)");

        _logger.LogInformation("Payload de negociación enviado:\n{Payload}", payload);

        // 4. ENVIAR
        return await _edcClient.StartNegotiationAsync(consumerMgmtUrl, payload, consumerConnector.ApiKey, ct);
    }

    /// <summary>
    /// Extrae el "@id" del RawOfferJson capturado del catálogo, sin lanzar excepciones.
    /// </summary>
    private static string? TryExtractRawOfferAtId(string? rawOfferJson)
    {
        if (string.IsNullOrWhiteSpace(rawOfferJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(rawOfferJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            if (doc.RootElement.TryGetProperty("@id", out var atId)
                && atId.ValueKind == JsonValueKind.String)
                return atId.GetString();
        }
        catch (JsonException) { }
        return null;
    }

    private static string BuildContractRequestPayload(StartContractNegotiationCommand request)
    {
        using var ms     = new System.IO.MemoryStream();
        using var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = false });

        writer.WriteStartObject();

        // @context mínimo: solo @vocab EDC — no incluimos odrl.jsonld para evitar
        // que EDC intente resolver IRIs ODRL en el contexto y falle la validación.
        writer.WritePropertyName("@context");
        writer.WriteStartObject();
        writer.WriteString("@vocab", "https://w3id.org/edc/v0.0.1/ns/");
        writer.WriteEndObject();

        writer.WriteString("@type", "ContractRequest");
        writer.WriteString("counterPartyAddress", request.ProviderProtocolEndpoint);
        writer.WriteString("protocol", "dataspace-protocol-http");

        // offerId, connectorId y assetId en raíz (formato EDC 0.13 con DSP)
        writer.WriteString("offerId", request.OfferId);
        writer.WriteString("connectorId", request.ProviderParticipantId);
        writer.WriteString("assetId", request.DatasetId);

        // policy: copia literal del RawOfferJson del catálogo.
        // El provider valida que sea idéntica a la de su contractDefinition.
        writer.WritePropertyName("policy");
        WritePolicyFromRawOrDto(writer, request);

        writer.WritePropertyName("callbackAddresses");
        writer.WriteStartArray();
        writer.WriteEndArray();

        writer.WriteEndObject();
        writer.Flush();

        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>
    /// El provider puede devolver odrl:rightOperand como un toString() de Java, p.ej.:
    ///   "[{@value={valueType=STRING, chars=eco_uc_scrapa, string=eco_uc_scrapa}}, ...]"
    /// Esto no es JSON válido. Este método extrae los valores "chars" y produce un array JSON real.
    /// </summary>
    private static string SanitizeJavaToStringArrayValues(string rawJson)
    {
        // Detecta una cadena JSON que contiene un toString de ArrayList de Java con @value nodes
        return System.Text.RegularExpressions.Regex.Replace(
            rawJson,
            @"""(\[\{@value=\{[^""]+\}\}[^""]*\])""",
            m =>
            {
                var inner = m.Groups[1].Value;
                var chars = System.Text.RegularExpressions.Regex.Matches(inner, @"chars=([^,}]+)");
                if (chars.Count == 0) return m.Value; // no tocar si no hay matches

                var jsonArray = "[" + string.Join(",",
                    chars.Cast<System.Text.RegularExpressions.Match>()
                         .Select(c => JsonSerializer.Serialize(c.Groups[1].Value.Trim()))) + "]";
                return jsonArray; // sustituye la cadena entrecomillada por un array JSON real
            });
    }

    private static void WritePolicyFromRawOrDto(Utf8JsonWriter writer, StartContractNegotiationCommand request)
    {
        var rawOfferJson = request.RawOfferJson ?? request.Offer?.RawOfferJson;

        if (!string.IsNullOrWhiteSpace(rawOfferJson))
        {
            // Sanear rightOperand con formato toString() de Java antes de parsear
            rawOfferJson = SanitizeJavaToStringArrayValues(rawOfferJson);

            try
            {
                using var doc = JsonDocument.Parse(rawOfferJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    // Copiar la policy literal del catálogo añadiendo/sobreescribiendo
                    // @id, @type, assigner y target para asegurar coherencia.
                    writer.WriteStartObject();

                    var root         = doc.RootElement;
                    var writtenAtId  = false;
                    var writtenType  = false;
                    var writtenAssigner = false;
                    var writtenTarget   = false;

                    foreach (var prop in root.EnumerateObject())
                    {
                        switch (prop.Name)
                        {
                            case "@id":
                                writer.WriteString("@id", request.OfferId);
                                writtenAtId = true;
                                break;
                            case "@type":
                                // Siempre "Offer" en v3
                                writer.WriteString("@type", "Offer");
                                writtenType = true;
                                break;
                            case "assigner":
                            case "odrl:assigner":
                                writer.WriteString("assigner", request.ProviderParticipantId);
                                writtenAssigner = true;
                                break;
                            case "target":
                            case "odrl:target":
                                writer.WriteString("target", request.DatasetId);
                                writtenTarget = true;
                                break;
                            case "@context":
                                // No replicar el @context interno de la offer
                                break;
                            default:
                                // Copiar el resto literalmente (permission, prohibition, obligation, …)
                                writer.WritePropertyName(prop.Name);
                                prop.Value.WriteTo(writer);
                                break;
                        }
                    }

                    if (!writtenAtId)      writer.WriteString("@id",      request.OfferId);
                    if (!writtenType)      writer.WriteString("@type",     "Offer");
                    if (!writtenAssigner)  writer.WriteString("assigner",  request.ProviderParticipantId);
                    if (!writtenTarget)    writer.WriteString("target",    request.DatasetId);

                    writer.WriteEndObject();
                    return;
                }
            }
            catch (JsonException) { /* caer al fallback DTO */ }
        }

        // Fallback: reconstruir desde DTOs parseados
        WritePolicyFromDto(writer, request);
    }

    private static void WritePolicyFromDto(Utf8JsonWriter writer, StartContractNegotiationCommand request)
    {
        writer.WriteStartObject();
        writer.WriteString("@id",      request.OfferId);
        writer.WriteString("@type",    "Offer");
        writer.WriteString("assigner", request.ProviderParticipantId);

        writer.WritePropertyName("permission");
        writer.WriteStartArray();
        foreach (var perm in request.Offer?.Permissions ?? [])
        {
            writer.WriteStartObject();
            writer.WriteString("action", NormalizePrefix(perm.Action));
            WriteConstraintsDto(writer, perm.Constraints);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WritePropertyName("prohibition");
        writer.WriteStartArray();
        foreach (var prohib in request.Offer?.Prohibitions ?? [])
        {
            writer.WriteStartObject();
            writer.WriteString("action", NormalizePrefix(prohib.Action));
            WriteConstraintsDto(writer, prohib.Constraints);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WritePropertyName("obligation");
        writer.WriteStartArray();
        foreach (var obl in request.Offer?.Obligations ?? [])
        {
            writer.WriteStartObject();
            writer.WriteString("action", NormalizePrefix(obl.Action));
            WriteConstraintsDto(writer, obl.Constraints);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WriteString("target", request.DatasetId);
        writer.WriteEndObject();
    }

    private static void WriteConstraintsDto(Utf8JsonWriter writer, List<EdcConstraintDto> constraints)
    {
        if (constraints.Count == 0) return;

        writer.WritePropertyName("constraint");
        if (constraints.Count == 1)
        {
            WriteConstraintDto(writer, constraints[0]);
            return;
        }

        writer.WriteStartArray();
        foreach (var c in constraints)
            WriteConstraintDto(writer, c);
        writer.WriteEndArray();
    }

    private static void WriteConstraintDto(Utf8JsonWriter writer, EdcConstraintDto c)
    {
        var operatorNorm = NormalizePrefix(c.Operator);
        writer.WriteStartObject();
        writer.WriteString("leftOperand",  NormalizeLeftOperand(c.LeftOperand));
        writer.WriteString("operator",     operatorNorm);
        writer.WritePropertyName("rightOperand");
        // isAnyOf: string CSV según EDC 0.13; demás operadores: string simple
        if (operatorNorm.Equals("isAnyOf", StringComparison.OrdinalIgnoreCase))
            writer.WriteStringValue(c.RightOperand.Replace(", ", ","));
        else
            writer.WriteStringValue(c.RightOperand);
        writer.WriteEndObject();
    }

    private static string NormalizePrefix(string value)
    {
        if (value.StartsWith("odrl:", StringComparison.Ordinal)) return value[5..];
        if (value.StartsWith("edc:",  StringComparison.Ordinal)) return value[4..];
        return value;
    }

    private static string NormalizeLeftOperand(string value)
    {
        if (value.StartsWith("edc:", StringComparison.Ordinal))
            return $"https://w3id.org/edc/v0.0.1/ns/{value[4..]}";

        if (value is "participant" or "purpose")
            return $"https://w3id.org/edc/v0.0.1/ns/{value}";

        return NormalizePrefix(value);
    }
}
