using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Common.Models;
using GreenTransit.Application.Features.WasteMoves.DTOs;
using GreenTransit.Domain.Authorization;
using GreenTransit.Domain.Entities;
using MediatR;

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
    int       PageSize           = 15,
    string?   SortBy             = null,
    bool      SortDescending     = true
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
        var linkedEntityId = _currentUser.LinkedEntityId;

        var q = _context.WasteMoves.AsNoTracking()
            .Where(w => ownerId == Guid.Empty || w.OwnerId == ownerId);

        // ── Filtro por perfil ─────────────────────────────────────────────────
        if (_currentUser.IsInProfile(ProfileConstants.Producer))
        {
            // PRODUCER: solo traslados de SOs emitidas por su entidad
            var soIds = _context.ServiceOrders
                .Where(so => so.IdIssuedBy == linkedEntityId && (ownerId == Guid.Empty || so.OwnerId == ownerId))
                .Select(so => so.Id);
            q = q.Where(w => w.ServiceOrderId != null && soIds.Contains(w.ServiceOrderId.Value));
        }
        else if (_currentUser.IsInProfile(ProfileConstants.Carrier))
        {
            // CARRIER: solo traslados donde es transportista asignado
            var wmIds = _context.WasteMoveResidues
                .Where(wmr => wmr.IdCarrier == linkedEntityId)
                .Select(wmr => wmr.IdWasteMove)
                .Distinct();
            q = q.Where(w => wmIds.Contains(w.Id));
        }
        else if (_currentUser.IsInProfile(ProfileConstants.Scrap))
        {
            // SCRAP: traslados donde figura como IdScrap o IdScrap2
            q = q.Where(w =>
                w.IdScrap == linkedEntityId ||
                w.IdScrap2 == linkedEntityId);
        }
        else if (_currentUser.IsInProfile(ProfileConstants.PublicEnt))
        {
            // PUBLIC_ENT: traslados de SOs emitidas por la entidad
            var soIds = _context.ServiceOrders
                .Where(so => so.IdIssuedBy == linkedEntityId && (ownerId == Guid.Empty || so.OwnerId == ownerId))
                .Select(so => so.Id);
            q = q.Where(w => w.ServiceOrderId != null && soIds.Contains(w.ServiceOrderId.Value));
        }
        else if (_currentUser.IsInProfile(ProfileConstants.CacOp))
        {
            // CAC_OP: traslados cuyo destino u origen es su CAC
            q = q.Where(w =>
                w.IdDestination == linkedEntityId ||
                w.IdSource == linkedEntityId);
        }
        else if (_currentUser.IsInProfile(ProfileConstants.PlantOp))
        {
            // PLANT_OP: traslados cuyo destino es su planta
            q = q.Where(w => w.IdDestination == linkedEntityId);
        }
        else if (_currentUser.IsInProfile(ProfileConstants.Coordinator))
        {
            // COORDINATOR: traslados de SCRAPs de sus acuerdos
            var scrapIds = _context.Agreements
                .Where(a => a.IdCoordinator == linkedEntityId && (ownerId == Guid.Empty || a.OwnerId == ownerId))
                .Select(a => a.IdScrap);
            q = q.Where(w => scrapIds.Contains(w.IdScrap));
        }
        // DISPATCH_OFFICE / ADMIN: sin filtro adicional

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
            var pattern = $"%{request.SearchTerm.Trim()}%";
            q = q.Where(w => w.WasteMoveReference != null
                           && EF.Functions.Like(w.WasteMoveReference, pattern));
        }

        var total = await q.CountAsync(ct);

        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        IQueryable<GreenTransit.Domain.Entities.WasteMove> sorted = (request.SortBy?.ToLowerInvariant()) switch
        {
            "wastemovereference"  => request.SortDescending ? q.OrderByDescending(w => w.WasteMoveReference)  : q.OrderBy(w => w.WasteMoveReference),
            "servicestatus"       => request.SortDescending ? q.OrderByDescending(w => w.ServiceStatus)        : q.OrderBy(w => w.ServiceStatus),
            "sourcename"          => request.SortDescending ? q.OrderByDescending(w => w.Source!.Name)         : q.OrderBy(w => w.Source!.Name),
            "destinationname"     => request.SortDescending ? q.OrderByDescending(w => w.Destination!.Name)    : q.OrderBy(w => w.Destination!.Name),
            "plannedpickupstart"  => request.SortDescending ? q.OrderByDescending(w => w.PlannedPickupStart)   : q.OrderBy(w => w.PlannedPickupStart),
            "requestdate"         => request.SortDescending ? q.OrderByDescending(w => w.RequestDate)          : q.OrderBy(w => w.RequestDate),
            _                     => q.OrderByDescending(w => w.RequestDate).ThenByDescending(w => w.DateCreateSys),
        };

        var items = await sorted
            .Skip((request.PageNumber - 1) * pageSize)
            .Take(pageSize)
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
            items, total, request.PageNumber, pageSize);
    }
}
