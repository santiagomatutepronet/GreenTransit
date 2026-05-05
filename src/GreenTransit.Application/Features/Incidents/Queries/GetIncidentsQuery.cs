using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Incidents.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.Incidents.Queries;

// ── Query ─────────────────────────────────────────────────────────────────────

/// <summary>Devuelve una página de incidencias filtradas por OwnerId.</summary>
public sealed record GetIncidentsQuery(
    string?   Severity           = null,
    bool?     IsOpen             = null,
    string?   Type               = null,
    Guid?     ServiceOrderId     = null,
    string?   WasteMoveReference = null,
    DateTime? DateFrom           = null,
    DateTime? DateTo             = null,
    int       Page               = 1,
    int       PageSize           = 20
) : IRequest<GetIncidentsResult>;

public sealed record GetIncidentsResult(
    IReadOnlyList<IncidentDto> Items,
    int                        TotalCount,
    int                        Page,
    int                        PageSize
);

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetIncidentsQueryHandler
    : IRequestHandler<GetIncidentsQuery, GetIncidentsResult>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetIncidentsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<GetIncidentsResult> Handle(
        GetIncidentsQuery request, CancellationToken ct)
    {
        var ownerId        = _currentUser.OwnerId;
        var isProducer     = _currentUser.IsInProfile(GreenTransit.Domain.Authorization.ProfileConstants.Producer);
        var isScrap        = _currentUser.IsInProfile(GreenTransit.Domain.Authorization.ProfileConstants.Scrap);
        var linkedEntityId = _currentUser.LinkedEntityId;

        var query = _context.Incidents
            .AsNoTracking()
            .Where(i => ownerId == Guid.Empty || i.OwnerId == ownerId);

        // PRODUCER: solo incidencias cuya SO fue emitida por su entidad (§3.2)
        if (isProducer && linkedEntityId.HasValue)
            query = query.Where(i =>
                i.ServiceOrderId != null &&
                i.ServiceOrder != null &&
                i.ServiceOrder.IdIssuedBy == linkedEntityId.Value);

        // SCRAP: solo incidencias vinculadas a traslados donde figure como IdScrap o IdScrap2
        if (isScrap && linkedEntityId.HasValue)
            query = query.Where(i =>
                (i.ServiceOrderId != null &&
                 _context.WasteMoves.Any(wm =>
                     wm.ServiceOrderId == i.ServiceOrderId &&
                     (wm.IdScrap == linkedEntityId.Value || wm.IdScrap2 == linkedEntityId.Value))) ||
                (i.WasteMoveReference != null &&
                 _context.WasteMoves.Any(wm =>
                     wm.WasteMoveReference == i.WasteMoveReference &&
                     (wm.IdScrap == linkedEntityId.Value || wm.IdScrap2 == linkedEntityId.Value))));

        if (!string.IsNullOrWhiteSpace(request.Severity))
            query = query.Where(i => i.Severity == request.Severity);

        if (request.IsOpen.HasValue)
            query = request.IsOpen.Value
                ? query.Where(i => i.ClosedAt == null)
                : query.Where(i => i.ClosedAt != null);

        if (!string.IsNullOrWhiteSpace(request.Type))
            query = query.Where(i => i.Type == request.Type);

        if (request.ServiceOrderId.HasValue)
            query = query.Where(i => i.ServiceOrderId == request.ServiceOrderId);

        if (!string.IsNullOrWhiteSpace(request.WasteMoveReference))
            query = query.Where(i =>
                i.WasteMoveReference!.Contains(request.WasteMoveReference));

        if (request.DateFrom.HasValue)
            query = query.Where(i => i.OpenedAt >= request.DateFrom.Value);

        if (request.DateTo.HasValue)
            query = query.Where(i => i.OpenedAt <= request.DateTo.Value);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(i => i.OpenedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(i => new IncidentDto(
                i.Id,
                i.Type,
                i.Severity,
                i.ClosedAt == null,
                i.OpenedAt,
                i.ClosedAt,
                i.ServiceOrderId,
                i.WasteMoveReference,
                i.TicketScale,
                i.ReportedByName,
                i.Description))
            .ToListAsync(ct);

        return new GetIncidentsResult(items, total, request.Page, request.PageSize);
    }
}
