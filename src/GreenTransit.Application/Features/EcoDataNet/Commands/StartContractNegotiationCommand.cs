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
            .WithMessage("ProviderProtocolEndpoint debe ser una URL válida.")
            .Must(url => url.Contains("/protocol", StringComparison.OrdinalIgnoreCase))
            .WithMessage("ProviderProtocolEndpoint debe apuntar al endpoint de protocolo DSP (/protocol), no al de management.")
            .Must(url => !url.Contains("/management", StringComparison.OrdinalIgnoreCase))
            .WithMessage("ProviderProtocolEndpoint no debe apuntar al endpoint /management del conector provider.");
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

        // 3. PRE-VALIDACIÓN DE POLÍTICA: comprobar si la oferta restringe por edc:participant
        //    antes de enviar la negociación, para dar un error útil en lugar de TERMINATED.
        var rawOffer = request.RawOfferJson ?? request.Offer?.RawOfferJson;
        var policyWarning = CheckParticipantPolicyConstraint(
            rawOffer, consumerConnector.EDCConnectorId, _logger);
        if (policyWarning != null)
        {
            _logger.LogWarning("Pre-validación política fallida: {Warning}", policyWarning);
            return new EdcNegotiationResponse
            {
                Success      = false,
                ErrorMessage = policyWarning
            };
        }
        //    "Value in JsonObjects name/value pair cannot be null" en EDC.
        var fieldErrors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.ProviderParticipantId))
            fieldErrors.Add("ProviderParticipantId (connectorId/assigner) está vacío — debe coincidir con el participantId del provider.");
        if (string.IsNullOrWhiteSpace(request.ProviderProtocolEndpoint))
            fieldErrors.Add("ProviderProtocolEndpoint (counterPartyAddress) está vacío.");
        if (string.IsNullOrWhiteSpace(request.DatasetId))
            fieldErrors.Add("DatasetId (target del asset) está vacío — debe coincidir con el assetId exacto del catálogo.");
        if (string.IsNullOrWhiteSpace(request.OfferId))
            fieldErrors.Add("OfferId está vacío — debe obtenerse del campo @id de odrl:hasPolicy en /catalog/request, NO construirse manualmente.");

        // Log diagnóstico antes de construir el payload
        _logger.LogInformation(
            "Validación campos negociación — ConsumerMgmt={ConsumerMgmt} | ConnectorId(provider)={ConnectorId} | " +
            "CounterPartyAddress={CounterParty} | AssetId(target)={AssetId} | OfferId={OfferId} | " +
            "ProviderParticipantId={ParticipantId} | RawOfferJson presente={HasRaw}",
            consumerMgmtUrl,
            request.ProviderParticipantId,
            request.ProviderProtocolEndpoint,
            request.DatasetId,
            request.OfferId,
            request.ProviderParticipantId,
            !string.IsNullOrWhiteSpace(request.RawOfferJson ?? request.Offer?.RawOfferJson));

        if (fieldErrors.Count > 0)
        {
            var errorMsg = "Payload de negociación inválido: " + string.Join(" | ", fieldErrors);
            _logger.LogError("StartContractNegotiation abortado — {Errors}", errorMsg);
            throw new ValidationException(errorMsg);
        }

        // 4. CONSTRUIR PAYLOAD
        _logger.LogInformation("RawOfferJson recibido del catálogo:\n{RawOffer}",
            request.RawOfferJson ?? request.Offer?.RawOfferJson ?? "(vacío)");

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

        // 5. ENVIAR
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

        // @context raíz: array con ODRL y vocabulario EDC (formato v3).
        writer.WritePropertyName("@context");
        writer.WriteStartArray();
        writer.WriteStringValue("http://www.w3.org/ns/odrl.jsonld");
        writer.WriteStartObject();
        writer.WriteString("@vocab", "https://w3id.org/edc/v0.0.1/ns/");
        writer.WriteString("edc", "https://w3id.org/edc/v0.0.1/ns/");
        writer.WriteEndObject();
        writer.WriteEndArray();

        writer.WriteString("@type", "ContractRequest");
        writer.WriteString("counterPartyAddress", request.ProviderProtocolEndpoint);
        writer.WriteString("protocol", "dataspace-protocol-http");

        // Policy ODRL Offer completa: se usa la policy del catálogo (RawOfferJson)
        // para que el @id coincida exactamente con el OfferId del provider y evitar TERMINATED.
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
            // NO sanitizar aquí: el rightOperand con toString() de Java debe enviarse
            // exactamente como el provider lo almacenó para que la comparación de políticas pase.
            // SanitizeJavaToStringArrayValues solo se usa para la pre-validación (extracción de IDs).
            try
            {
                using var doc = JsonDocument.Parse(rawOfferJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    // Copiar la policy literal del catálogo añadiendo/sobreescribiendo
                    // @id, @type, assigner y target para asegurar coherencia.
                    writer.WriteStartObject();

                    var root            = doc.RootElement;
                    var writtenAtId     = false;
                    var writtenType     = false;
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
                                writer.WriteString("@type", "Offer");
                                writtenType = true;
                                break;
                            case "assigner":
                            case "odrl:assigner":
                            case "http://www.w3.org/ns/odrl/2/assigner":
                                writer.WritePropertyName("odrl:assigner");
                                writer.WriteStartObject();
                                writer.WriteString("@id", request.ProviderParticipantId);
                                writer.WriteEndObject();
                                writtenAssigner = true;
                                break;
                            case "target":
                            case "odrl:target":
                            case "http://www.w3.org/ns/odrl/2/target":
                                writer.WritePropertyName("odrl:target");
                                writer.WriteStartObject();
                                writer.WriteString("@id", request.DatasetId);
                                writer.WriteEndObject();
                                writtenTarget = true;
                                break;
                            case "@context":
                                // Omitir: el @context ODRL ya está declarado en el raíz
                                break;
                            case "permission":
                            case "odrl:permission":
                            case "http://www.w3.org/ns/odrl/2/permission":
                                WriteOdrlArrayProperty(writer, "odrl:permission", prop.Value);
                                break;
                            case "prohibition":
                            case "odrl:prohibition":
                            case "http://www.w3.org/ns/odrl/2/prohibition":
                                WriteOdrlArrayProperty(writer, "odrl:prohibition", prop.Value);
                                break;
                            case "obligation":
                            case "odrl:obligation":
                            case "http://www.w3.org/ns/odrl/2/obligation":
                                WriteOdrlArrayProperty(writer, "odrl:obligation", prop.Value);
                                break;
                            default:
                                // Copiar el resto literalmente (odrl:permission, odrl:prohibition, odrl:obligation, …)
                                writer.WritePropertyName(prop.Name);
                                prop.Value.WriteTo(writer);
                                break;
                        }
                    }

                    if (!writtenAtId)      writer.WriteString("@id",      request.OfferId);
                    if (!writtenType)      writer.WriteString("@type",     "Offer");
                    if (!writtenAssigner)
                    {
                        writer.WritePropertyName("odrl:assigner");
                        writer.WriteStartObject();
                        writer.WriteString("@id", request.ProviderParticipantId);
                        writer.WriteEndObject();
                    }
                    if (!writtenTarget)
                    {
                        writer.WritePropertyName("odrl:target");
                        writer.WriteStartObject();
                        writer.WriteString("@id", request.DatasetId);
                        writer.WriteEndObject();
                    }

                    writer.WriteEndObject();
                    return;
                }
            }
            catch (JsonException) { /* caer al fallback DTO */ }
        }

        // Fallback: reconstruir desde DTOs parseados
        WritePolicyFromDto(writer, request);
    }

    private static void WriteOdrlArrayProperty(Utf8JsonWriter writer, string propertyName, JsonElement value)
    {
        writer.WritePropertyName(propertyName);

        if (value.ValueKind == JsonValueKind.Array)
        {
            value.WriteTo(writer);
            return;
        }

        writer.WriteStartArray();
        if (value.ValueKind != JsonValueKind.Null && value.ValueKind != JsonValueKind.Undefined)
            value.WriteTo(writer);
        writer.WriteEndArray();
    }

    private static void WritePolicyFromDto(Utf8JsonWriter writer, StartContractNegotiationCommand request)
    {
        writer.WriteStartObject();
        writer.WriteString("@id",      request.OfferId);
        writer.WriteString("@type",    "Offer");

        writer.WritePropertyName("odrl:assigner");
        writer.WriteStartObject();
        writer.WriteString("@id", request.ProviderParticipantId);
        writer.WriteEndObject();

        writer.WritePropertyName("odrl:target");
        writer.WriteStartObject();
        writer.WriteString("@id", request.DatasetId);
        writer.WriteEndObject();

        writer.WritePropertyName("odrl:permission");
        writer.WriteStartArray();
        foreach (var perm in request.Offer?.Permissions ?? [])
        {
            writer.WriteStartObject();
            WriteOdrlActionObject(writer, "odrl:action", perm.Action);
            WriteOdrlConstraintsDto(writer, perm.Constraints);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WritePropertyName("odrl:prohibition");
        writer.WriteStartArray();
        foreach (var prohib in request.Offer?.Prohibitions ?? [])
        {
            writer.WriteStartObject();
            WriteOdrlActionObject(writer, "odrl:action", prohib.Action);
            WriteOdrlConstraintsDto(writer, prohib.Constraints);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WritePropertyName("odrl:obligation");
        writer.WriteStartArray();
        foreach (var obl in request.Offer?.Obligations ?? [])
        {
            writer.WriteStartObject();
            WriteOdrlActionObject(writer, "odrl:action", obl.Action);
            WriteOdrlConstraintsDto(writer, obl.Constraints);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WriteEndObject();
    }

    // Escribe: "odrl:action": { "@id": "odrl:use" }
    private static void WriteOdrlActionObject(Utf8JsonWriter writer, string propertyName, string actionValue)
    {
        var normalized = EnsureOdrlPrefix(actionValue);
        writer.WritePropertyName(propertyName);
        writer.WriteStartObject();
        writer.WriteString("@id", normalized);
        writer.WriteEndObject();
    }

    private static void WriteOdrlConstraintsDto(Utf8JsonWriter writer, List<EdcConstraintDto> constraints)
    {
        if (constraints.Count == 0) return;

        writer.WritePropertyName("odrl:constraint");
        if (constraints.Count == 1)
        {
            WriteOdrlConstraintDto(writer, constraints[0]);
            return;
        }

        writer.WriteStartArray();
        foreach (var c in constraints)
            WriteOdrlConstraintDto(writer, c);
        writer.WriteEndArray();
    }

    private static void WriteOdrlConstraintDto(Utf8JsonWriter writer, EdcConstraintDto c)
    {
        writer.WriteStartObject();

        // odrl:leftOperand: { "@id": "edc:participant" }
        writer.WritePropertyName("odrl:leftOperand");
        writer.WriteStartObject();
        writer.WriteString("@id", NormalizeLeftOperandId(c.LeftOperand));
        writer.WriteEndObject();

        // odrl:operator: { "@id": "odrl:isAnyOf" }
        writer.WritePropertyName("odrl:operator");
        writer.WriteStartObject();
        writer.WriteString("@id", EnsureOdrlPrefix(c.Operator));
        writer.WriteEndObject();

        // odrl:rightOperand: string o array
        writer.WritePropertyName("odrl:rightOperand");
        var op = c.Operator.StartsWith("odrl:", StringComparison.Ordinal) ? c.Operator[5..] : c.Operator;
        if (op.Equals("isAnyOf", StringComparison.OrdinalIgnoreCase))
        {
            var values = c.RightOperand
                .Split(new[] { "|", ",", ", " }, StringSplitOptions.RemoveEmptyEntries);
            writer.WriteStartArray();
            foreach (var v in values)
                writer.WriteStringValue(v.Trim());
            writer.WriteEndArray();
        }
        else
        {
            writer.WriteStringValue(c.RightOperand);
        }

        writer.WriteEndObject();
    }

    private static string EnsureOdrlPrefix(string value)
    {
        if (value.StartsWith("odrl:", StringComparison.Ordinal)) return value;
        if (value.StartsWith("edc:",  StringComparison.Ordinal)) return value;
        return $"odrl:{value}";
    }

    private static string NormalizeLeftOperandId(string value)
    {
        if (value.StartsWith("edc:",  StringComparison.Ordinal)) return value;
        if (value.StartsWith("odrl:", StringComparison.Ordinal)) return value;
        if (value.StartsWith("http",  StringComparison.Ordinal)) return value;
        // sin prefijo: asumir edc:
        return $"edc:{value}";
    }

    /// <summary>
    /// Revisa si la oferta contiene una restricción "edc:participant isAnyOf [...]" o similar
    /// y verifica que el EDCConnectorId del consumer esté incluido.
    /// Devuelve un mensaje de error legible si la política rechazará la negociación,
    /// o null si no hay restricción de participante o sí está autorizado.
    /// </summary>
    private static string? CheckParticipantPolicyConstraint(
        string? rawOfferJson,
        string consumerConnectorId,
        ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(rawOfferJson)) return null;

        try
        {
            using var doc = JsonDocument.Parse(rawOfferJson);
            var root = doc.RootElement;

            // Recorrer permissions buscando constraints con leftOperand = participant
            foreach (var permPropName in new[] { "odrl:permission", "permission", "http://www.w3.org/ns/odrl/2/permission" })
            {
                if (!root.TryGetProperty(permPropName, out var permsEl)) continue;

                var perms = permsEl.ValueKind == JsonValueKind.Array
                    ? permsEl.EnumerateArray().ToList()
                    : new List<JsonElement> { permsEl };

                foreach (var perm in perms)
                {
                    foreach (var constraintPropName in new[] { "odrl:constraint", "constraint", "http://www.w3.org/ns/odrl/2/constraint" })
                    {
                        if (!perm.TryGetProperty(constraintPropName, out var constraintsEl)) continue;

                        var constraints = constraintsEl.ValueKind == JsonValueKind.Array
                            ? constraintsEl.EnumerateArray().ToList()
                            : new List<JsonElement> { constraintsEl };

                        foreach (var constraint in constraints)
                        {
                            var leftOperand = ExtractJsonLdId(constraint, "odrl:leftOperand", "leftOperand");
                            if (!leftOperand.Contains("participant", StringComparison.OrdinalIgnoreCase))
                                continue;

                            var op = ExtractJsonLdId(constraint, "odrl:operator", "operator");
                            var isListOp = op.Contains("isAnyOf", StringComparison.OrdinalIgnoreCase)
                                        || op.Contains("isAllOf", StringComparison.OrdinalIgnoreCase)
                                        || op.Contains("isNoneOf", StringComparison.OrdinalIgnoreCase);

                            var authorized = ExtractAllRightOperandValues(constraint);

                            logger.LogInformation(
                                "Restricción participant encontrada — operador={Op} participantes autorizados=[{List}] connectorId={ConnectorId}",
                                op, string.Join(", ", authorized), consumerConnectorId);

                            if (op.Contains("isNoneOf", StringComparison.OrdinalIgnoreCase))
                            {
                                if (authorized.Contains(consumerConnectorId, StringComparer.OrdinalIgnoreCase))
                                    return $"Tu conector '{consumerConnectorId}' está explícitamente excluido de esta oferta (odrl:isNoneOf). " +
                                           $"Solicita al proveedor que modifique la política del asset.";
                                return null;
                            }

                            if (isListOp && authorized.Count > 0)
                            {
                                if (!authorized.Contains(consumerConnectorId, StringComparer.OrdinalIgnoreCase))
                                    return $"Tu conector '{consumerConnectorId}' no está autorizado en la política de esta oferta. " +
                                           $"Participantes permitidos: [{string.Join(", ", authorized)}]. " +
                                           $"Solicita al proveedor que añada tu participantId a la política del asset.";
                            }
                        }
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "No se pudo parsear RawOfferJson para pre-validación de participant");
        }

        return null;
    }

    private static string ExtractJsonLdId(JsonElement element, params string[] propNames)
    {
        foreach (var name in propNames)
        {
            if (!element.TryGetProperty(name, out var prop)) continue;
            if (prop.ValueKind == JsonValueKind.Object && prop.TryGetProperty("@id", out var id))
                return id.GetString() ?? string.Empty;
            if (prop.ValueKind == JsonValueKind.String)
                return prop.GetString() ?? string.Empty;
        }
        return string.Empty;
    }

    private static List<string> ExtractAllRightOperandValues(JsonElement constraint)
    {
        var result = new List<string>();
        foreach (var name in new[] { "odrl:rightOperand", "rightOperand", "http://www.w3.org/ns/odrl/2/rightOperand" })
        {
            if (!constraint.TryGetProperty(name, out var prop)) continue;

            if (prop.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in prop.EnumerateArray())
                {
                    var v = item.ValueKind == JsonValueKind.Object && item.TryGetProperty("@id", out var id)
                        ? id.GetString()
                        : item.ValueKind == JsonValueKind.String ? item.GetString() : null;
                    if (!string.IsNullOrEmpty(v)) result.Add(v);
                }
            }
            else if (prop.ValueKind == JsonValueKind.String)
            {
                var raw = prop.GetString();
                if (string.IsNullOrEmpty(raw)) break;

                // El provider puede devolver un toString() de Java, p.ej.:
                // "[{@value={valueType=STRING, chars=eco_uc_scrapa, string=eco_uc_scrapa}}, ...]"
                // Extraer todos los valores "chars=<valor>"
                var javaCharsMatches = System.Text.RegularExpressions.Regex.Matches(
                    raw, @"chars=([^,}\]]+)");
                if (javaCharsMatches.Count > 0)
                {
                    foreach (System.Text.RegularExpressions.Match m in javaCharsMatches)
                        result.Add(m.Groups[1].Value.Trim());
                }
                else
                {
                    result.Add(raw);
                }
            }
            break;
        }
        return result;
    }


}
