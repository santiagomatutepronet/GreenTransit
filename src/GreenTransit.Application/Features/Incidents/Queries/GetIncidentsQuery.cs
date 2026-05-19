using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Common.Models;
using GreenTransit.Application.Features.Incidents.DTOs;
using GreenTransit.Domain.Authorization;
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
    int       PageNumber         = 1,
    int       PageSize           = 15
) : IRequest<PaginatedResult<IncidentDto>>;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetIncidentsQueryHandler
    : IRequestHandler<GetIncidentsQuery, PaginatedResult<IncidentDto>>
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

    public async Task<PaginatedResult<IncidentDto>> Handle(
        GetIncidentsQuery request, CancellationToken ct)
    {
        var ownerId        = _currentUser.OwnerId;
        var linkedEntityId = _currentUser.LinkedEntityId;

        var query = _context.Incidents
            .AsNoTracking()
            .Where(i => ownerId == Guid.Empty || i.OwnerId == ownerId);

        // ── Filtro por perfil (if-else: un solo perfil activo por usuario) ────
        if (_currentUser.IsInProfile(ProfileConstants.Producer))
        {
            // PRODUCER: solo incidencias cuya SO fue emitida por su entidad
            query = query.Where(i =>
                i.ServiceOrderId != null &&
                i.ServiceOrder != null &&
                i.ServiceOrder.IdIssuedBy == linkedEntityId);
        }
        else if (_currentUser.IsInProfile(ProfileConstants.Scrap))
        {
            // SCRAP: incidencias vinculadas a traslados donde figure como IdScrap o IdScrap2
            query = query.Where(i =>
                (i.ServiceOrderId != null &&
                 _context.WasteMoves.Any(wm =>
                     wm.ServiceOrderId == i.ServiceOrderId &&
                     (wm.IdScrap == linkedEntityId || wm.IdScrap2 == linkedEntityId))) ||
                (i.WasteMoveReference != null &&
                 _context.WasteMoves.Any(wm =>
                     wm.WasteMoveReference == i.WasteMoveReference &&
                     (wm.IdScrap == linkedEntityId || wm.IdScrap2 == linkedEntityId))));
        }
        else if (_currentUser.IsInProfile(ProfileConstants.Carrier))
        {
            // CARRIER: incidencias vinculadas a traslados donde es transportista
            var wmIds = _context.WasteMoveResidues
                .Where(wmr => wmr.IdCarrier == linkedEntityId)
                .Select(wmr => wmr.IdWasteMove)
                .Distinct();
            query = query.Where(i =>
                i.WasteMoveReference != null &&
                _context.WasteMoves.Any(wm =>
                    wm.WasteMoveReference == i.WasteMoveReference &&
                    wmIds.Contains(wm.Id)));
        }
        else if (_currentUser.IsInProfile(ProfileConstants.PublicEnt))
        {
            // PUBLIC_ENT: incidencias de SOs emitidas por su entidad
            var soIds = _context.ServiceOrders
                .Where(so => so.IdIssuedBy == linkedEntityId && (ownerId == Guid.Empty || so.OwnerId == ownerId))
                .Select(so => so.Id);
            query = query.Where(i =>
                (i.ServiceOrderId != null && soIds.Contains(i.ServiceOrderId.Value)));
        }
        else if (_currentUser.IsInProfile(ProfileConstants.CacOp))
        {
            // CAC_OP: incidencias vinculadas a traslados con destino u origen su CAC
            var wmRefs = _context.WasteMoves
                .Where(wm => (wm.IdDestination == linkedEntityId || wm.IdSource == linkedEntityId)
                          && (ownerId == Guid.Empty || wm.OwnerId == ownerId)
                          && wm.WasteMoveReference != null)
                .Select(wm => wm.WasteMoveReference!);
            query = query.Where(i =>
                i.WasteMoveReference != null && wmRefs.Contains(i.WasteMoveReference));
        }
        else if (_currentUser.IsInProfile(ProfileConstants.PlantOp))
        {
            // PLANT_OP: incidencias de traslados con destino su planta
            var wmRefs = _context.WasteMoves
                .Where(wm => wm.IdDestination == linkedEntityId
                          && (ownerId == Guid.Empty || wm.OwnerId == ownerId)
                          && wm.WasteMoveReference != null)
                .Select(wm => wm.WasteMoveReference!);
            query = query.Where(i =>
                i.WasteMoveReference != null && wmRefs.Contains(i.WasteMoveReference));
        }
        else if (_currentUser.IsInProfile(ProfileConstants.Coordinator))
        {
            // COORDINATOR: incidencias de traslados de SCRAPs coordinados
            var scrapIds = _context.Agreements
                .Where(a => a.IdCoordinator == linkedEntityId && (ownerId == Guid.Empty || a.OwnerId == ownerId))
                .Select(a => a.IdScrap);
            query = query.Where(i =>
                i.WasteMoveReference != null &&
                _context.WasteMoves.Any(wm =>
                    wm.WasteMoveReference == i.WasteMoveReference &&
                    scrapIds.Contains(wm.IdScrap)));
        }
        // DISPATCH_OFFICE / ADMIN: sin filtro adicional

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

        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var items = await query
            .OrderByDescending(i => i.OpenedAt)
            .Skip((request.PageNumber - 1) * pageSize)
            .Take(pageSize)
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

        return PaginatedResult<IncidentDto>.Create(items, total, request.PageNumber, pageSize);
    }
}
