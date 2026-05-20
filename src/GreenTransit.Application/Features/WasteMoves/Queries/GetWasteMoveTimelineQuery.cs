using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.EntryCACs.DTOs;
using GreenTransit.Application.Features.EntryPlants.DTOs;
using GreenTransit.Application.Features.TreatmentPlants.DTOs;
using GreenTransit.Application.Features.WasteMoves.DTOs;
using GreenTransit.Domain.Constants;
using MediatR;

namespace GreenTransit.Application.Features.WasteMoves.Queries;

// ── Query ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Devuelve el ciclo de vida completo (vista 360º) de un Traslado de Residuos.
/// La visibilidad de cada sección depende del perfil del usuario activo:
///   - CARRIER  → solo ve líneas donde figura como IdCarrier.
///   - PLANT_OP → solo ve entradas y tratamientos de su OwnerId.
///   - ADMIN / SCRAP → visibilidad total.
/// </summary>
public sealed record GetWasteMoveTimelineQuery(Guid WasteMoveId) : IRequest<WasteMoveTimelineDto?>;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetWasteMoveTimelineQueryHandler
    : IRequestHandler<GetWasteMoveTimelineQuery, WasteMoveTimelineDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetWasteMoveTimelineQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<WasteMoveTimelineDto?> Handle(
        GetWasteMoveTimelineQuery request, CancellationToken ct)
    {
        var ownerId = _currentUser.OwnerId;
        var profile = _currentUser.UserProfile;

        // ── 1. Traslado principal ─────────────────────────────────────────────
        var wm = await _context.WasteMoves
            .AsNoTracking()
            .Include(x => x.Source)
            .Include(x => x.Destination)
            .Include(x => x.Scrap)
            .Include(x => x.OperatorTransfer)
            .Include(x => x.ServiceOrder)
                .ThenInclude(so => so!.LerCode)
            .Include(x => x.WasteMoveResidues)
                .ThenInclude(r => r.Residue)
            .Include(x => x.WasteMoveResidues)
                .ThenInclude(r => r.Carrier)
            .Include(x => x.WasteMoveResidues)
                .ThenInclude(r => r.EmissionFactorSet)
            .FirstOrDefaultAsync(
                x => x.Id == request.WasteMoveId
                  && (ownerId == Guid.Empty || x.OwnerId == ownerId),
                ct);

        if (wm is null) return null;

        var wmRef = wm.WasteMoveReference;

        // ── 2. Filtro de residuos por perfil ──────────────────────────────────
        var residueLines = wm.WasteMoveResidues
            .Where(r => profile != "CARRIER" || r.IdCarrier == GetCarrierEntityId(ownerId))
            .Select(r => new TimelineResidueDto(
                r.Id,
                r.IdResidue,
                r.Residue?.Name,
                r.Residue?.IsDangerous ?? false,
                r.Residue?.IsRAEE ?? false,
                r.Weight,
                r.MeasureUnit,
                r.NTNumber,
                r.DINumber,
                r.DIPhase,
                r.Carrier?.Name,
                r.TransportInfo_VehicleRegistration,
                r.VehicleType,
                r.FuelType,
                r.EuroClass,
                r.TransportInfo_TransportDistance,
                r.TransportInfo_TransportCarbonEmissions,
                r.EmissionFactorVersion))
            .ToList();

        // ── 3. EntryCACs ──────────────────────────────────────────────────────
        var entryCACsQuery = _context.EntryCACs
            .AsNoTracking()
            .Include(c => c.EntryCACResidues)
                .ThenInclude(r => r.Residue)
            .Where(c => c.IdWasteMove == wm.Id
                     && (ownerId == Guid.Empty || c.OwnerId == ownerId));

        var entryCACs = (await entryCACsQuery.ToListAsync(ct))
            .Select(c => new EntryCACDetailDto(
                c.Id,
                c.IdWasteMove,
                c.WasteMoveReference,
                c.OwnerId,
                c.CACEntryDate,
                c.TypeContainer,
                c.PriceContainer,
                c.CollectionMethod,
                c.IdUser,
                c.DateCreateSys,
                c.DateModifiedSys,
                c.EntryCACResidues
                    .Select(r => new EntryCACResidueDto(
                        r.Id, r.IdEntryCAC, r.IdResidue,
                        r.Residue?.Name, r.Weight, r.MeasureUnit,
                        r.Units, r.PriceWeight, r.PriceUnit))
                    .ToList()))
            .ToList();

        // ── 4. EntryPlants ────────────────────────────────────────────────────
        var entryPlantsQuery = _context.EntryPlants
            .AsNoTracking()
            .Include(p => p.EntryPlantResidues)
                .ThenInclude(r => r.Residue)
            .Include(p => p.WasteMove)
            .Where(p => p.IdWasteMove == wm.Id
                     && (ownerId == Guid.Empty || p.OwnerId == ownerId));

        var entryPlants = (await entryPlantsQuery.ToListAsync(ct))
            .Select(p =>
            {
                var wmStatus = p.WasteMove?.ServiceStatus;
                var discrepancy = false;
                if (p.NetWeight.HasValue)
                {
                    var estSum = wm.WasteMoveResidues.Sum(r => r.Weight ?? 0m);
                    discrepancy = estSum > 0
                        && Math.Abs((p.NetWeight.Value - estSum) / estSum) > 0.05m;
                }

                return new EntryPlantDetailDto(
                    p.Id,
                    p.IdWasteMove,
                    p.WasteMoveReference,
                    wmStatus,
                    null,
                    p.OwnerId,
                    p.TicketScale,
                    p.WeighbridgeId,
                    p.PlantEntryDate,
                    p.GrossWeight,
                    p.TareWeight,
                    p.NetWeight,
                    p.TypeContainer,
                    p.PriceContainer,
                    p.ServiceOrderId,
                    p.IdUser,
                    p.DateCreateSys,
                    p.DateModifiedSys,
                    discrepancy,
                    p.EntryPlantResidues
                        .Select(r => new EntryPlantResidueDto(
                            r.Id, r.IdEntryPlant, r.IdResidue,
                            r.Residue?.Name, r.Weight, r.MeasureUnit,
                            r.Units, r.PriceWeight, r.PriceUnit))
                        .ToList());
            })
            .ToList();

        // ── 5. TreatmentPlants ────────────────────────────────────────────────
        var treatmentPlantsQuery = _context.TreatmentPlants
            .AsNoTracking()
            .Include(t => t.TreatmentOperation)
            .Include(t => t.TreatmentPlantResidues)
                .ThenInclude(r => r.Residue)
            .Include(t => t.TreatmentPlantResidues)
                .ThenInclude(r => r.ResidueReused)
            .Include(t => t.TreatmentPlantResidues)
                .ThenInclude(r => r.ResidueValued)
            .Include(t => t.TreatmentPlantResidues)
                .ThenInclude(r => r.ResidueRemove)
            .Where(t => t.IdWasteMove == wm.Id
                     && (ownerId == Guid.Empty || t.OwnerId == ownerId));

        var treatmentPlants = (await treatmentPlantsQuery.ToListAsync(ct))
            .Select(t =>
            {
                var totalIn      = t.TreatmentPlantResidues.Sum(r => r.WeightTotal   ?? 0m);
                var totalReused  = t.TreatmentPlantResidues.Sum(r => r.WeightReused  ?? 0m);
                var totalValued  = t.TreatmentPlantResidues.Sum(r => r.WeightValued  ?? 0m);
                var totalRemove  = t.TreatmentPlantResidues.Sum(r => r.WeightRemove  ?? 0m);
                var recycleRate  = totalIn > 0 ? totalValued  / totalIn * 100m : 0m;
                var valorRate    = totalIn > 0 ? (totalReused + totalValued) / totalIn * 100m : 0m;
                var rejectRate   = totalIn > 0 ? totalRemove  / totalIn * 100m : 0m;

                return new TreatmentPlantDetailDto(
                    t.Id,
                    t.IdWasteMove,
                    t.WasteMoveReference,
                    null,
                    t.OwnerId,
                    t.TicketScale,
                    t.PlantTreatmentDate,
                    t.IdTreatmentOperation,
                    t.TreatmentOperation?.Code,
                    t.TreatmentOperation?.Description,
                    t.TreatmentOperation?.OperationType,
                    t.TreatmentOperation?.IsRecycling ?? false,
                    t.TreatmentOperation?.IsEnergyRecovery ?? false,
                    t.TreatmentOperation?.IsPreparationForReuse ?? false,
                    t.ImproperWeight,
                    t.QualityMetricsJson,
                    t.TypeContainer,
                    t.PriceContainer,
                    t.ServiceOrderId,
                    t.IncidentId,
                    t.IdUser,
                    t.DateCreateSys,
                    t.DateModifiedSys,
                    totalIn, totalReused, totalValued, totalRemove,
                    Math.Round(recycleRate, 2),
                    Math.Round(valorRate,   2),
                    Math.Round(rejectRate,  2),
                    t.TreatmentPlantResidues
                        .Select(r => new TreatmentPlantResidueDto(
                            r.Id, r.IdTreatmentPlant,
                            r.IdResidue,         r.Residue?.Name,      r.Category,
                            r.WeightTotal,       r.MeasureUnit,        r.Units,
                            r.PriceWeight,       r.PriceUnit,
                            r.IdResidueReused,   r.ResidueReused?.Name,
                            r.WeightReused,      r.MeasureUnitReused,  r.UnitsReused,
                            r.IdResidueValued,   r.ResidueValued?.Name,
                            r.WeightValued,      r.MeasureUnitValued,  r.UnitsValued,
                            r.IdResidueRemove,   r.ResidueRemove?.Name,
                            r.WeightRemove,      r.MeasureUnitRemove,  r.UnitsRemove,
                            (r.WeightReused ?? 0m) + (r.WeightValued ?? 0m) + (r.WeightRemove ?? 0m),
                            Math.Abs((r.WeightTotal ?? 0m)
                                   - (r.WeightReused ?? 0m)
                                   - (r.WeightValued ?? 0m)
                                   - (r.WeightRemove ?? 0m)),
                            Math.Abs((r.WeightTotal ?? 0m)
                                   - (r.WeightReused ?? 0m)
                                   - (r.WeightValued ?? 0m)
                                   - (r.WeightRemove ?? 0m)) <= (r.WeightTotal ?? 0m) * 0.01m))
                        .ToList());
            })
            .ToList();

        // ── 6. SettlementLines por WasteMoveReference ─────────────────────────
        List<TimelineSettlementLineDto> settlementLines = [];
        if (!string.IsNullOrEmpty(wmRef) && profile is "ADMIN" or "SCRAP" or "PUBLIC_ENT")
        {
            var lines = await _context.SettlementLines
                .AsNoTracking()
                .Include(l => l.Settlement)
                .Include(l => l.LerCode)
                .Where(l => l.Settlement.OwnerId == ownerId || ownerId == Guid.Empty)
                .ToListAsync(ct);

            settlementLines = lines
                .Where(l => l.SourceIdsJson != null
                         && l.SourceIdsJson.Contains(wm.Id.ToString()))
                .Select(l => new TimelineSettlementLineDto(
                    l.Id,
                    l.SettlementId,
                    l.Settlement.SettlementNumber,
                    l.Settlement.Status,
                    l.ProductCategory,
                    l.LerCode?.Code,
                    l.WeightKg,
                    l.PricePerKg,
                    l.Amount,
                    l.EvidenceType))
                .ToList();
        }

        // ── 7. Incidents ──────────────────────────────────────────────────────
        var incidents = await _context.Incidents
            .AsNoTracking()
            .Where(i => (ownerId == Guid.Empty || i.OwnerId == ownerId)
                     && (i.WasteMoveReference == wmRef
                      || i.ServiceOrderId == wm.ServiceOrderId))
            .OrderByDescending(i => i.OpenedAt)
            .Select(i => new TimelineIncidentDto(
                i.Id,
                i.Type,
                i.Severity,
                i.OpenedAt,
                i.ClosedAt,
                i.ClosedAt == null,
                i.ReportedByName,
                i.Description))
            .ToListAsync(ct);

        // ── 8. KPIs agregados ─────────────────────────────────────────────────
        var totalCO2     = residueLines.Sum(r => r.TransportCarbonEmissions ?? 0m);
        var aggWeightIn  = treatmentPlants.Sum(t => t.TotalWeightIn);
        var aggReused    = treatmentPlants.Sum(t => t.TotalWeightReused);
        var aggValued    = treatmentPlants.Sum(t => t.TotalWeightValued);
        var aggRemove    = treatmentPlants.Sum(t => t.TotalWeightRemove);

        // ── 9. StatusDates para el stepper ────────────────────────────────────
        var statusDates = new Dictionary<string, DateTime?>
        {
            [WasteMoveStatuses.Solicitado]  = wm.RequestDate,
            [WasteMoveStatuses.Planificado] = wm.PlannedPickupStart,
            [WasteMoveStatuses.Recogido]    = wm.ActualPickupStart,
            [WasteMoveStatuses.EnCAC]       = entryCACs.FirstOrDefault()?.CACEntryDate,
            [WasteMoveStatuses.EnPlanta]    = entryPlants.FirstOrDefault()?.PlantEntryDate,
            [WasteMoveStatuses.Clasificado] = treatmentPlants.FirstOrDefault()?.PlantTreatmentDate
        };

        // ── 10. SO snapshot ───────────────────────────────────────────────────
        TimelineServiceOrderDto? soDto = null;
        if (wm.ServiceOrder is { } so)
        {
            soDto = new TimelineServiceOrderDto(
                so.Id,
                so.ServiceOrderNumber,
                so.IssuedAt,
                so.Status,
                so.Priority,
                so.IssuedByName,
                so.IssuedByNationalId,
                so.IssuedByCenterCode,
                so.WasteStream,
                so.LerCode?.Code,
                so.LerCode?.Description,
                so.LerCode?.IsDangerous ?? false,
                so.EstimatedWeight,
                so.VehicleRegistration,
                so.VehicleType,
                so.FuelType,
                so.EuroClass,
                so.TransportDistanceKm);
        }

        return new WasteMoveTimelineDto(
            wm.Id,
            wm.WasteMoveReference,
            wm.ServiceStatus,
            wm.OwnerId,
            wm.Source?.Name,
            wm.Source?.Latitude,
            wm.Source?.Longitude,
            wm.Destination?.Name,
            wm.Destination?.Latitude,
            wm.Destination?.Longitude,
            wm.Scrap?.Name,
            wm.OperatorTransfer?.Name,
            wm.RequestDate,
            wm.PlannedPickupStart,
            wm.ActualPickupStart,
            wm.GatheredDate,
            wm.PlantEntryDate,
            wm.DocumentId,
            wm.DocumentHash,
            wm.SignatureStatus,
            soDto,
            residueLines,
            entryCACs,
            entryPlants,
            treatmentPlants,
            settlementLines,
            incidents,
            totalCO2,
            aggWeightIn,
            aggReused,
            aggValued,
            aggRemove,
            statusDates);
    }

    // Auxiliar: cuando el perfil es CARRIER el OwnerId puede coincidir con la entidad transportista.
    // Se usa para el filtro de líneas visibles por el transportista.
    private static Guid GetCarrierEntityId(Guid ownerId) => ownerId;
}
