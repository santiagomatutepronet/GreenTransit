using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Common.Models;
using GreenTransit.Application.Features.ServiceOrders.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.ServiceOrders.Queries;

// ── GetServiceOrdersQuery — lista paginada con filtros ────────────────────────

public sealed record GetServiceOrdersQuery(
    string?   Status            = null,
    string?   Priority          = null,
    Guid?     IdIssuedBy        = null,
    Guid?     IdPickupPoint     = null,
    Guid?     IdLERCode         = null,
    DateTime? PlannedPickupFrom = null,
    DateTime? PlannedPickupTo   = null,
    string?   SearchTerm        = null,
    int       PageNumber        = 1,
    int       PageSize          = 20
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
        var isScrap        = _currentUser.IsInProfile(GreenTransit.Domain.Authorization.ProfileConstants.Scrap);
        var linkedEntityId = _currentUser.LinkedEntityId;

        var q = _context.ServiceOrders.AsNoTracking()
            .Where(s => ownerId == Guid.Empty || s.OwnerId == ownerId);

        // SCRAP: SOs sin traslado asignado aún, o cuyo traslado les tiene como IdScrap/IdScrap2
        if (isScrap && linkedEntityId.HasValue)
            q = q.Where(s =>
                !_context.WasteMoves.Any(wm => wm.ServiceOrderId == s.Id) ||
                _context.WasteMoves.Any(wm =>
                    wm.ServiceOrderId == s.Id &&
                    (wm.IdScrap == linkedEntityId.Value || wm.IdScrap2 == linkedEntityId.Value)));

        if (!string.IsNullOrWhiteSpace(request.Status))
            q = q.Where(s => s.Status == request.Status);

        if (!string.IsNullOrWhiteSpace(request.Priority))
            q = q.Where(s => s.Priority == request.Priority);

        if (request.IdIssuedBy.HasValue)
            q = q.Where(s => s.IdIssuedBy == request.IdIssuedBy);

        if (request.IdPickupPoint.HasValue)
            q = q.Where(s => s.IdPickupPoint == request.IdPickupPoint);

        if (request.IdLERCode.HasValue)
            q = q.Where(s => s.IdLERCode == request.IdLERCode);

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

        var items = await q
            .OrderByDescending(s => s.IssuedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
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
                s.EstimatedWeight,
                s.MeasureUnit,
                s.WasteMoveReference,
                s.IdLERCode,
                s.LerCode != null ? s.LerCode.Code : null,
                s.LerCode != null ? s.LerCode.Description : null))
            .ToListAsync(cancellationToken);

        return PaginatedResult<ServiceOrderDto>.Create(items, total, request.PageNumber, request.PageSize);
    }
}

