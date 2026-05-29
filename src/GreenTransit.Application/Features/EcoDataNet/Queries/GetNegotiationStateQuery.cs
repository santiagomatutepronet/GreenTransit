using FluentValidation;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.EcoDataNet.DTOs;
using GreenTransit.Domain.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GreenTransit.Application.Features.EcoDataNet.Queries;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetNegotiationStateQuery : IRequest<EdcNegotiationStateResponse>
{
    public int ConsumerUserId { get; init; }
    public string NegotiationId { get; init; } = string.Empty;
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class GetNegotiationStateQueryValidator : AbstractValidator<GetNegotiationStateQuery>
{
    public GetNegotiationStateQueryValidator()
    {
        RuleFor(x => x.ConsumerUserId).GreaterThan(0);
        RuleFor(x => x.NegotiationId).NotEmpty();
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetNegotiationStateQueryHandler
    : IRequestHandler<GetNegotiationStateQuery, EdcNegotiationStateResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService   _currentUser;
    private readonly IEdcManagementClient  _edcClient;
    private readonly ILogger<GetNegotiationStateQueryHandler> _logger;

    public GetNegotiationStateQueryHandler(
        IApplicationDbContext db,
        ICurrentUserService   currentUser,
        IEdcManagementClient  edcClient,
        ILogger<GetNegotiationStateQueryHandler> logger)
    {
        _db          = db;
        _currentUser = currentUser;
        _edcClient   = edcClient;
        _logger      = logger;
    }

    public async Task<EdcNegotiationStateResponse> Handle(
        GetNegotiationStateQuery request, CancellationToken ct)
    {
        var isAdmin = _currentUser.IsInProfile(ProfileConstants.Admin);
        if (!isAdmin && request.ConsumerUserId != _currentUser.IdUser)
            throw new ValidationException("No tiene permiso para consultar negociaciones de otro usuario.");

        var consumerConnector = await _db.UserEDCConnectors
            .AsNoTracking()
            .Include(c => c.User)
            .FirstOrDefaultAsync(
                c => c.UserId == request.ConsumerUserId
                  && c.User.OwnerId == _currentUser.OwnerId, ct);

        if (consumerConnector is null)
            throw new ValidationException("El usuario consumidor no tiene un conector EDC configurado.");

        var consumerMgmtUrl = $"https://mgmt.{consumerConnector.EDCServerName}/management";

        _logger.LogDebug("Consultando estado negociación {Id} en {Server}",
            request.NegotiationId, consumerConnector.EDCServerName);

        return await _edcClient.GetNegotiationStateAsync(consumerMgmtUrl, request.NegotiationId, consumerConnector.ApiKey, ct);
    }
}
