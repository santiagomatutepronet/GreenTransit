using GreenTransit.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.EcoDataNet.Commands;

public class DeleteExplorerLayoutConfigCommandHandler
    : IRequestHandler<DeleteExplorerLayoutConfigCommand, bool>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService   _currentUser;

    public DeleteExplorerLayoutConfigCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser)
    {
        _db          = db;
        _currentUser = currentUser;
    }

    public async Task<bool> Handle(
        DeleteExplorerLayoutConfigCommand request,
        CancellationToken cancellationToken)
    {
        var ownerId = _currentUser.OwnerId;
        var userId  = _currentUser.IdUser;

        var record = await _db.ExplorerLayoutConfigs
            .Where(c => c.OwnerId              == ownerId
                     && c.UserId               == userId
                     && c.AssetId              == request.AssetId
                     && c.ProviderParticipantId == request.ProviderParticipantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (record is null)
            return false;

        _db.Remove(record);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
