using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.WasteMoves.DTOs;
using MediatR;

namespace GreenTransit.Application.Features.WasteMoves.Queries;

// ── GetWasteMoveByIdQuery — detalle completo ──────────────────────────────────

public sealed record GetWasteMoveByIdQuery(Guid Id) : IRequest<WasteMoveDetailDto?>;

public sealed class GetWasteMoveByIdQueryHandler
    : IRequestHandler<GetWasteMoveByIdQuery, WasteMoveDetailDto?>
{
    private readonly IApplicationDbContext _context;

    public GetWasteMoveByIdQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<WasteMoveDetailDto?> Handle(
        GetWasteMoveByIdQuery request, CancellationToken ct)
    {
        var w = await _context.WasteMoves
            .AsNoTracking()
            .Include(x => x.Source)
            .Include(x => x.Destination)
            .Include(x => x.Scrap)
            .Include(x => x.Scrap2)
            .Include(x => x.OperatorTransfer)
            .Include(x => x.ServiceOrder)
                .ThenInclude(so => so!.LerCode)
            .Include(x => x.WasteMoveResidues)
                .ThenInclude(r => r.Residue)
                    .ThenInclude(res => res!.LerCode)
            .Include(x => x.WasteMoveResidues)
                .ThenInclude(r => r.LerCode)
            .Include(x => x.WasteMoveResidues)
                .ThenInclude(r => r.TreatmentOperationDestiny)
            .Include(x => x.WasteMoveResidues)
                .ThenInclude(r => r.Carrier)
            .FirstOrDefaultAsync(x => x.Id == request.Id, ct);

        if (w is null) return null;

        var residues = w.WasteMoveResidues
            .Select(r => new WasteMoveResidueDto(
                r.Id,
                r.IdWasteMove,
                r.IdResidue,
                r.Residue?.Name,
                r.Residue?.IsDangerous ?? false,
                r.Residue?.IsRAEE ?? false,
                r.IdLerCode,
                r.LerCode?.Code ?? r.Residue?.LerCode?.Code,
                r.LerCode?.Description ?? r.Residue?.LerCode?.Description,
                r.Weight,
                r.MeasureUnit,
                r.Units,
                r.UnitPriceKg,
                r.DateDelivery,
                r.IdTreatmentOperationDestiny,
                r.TreatmentOperationDestiny?.Code,
                r.TreatmentOperationDestiny?.Description,
                r.IdCarrier,
                r.Carrier?.Name,
                r.NTNumber,
                r.DINumber,
                r.DIPhase,
                r.VehicleType,
                r.FuelType,
                r.EuroClass,
                r.TransportInfo_TransportDistance,
                r.TransportInfo_TransportCarbonEmissions,
                r.EmissionFactorSetId,
                r.EmissionFactorVersion
            ))
            .ToList();

        return new WasteMoveDetailDto(
            w.Id,
            w.WasteMoveReference,
            w.ServiceStatus,
            w.OwnerId,
            w.IdSource,
            w.Source?.Name,
            w.IdDestination,
            w.Destination?.Name,
            w.IdScrap,
            w.Scrap?.Name,
            w.IdScrap2,
            w.Scrap2?.Name,
            w.IdOperatorTransfer,
            w.OperatorTransfer?.Name,
            w.ServiceOrderId,
            w.ServiceOrder?.ServiceOrderNumber,
            w.ServiceOrder?.IdLERCode,
            w.ServiceOrder?.LerCode?.Code,
            w.ServiceOrder?.LerCode?.Description,
            w.ServiceOrder?.LerCode?.IsDangerous ?? false,
            w.RequestDate,
            w.PlannedPickupStart,
            w.PlannedPickupEnd,
            w.PlannedDeliveryStart,
            w.PlannedDeliveryEnd,
            w.ActualPickupStart,
            w.ActualPickupEnd,
            w.ActualDeliveryStart,
            w.ActualDeliveryEnd,
            w.Lot,
            w.DocumentId,
            w.SignatureStatus,
            w.Version,
            w.DateCreateSys,
            w.DateModifiedSys,
            w.IdUser,
            residues
        );
    }
}
