using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.ServiceOrders.DTOs;
using GreenTransit.Application.Features.WasteMoves.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.ServiceOrders.Queries;

// ── GetServiceOrderByIdQuery ──────────────────────────────────────────────────

public sealed record GetServiceOrderByIdQuery(Guid Id) : IRequest<ServiceOrderDetailDto?>;

public sealed class GetServiceOrderByIdQueryHandler
    : IRequestHandler<GetServiceOrderByIdQuery, ServiceOrderDetailDto?>
{
    private readonly IApplicationDbContext _context;

    public GetServiceOrderByIdQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<ServiceOrderDetailDto?> Handle(
        GetServiceOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var so = await _context.ServiceOrders
            .AsNoTracking()
            .Include(s => s.IssuedBy)
            .Include(s => s.PickupPoint)
            .Include(s => s.Carrier)
            .Include(s => s.PlannedPlant)
            .Include(s => s.Residues)
                .ThenInclude(r => r.LerCode)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (so is null) return null;

        var residues = so.Residues
            .OrderBy(r => r.SortOrder)
            .Select(r => new ServiceOrderResidueDto(
                r.Id,
                r.IdServiceOrder,
                r.SortOrder,
                r.IdLERCode,
                r.LerCode?.Code,
                r.LerCode?.Description,
                r.LerCode?.IsDangerous ?? false,
                r.ProductUse,
                r.ProductCategory,
                r.EstimatedWeight,
                r.MeasureUnit,
                r.Units))
            .ToList();

        return new ServiceOrderDetailDto(
            so.Id,
            so.ServiceOrderNumber,
            so.IssuedAt,
            so.IdIssuedBy,
            so.IssuedBy != null ? so.IssuedBy.Name : so.IssuedByName,
            so.IssuedBy != null ? so.IssuedBy.NationalId : so.IssuedByNationalId,
            so.IssuedBy != null ? so.IssuedBy.CenterCode : so.IssuedByCenterCode,
            so.Status,
            so.Priority,
            so.WasteStream,
            so.SubStream,
            so.IdPickupPoint,
            so.PickupPoint?.Name,
            so.PlannedPickupStart,
            so.PlannedPickupEnd,
            so.PlannedDeliveryStart,
            so.PlannedDeliveryEnd,
            so.ContainersJson,
            so.IdCarrier,
            so.Carrier?.Name,
            so.IdPlannedPlant,
            so.PlannedPlant?.Name,
            so.WasteMoveReference,
            so.TicketScalePlanned,
            so.Version,
            so.CreatedAt,
            so.UpdatedAt,
            so.IdUser,
            residues);
    }
}

// ── GetUpcomingServiceOrdersQuery — widget dashboard ─────────────────────────

public sealed record GetUpcomingServiceOrdersQuery(int Days = 7)
    : IRequest<IReadOnlyList<ServiceOrderDto>>;

public sealed class GetUpcomingServiceOrdersQueryHandler
    : IRequestHandler<GetUpcomingServiceOrdersQuery, IReadOnlyList<ServiceOrderDto>>
{
    private readonly IApplicationDbContext _context;

    public GetUpcomingServiceOrdersQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<IReadOnlyList<ServiceOrderDto>> Handle(
        GetUpcomingServiceOrdersQuery request, CancellationToken cancellationToken)
    {
        var from = DateTime.UtcNow.Date;
        var to   = from.AddDays(request.Days);

        return await _context.ServiceOrders
            .AsNoTracking()
            .Where(s => s.Status != "Cancelled"
                     && s.PlannedPickupStart >= from
                     && s.PlannedPickupStart <= to)
            .OrderBy(s => s.PlannedPickupStart)
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
    }
}
