using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Common.Models;
using GreenTransit.Application.Features.WasteMoves.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.WasteMoves.Queries;

// ── GetWasteMovesQuery — lista paginada con filtros ───────────────────────────

public sealed record GetWasteMovesQuery(
    string?   ServiceStatus      = null,
    Guid?     IdSource           = null,
    Guid?     IdDestination      = null,
    Guid?     IdScrap            = null,
    Guid?     ServiceOrderIssuedBy = null,
    DateTime? DateFrom           = null,
    DateTime? DateTo             = null,
    string?   SearchTerm         = null,
    int       PageNumber         = 1,
    int       PageSize           = 20
) : IRequest<PaginatedResult<WasteMoveDto>>;

public sealed class GetWasteMovesQueryHandler
    : IRequestHandler<GetWasteMovesQuery, PaginatedResult<WasteMoveDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetWasteMovesQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<WasteMoveDto>> Handle(
        GetWasteMovesQuery request, CancellationToken ct)
    {
        var ownerId        = _currentUser.OwnerId;
        var isScrap        = _currentUser.IsInProfile(GreenTransit.Domain.Authorization.ProfileConstants.Scrap);
        var linkedEntityId = _currentUser.LinkedEntityId;

        var q = _context.WasteMoves.AsNoTracking()
            .Where(w => ownerId == Guid.Empty || w.OwnerId == ownerId);

        // SCRAP: solo traslados donde figure como IdScrap o IdScrap2
        if (isScrap && linkedEntityId.HasValue)
            q = q.Where(w =>
                w.IdScrap == linkedEntityId.Value ||
                w.IdScrap2 == linkedEntityId.Value);

        if (!string.IsNullOrWhiteSpace(request.ServiceStatus))
            q = q.Where(w => w.ServiceStatus == request.ServiceStatus);

        if (request.IdSource.HasValue)
            q = q.Where(w => w.IdSource == request.IdSource);

        if (request.IdDestination.HasValue)
            q = q.Where(w => w.IdDestination == request.IdDestination);

        if (request.IdScrap.HasValue)
            q = q.Where(w => w.IdScrap == request.IdScrap);

        if (request.ServiceOrderIssuedBy.HasValue)
            q = q.Where(w =>
                w.ServiceOrderId != null &&
                w.ServiceOrder != null &&
                w.ServiceOrder.IdIssuedBy == request.ServiceOrderIssuedBy.Value);

        if (request.DateFrom.HasValue)
            q = q.Where(w => w.PlannedPickupStart >= request.DateFrom);

        if (request.DateTo.HasValue)
            q = q.Where(w => w.PlannedPickupStart <= request.DateTo);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.ToLower();
            q = q.Where(w => w.WasteMoveReference != null
                           && w.WasteMoveReference.ToLower().Contains(term));
        }

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(w => w.RequestDate)
            .ThenByDescending(w => w.DateCreateSys)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(w => new WasteMoveDto(
                w.Id,
                w.WasteMoveReference,
                w.ServiceStatus,
                w.IdSource,
                w.Source != null ? w.Source.Name : null,
                w.IdDestination,
                w.Destination != null ? w.Destination.Name : null,
                w.PlannedPickupStart,
                w.RequestDate,
                w.WasteMoveResidues.Count
            ))
            .ToListAsync(ct);

        return PaginatedResult<WasteMoveDto>.Create(
            items, total, request.PageNumber, request.PageSize);
    }
}
