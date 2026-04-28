using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Entities.DTOs;
using GreenTransit.Application.Features.Entities.Queries;
using GreenTransit.Application.Features.ServiceOrders.DTOs;
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
        return await _context.ServiceOrders
            .AsNoTracking()
            .Where(s => s.Id == request.Id)
            .Select(s => new ServiceOrderDetailDto(
                s.Id,
                s.ServiceOrderNumber,
                s.IssuedAt,
                s.IdIssuedBy,
                s.IssuedBy != null ? s.IssuedBy.Name : s.IssuedByName,
                s.IssuedBy != null ? s.IssuedBy.NationalId : s.IssuedByNationalId,
                s.IssuedBy != null ? s.IssuedBy.CenterCode : s.IssuedByCenterCode,
                s.Status,
                s.Priority,
                s.WasteStream,
                s.SubStream,
                s.ProductUse,
                s.ProductCategory,
                s.IdLERCode,
                s.LerCode != null ? s.LerCode.Code : null,
                s.LerCode != null ? s.LerCode.Description : null,
                s.LerCode != null && s.LerCode.IsDangerous,
                s.IdPickupPoint,
                s.PickupPoint != null ? s.PickupPoint.Name : null,
                s.PlannedPickupStart,
                s.PlannedPickupEnd,
                s.PlannedDeliveryStart,
                s.PlannedDeliveryEnd,
                s.EstimatedWeight,
                s.MeasureUnit,
                s.Units,
                s.ContainersJson,
                s.IdCarrier,
                s.Carrier != null ? s.Carrier.Name : null,
                s.IdPlannedPlant,
                s.PlannedPlant != null ? s.PlannedPlant.Name : null,
                s.WasteMoveReference,
                s.TicketScalePlanned,
                s.Version,
                s.CreatedAt,
                s.UpdatedAt,
                s.IdUser))
            .FirstOrDefaultAsync(cancellationToken);
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
