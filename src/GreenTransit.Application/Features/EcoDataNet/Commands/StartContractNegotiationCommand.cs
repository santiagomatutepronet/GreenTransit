using FluentValidation;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.EcoDataNet.DTOs;
using GreenTransit.Domain.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

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

        _logger.LogInformation(
            "Iniciando negociación: Consumer={Server}, Provider={Endpoint}, Asset={Asset}, Offer={Offer}",
            consumerConnector.EDCServerName, request.ProviderProtocolEndpoint,
            request.DatasetId, request.OfferId);

        _logger.LogDebug("Payload de negociación: {Payload}", payload);

        // 4. ENVIAR
        return await _edcClient.StartNegotiationAsync(consumerMgmtUrl, payload, consumerConnector.ApiKey, ct);
    }

    private static string BuildContractRequestPayload(StartContractNegotiationCommand request)
    {
        using var ms     = new System.IO.MemoryStream();
        using var writer = new Utf8JsonWriter(ms);

        writer.WriteStartObject();

        writer.WritePropertyName("@context");
        writer.WriteStartObject();
        writer.WriteString("@vocab", "https://w3id.org/edc/v0.0.1/ns/");
        writer.WriteEndObject();

        writer.WriteString("@type", "ContractRequest");
        writer.WriteString("counterPartyAddress", request.ProviderProtocolEndpoint);
        writer.WriteString("protocol", "dataspace-protocol-http");

        writer.WritePropertyName("policy");
        writer.WriteStartObject();
        writer.WriteString("@context", "http://www.w3.org/ns/odrl.jsonld");
        writer.WriteString("@id",   request.OfferId);
        writer.WriteString("@type", "Offer");

        // odrl:permission
        writer.WritePropertyName("odrl:permission");
        writer.WriteStartArray();
        foreach (var perm in request.Offer?.Permissions ?? [])
            WritePermission(writer, perm);
        writer.WriteEndArray();

        // odrl:prohibition
        writer.WritePropertyName("odrl:prohibition");
        writer.WriteStartArray();
        foreach (var prohib in request.Offer?.Prohibitions ?? [])
            WriteProhibitionOrObligation(writer, prohib.Action, prohib.Constraints);
        writer.WriteEndArray();

        // odrl:obligation
        writer.WritePropertyName("odrl:obligation");
        writer.WriteStartArray();
        foreach (var obl in request.Offer?.Obligations ?? [])
            WriteProhibitionOrObligation(writer, obl.Action, obl.Constraints);
        writer.WriteEndArray();

        writer.WriteString("assigner", request.ProviderParticipantId);
        writer.WriteString("target",   request.DatasetId);
        writer.WriteEndObject(); // end policy

        writer.WriteEndObject(); // end ContractRequest
        writer.Flush();

        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>
    /// Escribe un permiso ODRL con su acción y restricciones.
    /// Los strings ya normalizados se reenvuelven en {"@id":"..."} según exige el conector.
    /// </summary>
    private static void WritePermission(Utf8JsonWriter writer, EdcPermissionDto perm)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("odrl:action");
        WriteIdObject(writer, perm.Action);
        WriteConstraints(writer, perm.Constraints);
        writer.WriteEndObject();
    }

    private static void WriteProhibitionOrObligation(
        Utf8JsonWriter writer, string action, List<EdcConstraintDto> constraints)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("odrl:action");
        WriteIdObject(writer, action);
        WriteConstraints(writer, constraints);
        writer.WriteEndObject();
    }

    private static void WriteConstraints(Utf8JsonWriter writer, List<EdcConstraintDto> constraints)
    {
        if (constraints.Count == 0) return;

        writer.WritePropertyName("odrl:constraint");
        writer.WriteStartArray();
        foreach (var c in constraints)
        {
            writer.WriteStartObject();

            writer.WritePropertyName("odrl:leftOperand");
            WriteIdObject(writer, c.LeftOperand);

            writer.WritePropertyName("odrl:operator");
            WriteIdObject(writer, c.Operator);

            // rightOperand: puede ser string simple o lista separada (de tipo isAnyOf)
            writer.WritePropertyName("odrl:rightOperand");
            WriteRightOperandSanitized(writer, c.RightOperand);

            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    /// <summary>Escribe {"@id": "value"} como espera el conector EDC.</summary>
    private static void WriteIdObject(Utf8JsonWriter writer, string value)
    {
        writer.WriteStartObject();
        writer.WriteString("@id", value);
        writer.WriteEndObject();
    }

    /// <summary>
    /// Detecta el formato Java toString "[{@value={valueType=STRING, chars=VAL, ...}}, ...]"
    /// y lo convierte al valor string real (o array si hay varios).
    /// Si no coincide, escribe el string tal cual.
    /// </summary>
    private static void WriteRightOperandSanitized(Utf8JsonWriter writer, string value)
    {
        var matches = Regex.Matches(value, @"chars=([^,}]+)");
        if (matches.Count == 1)
        {
            writer.WriteStringValue(matches[0].Groups[1].Value.Trim());
        }
        else if (matches.Count > 1)
        {
            writer.WriteStartArray();
            foreach (Match m in matches)
                writer.WriteStringValue(m.Groups[1].Value.Trim());
            writer.WriteEndArray();
        }
        else
        {
            writer.WriteStringValue(value);
        }
    }
}
