using FluentValidation;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.EcoDataNet.DTOs;
using GreenTransit.Domain.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GreenTransit.Application.Features.EcoDataNet.Queries;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetTransferStateQuery : IRequest<EdcTransferStateResponse>
{
    public int ConsumerUserId { get; init; }
    public string TransferProcessId { get; init; } = string.Empty;
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class GetTransferStateQueryValidator : AbstractValidator<GetTransferStateQuery>
{
    public GetTransferStateQueryValidator()
    {
        RuleFor(x => x.ConsumerUserId).GreaterThan(0);
        RuleFor(x => x.TransferProcessId).NotEmpty();
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetTransferStateQueryHandler
    : IRequestHandler<GetTransferStateQuery, EdcTransferStateResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService   _currentUser;
    private readonly IEdcManagementClient  _edcClient;
    private readonly ILogger<GetTransferStateQueryHandler> _logger;

    public GetTransferStateQueryHandler(
        IApplicationDbContext db,
        ICurrentUserService   currentUser,
        IEdcManagementClient  edcClient,
        ILogger<GetTransferStateQueryHandler> logger)
    {
        _db          = db;
        _currentUser = currentUser;
        _edcClient   = edcClient;
        _logger      = logger;
    }

    public async Task<EdcTransferStateResponse> Handle(
        GetTransferStateQuery request, CancellationToken ct)
    {
        var isAdmin = _currentUser.IsInProfile(ProfileConstants.Admin);
        if (!isAdmin && request.ConsumerUserId != _currentUser.IdUser)
            throw new ValidationException("No tiene permiso para consultar transferencias de otro usuario.");

        var consumerConnector = await _db.UserEDCConnectors
            .AsNoTracking()
            .Include(c => c.User)
            .FirstOrDefaultAsync(
                c => c.UserId == request.ConsumerUserId
                  && c.User.OwnerId == _currentUser.OwnerId, ct);

        if (consumerConnector is null)
            throw new ValidationException("El usuario consumidor no tiene un conector EDC configurado.");

        var consumerMgmtUrl = $"https://mgmt.{consumerConnector.EDCServerName}/management";

        _logger.LogDebug("Consultando estado transferencia {Id} en {Server}",
            request.TransferProcessId, consumerConnector.EDCServerName);

        return await _edcClient.GetTransferStateAsync(consumerMgmtUrl, request.TransferProcessId, consumerConnector.ApiKey, ct);
    }
}
