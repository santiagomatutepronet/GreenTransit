using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Common.Models;
using GreenTransit.Application.Features.ServiceOrders.DTOs;
using GreenTransit.Domain.Authorization;
using MediatR;

namespace GreenTransit.Application.Features.ServiceOrders.Queries;

// ── GetServiceOrdersQuery — lista paginada con filtros ────────────────────────

public sealed record GetServiceOrdersQuery(
    string?   Status               = null,
    string?   Priority             = null,
    Guid?     IdIssuedBy           = null,
    Guid?     IdPickupPoint        = null,
    Guid?     IdLERCode            = null,
    string?   WasteStream          = null,
    DateTime? PlannedPickupFrom    = null,
    DateTime? PlannedPickupTo      = null,
    string?   SearchTerm           = null,
    int       PageNumber           = 1,
    int       PageSize             = 15,
    string[]? Statuses             = null,
    bool      OnlyWithoutWasteMove = false
) : IRequest<PaginatedResult<ServiceOrderDto>>;

public sealed class GetServiceOrdersQueryHandler
    : IRequestHandler<GetServiceOrdersQuery, PaginatedResult<ServiceOrderDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetServiceOrdersQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<ServiceOrderDto>> Handle(
        GetServiceOrdersQuery request, CancellationToken cancellationToken)
    {
        var ownerId        = _currentUser.OwnerId;
        var linkedEntityId = _currentUser.LinkedEntityId;

        var q = _context.ServiceOrders.AsNoTracking()
            .Where(s => ownerId == Guid.Empty || s.OwnerId == ownerId);

        // ── Filtro por perfil ─────────────────────────────────────────────────
        if (_currentUser.IsInProfile(ProfileConstants.Producer) || _currentUser.IsInProfile(ProfileConstants.PublicEnt))
        {
            // PRODUCER / PUBLIC_ENT: solo sus propias SOs (IdIssuedBy)
            q = q.Where(s => s.IdIssuedBy == linkedEntityId);
        }
        else if (_currentUser.IsInProfile(ProfileConstants.Scrap))
        {
            // SCRAP: SOs sin traslado asignado aún, o cuyo traslado les incluye como IdScrap/IdScrap2
            q = q.Where(s =>
                !_context.WasteMoves.Any(wm => wm.ServiceOrderId == s.Id) ||
                _context.WasteMoves.Any(wm =>
                    wm.ServiceOrderId == s.Id &&
                    (wm.IdScrap == linkedEntityId || wm.IdScrap2 == linkedEntityId)));
        }
        else if (_currentUser.IsInProfile(ProfileConstants.Carrier))
        {
            // CARRIER: SOs vinculadas a traslados donde es transportista
            var wmIds = _context.WasteMoveResidues
                .Where(wmr => wmr.IdCarrier == linkedEntityId)
                .Select(wmr => wmr.IdWasteMove);
            var soIds = _context.WasteMoves
                .Where(wm => wmIds.Contains(wm.Id) && wm.ServiceOrderId != null)
                .Select(wm => wm.ServiceOrderId!.Value);
            q = q.Where(s => soIds.Contains(s.Id));
        }
        else if (_currentUser.IsInProfile(ProfileConstants.CacOp))
        {
            // CAC_OP: SOs cuyo punto de recogida es su entidad, o cuyo traslado tiene destino su CAC
            q = q.Where(s =>
                s.IdPickupPoint == linkedEntityId ||
                _context.WasteMoves.Any(wm =>
                    wm.ServiceOrderId == s.Id &&
                    wm.IdDestination == linkedEntityId));
        }
        else if (_currentUser.IsInProfile(ProfileConstants.PlantOp))
        {
            // PLANT_OP: SOs cuyo traslado tiene destino su planta
            q = q.Where(s =>
                _context.WasteMoves.Any(wm =>
                    wm.ServiceOrderId == s.Id &&
                    wm.IdDestination == linkedEntityId));
        }
        else if (_currentUser.IsInProfile(ProfileConstants.Coordinator))
        {
            // COORDINATOR: SOs de traslados de SCRAPs de sus acuerdos
            var scrapIds = _context.Agreements
                .Where(a => a.IdCoordinator == linkedEntityId && (ownerId == Guid.Empty || a.OwnerId == ownerId))
                .Select(a => a.IdScrap);
            q = q.Where(s =>
                _context.WasteMoves.Any(wm =>
                    wm.ServiceOrderId == s.Id &&
                    scrapIds.Contains(wm.IdScrap)));
        }
        // DISPATCH_OFFICE / ADMIN: sin filtro adicional

        if (!string.IsNullOrWhiteSpace(request.Status))
            q = q.Where(s => s.Status == request.Status);

        if (request.Statuses is { Length: > 0 })
            q = q.Where(s => request.Statuses.Contains(s.Status));

        if (request.OnlyWithoutWasteMove)
            q = q.Where(s => s.WasteMoveReference == null || s.WasteMoveReference == "");

        if (!string.IsNullOrWhiteSpace(request.Priority))
            q = q.Where(s => s.Priority == request.Priority);

        if (request.IdIssuedBy.HasValue)
            q = q.Where(s => s.IdIssuedBy == request.IdIssuedBy);

        if (request.IdPickupPoint.HasValue)
            q = q.Where(s => s.IdPickupPoint == request.IdPickupPoint);

        if (request.IdLERCode.HasValue)
            q = q.Where(s => s.IdLERCode == request.IdLERCode);

        if (!string.IsNullOrWhiteSpace(request.WasteStream))
            q = q.Where(s => s.WasteStream == request.WasteStream);

        if (request.PlannedPickupFrom.HasValue)
            q = q.Where(s => s.PlannedPickupStart >= request.PlannedPickupFrom);

        if (request.PlannedPickupTo.HasValue)
            q = q.Where(s => s.PlannedPickupStart <= request.PlannedPickupTo);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.ToLower();
            q = q.Where(s => s.ServiceOrderNumber.ToLower().Contains(term)
                           || (s.WasteMoveReference != null && s.WasteMoveReference.ToLower().Contains(term)));
        }

        var total = await q.CountAsync(cancellationToken);

        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var items = await q
            .OrderByDescending(s => s.IssuedAt)
            .Skip((request.PageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new ServiceOrderDto(
                s.Id,
                s.ServiceOrderNumber,
                s.Status,
                s.Priority,
                s.IssuedAt,
                s.PlannedPickupStart,
                s.IdPickupPoint,
                s.PickupPoint != null ? s.PickupPoint.Name : null,
                s.WasteStream,
                s.Residues.Sum(r => (decimal?)r.EstimatedWeight),
                s.MeasureUnit,
                s.WasteMoveReference,
                s.IdLERCode,
                s.LerCode != null ? s.LerCode.Code : null,
                s.LerCode != null ? s.LerCode.Description : null,
                string.Join(", ", s.Residues
                    .Where(r => r.LerCode != null && r.LerCode.Code != null)
                    .Select(r => r.LerCode!.Code!)
                    .Distinct()
                    .OrderBy(c => c))))
            .ToListAsync(cancellationToken);

        return PaginatedResult<ServiceOrderDto>.Create(items, total, request.PageNumber, pageSize);
    }
}

