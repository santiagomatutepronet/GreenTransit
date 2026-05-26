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

public sealed record StartTransferProcessCommand : IRequest<EdcTransferResponse>
{
    public int ConsumerUserId { get; init; }
    public string ContractAgreementId { get; init; } = string.Empty;
    public string AssetId { get; init; } = string.Empty;
    public string ProviderProtocolEndpoint { get; init; } = string.Empty;
    public string TransferType { get; init; } = "HttpData-PULL";
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class StartTransferProcessCommandValidator
    : AbstractValidator<StartTransferProcessCommand>
{
    public StartTransferProcessCommandValidator()
    {
        RuleFor(x => x.ConsumerUserId).GreaterThan(0);
        RuleFor(x => x.ContractAgreementId).NotEmpty();
        RuleFor(x => x.AssetId).NotEmpty();
        RuleFor(x => x.ProviderProtocolEndpoint).NotEmpty()
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("ProviderProtocolEndpoint debe ser una URL válida.");
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class StartTransferProcessCommandHandler
    : IRequestHandler<StartTransferProcessCommand, EdcTransferResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService   _currentUser;
    private readonly IEdcManagementClient  _edcClient;
    private readonly ILogger<StartTransferProcessCommandHandler> _logger;

    public StartTransferProcessCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService   currentUser,
        IEdcManagementClient  edcClient,
        ILogger<StartTransferProcessCommandHandler> logger)
    {
        _db          = db;
        _currentUser = currentUser;
        _edcClient   = edcClient;
        _logger      = logger;
    }

    public async Task<EdcTransferResponse> Handle(
        StartTransferProcessCommand request, CancellationToken ct)
    {
        var isAdmin = _currentUser.IsInProfile(ProfileConstants.Admin);
        if (!isAdmin && request.ConsumerUserId != _currentUser.IdUser)
            throw new ValidationException("No tiene permiso para iniciar transferencias en nombre de otro usuario.");

        var consumerConnector = await _db.UserEDCConnectors
            .AsNoTracking()
            .Include(c => c.User)
            .FirstOrDefaultAsync(
                c => c.UserId == request.ConsumerUserId
                  && c.User.OwnerId == _currentUser.OwnerId, ct);

        if (consumerConnector is null)
            throw new ValidationException("El usuario consumidor no tiene un conector EDC configurado.");

        var consumerMgmtUrl = $"https://mgmt.{consumerConnector.EDCServerName}/management";

        var payload = BuildTransferRequestPayload(request);

        _logger.LogInformation(
            "Iniciando transferencia: Consumer={Server}, Contract={Contract}, Asset={Asset}, Type={Type}",
            consumerConnector.EDCServerName, request.ContractAgreementId, request.AssetId, request.TransferType);

        return await _edcClient.StartTransferAsync(consumerMgmtUrl, payload, consumerConnector.ApiKey, ct);
    }

    private static string BuildTransferRequestPayload(StartTransferProcessCommand request)
    {
        var body = new Dictionary<string, object>
        {
            ["@context"]            = new Dictionary<string, string> { ["@vocab"] = "https://w3id.org/edc/v0.0.1/ns/" },
            ["@type"]               = "TransferRequestDto",
            ["counterPartyAddress"] = request.ProviderProtocolEndpoint,
            ["contractId"]          = request.ContractAgreementId,
            ["assetId"]             = request.AssetId,
            ["protocol"]            = "dataspace-protocol-http",
            ["transferType"]        = request.TransferType
        };

        return JsonSerializer.Serialize(body, new JsonSerializerOptions { WriteIndented = false });
    }
}
