using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Dashboard.DTOs;
using MediatR;

namespace GreenTransit.Application.Features.Dashboard.Queries;

/// <summary>
/// Devuelve el resumen operativo completo para el dashboard.
/// Filtra todo por OwnerId del usuario activo. Adapta los datos según UserProfile:
///   CARRIER    → WasteMovesByStatus solo traslados con transporte asignado; UpcomingPickups solo SOs con IdCarrier.
///   PLANT_OP   → KPIs de tratamiento solo de su OwnerId (estándar multi-tenant).
///   ADMIN/SCRAP → todos los datos del OwnerId.
/// Las consultas se ejecutan de forma secuencial sobre el mismo DbContext (EF Core no admite
/// concurrencia sobre una sola instancia de contexto).
/// </summary>
public sealed record GetDashboardSummaryQuery : IRequest<DashboardSummaryDto>;

public sealed class GetDashboardSummaryQueryHandler
    : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    private static readonly string[] CollectedStatuses =
        ["RECOGIDO", "EN CAC", "EN PLANTA", "CLASIFICADO"];

    public GetDashboardSummaryQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<DashboardSummaryDto> Handle(
        GetDashboardSummaryQuery request, CancellationToken ct)
    {
        var now              = DateTime.UtcNow;
        var firstOfMonth     = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var firstOfNextMonth = firstOfMonth.AddMonths(1);
        var firstOfPrevMonth = firstOfMonth.AddMonths(-1);
        var in7Days          = now.AddDays(7);
        var first6MonthsAgo  = firstOfMonth.AddMonths(-5);
        var yearStart        = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var yearEnd          = yearStart.AddYears(1);

        var ownerId        = _currentUser.OwnerId;
        var linkedEntityId = _currentUser.LinkedEntityId;

        // ── Helpers para filtro de tenant ─────────────────────────────────────
        bool HasOwner(Guid? entityOwnerId) => ownerId == Guid.Empty || entityOwnerId == ownerId;

        // ── Preparar IDs de SCRAPs para perfil COORDINATOR ────────────────────
        // IQueryable — EF lo traduce a subquery SQL, sin materializar lista en C#
        IQueryable<Guid>? coordinatorScrapQuery = null;
        if (_currentUser.IsInProfile(GreenTransit.Domain.Authorization.ProfileConstants.Coordinator) && linkedEntityId.HasValue)
        {
            coordinatorScrapQuery = _context.Agreements
                .Where(a => a.IdCoordinator == linkedEntityId.Value && (ownerId == Guid.Empty || a.OwnerId == ownerId) && a.IdScrap != null)
                .Select(a => a.IdScrap!.Value)
                .Distinct();
        }

        // ── 1. WasteMoves by status ───────────────────────────────────────────
        var wmQuery = _context.WasteMoves
            .AsNoTracking()
            .Where(wm => ownerId == Guid.Empty || wm.OwnerId == ownerId);

        if (_currentUser.IsInProfile(GreenTransit.Domain.Authorization.ProfileConstants.Carrier))
            wmQuery = wmQuery.Where(wm =>
                wm.WasteMoveResidues.Any(r => r.IdCarrier == linkedEntityId));
        else if (_currentUser.IsInProfile(GreenTransit.Domain.Authorization.ProfileConstants.Producer) && linkedEntityId.HasValue)
            wmQuery = wmQuery.Where(wm =>
                wm.ServiceOrderId != null &&
                wm.ServiceOrder != null &&
                wm.ServiceOrder.IdIssuedBy == linkedEntityId.Value);
        else if (_currentUser.IsInProfile(GreenTransit.Domain.Authorization.ProfileConstants.Scrap) && linkedEntityId.HasValue)
            wmQuery = wmQuery.Where(wm =>
                wm.IdScrap == linkedEntityId.Value ||
                wm.IdScrap2 == linkedEntityId.Value);
        else if (_currentUser.IsInProfile(GreenTransit.Domain.Authorization.ProfileConstants.PublicEnt) && linkedEntityId.HasValue)
        {
            var soIds = _context.ServiceOrders
                .Where(so => so.IdIssuedBy == linkedEntityId.Value && (ownerId == Guid.Empty || so.OwnerId == ownerId))
                .Select(so => so.Id);
            wmQuery = wmQuery.Where(wm => wm.ServiceOrderId != null && soIds.Contains(wm.ServiceOrderId.Value));
        }
        else if (_currentUser.IsInProfile(GreenTransit.Domain.Authorization.ProfileConstants.PlantOp) && linkedEntityId.HasValue)
            wmQuery = wmQuery.Where(wm => wm.IdDestination == linkedEntityId.Value);
        else if (_currentUser.IsInProfile(GreenTransit.Domain.Authorization.ProfileConstants.CacOp) && linkedEntityId.HasValue)
            wmQuery = wmQuery.Where(wm =>
                wm.IdDestination == linkedEntityId.Value ||
                wm.IdSource == linkedEntityId.Value);
        else if (coordinatorScrapQuery != null)
            wmQuery = wmQuery.Where(wm => wm.IdScrap.HasValue && coordinatorScrapQuery.Contains(wm.IdScrap.Value));

        var wasteMovesByStatus = (await wmQuery
            .GroupBy(wm => wm.ServiceStatus ?? "DESCONOCIDO")
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct))
            .ToDictionary(x => x.Status, x => x.Count);

        // ── 2. Kg recogidos este mes ──────────────────────────────────────────
        var wmrBase = _context.WasteMoveResidues
            .AsNoTracking()
            .Where(r => ownerId == Guid.Empty || r.WasteMove.OwnerId == ownerId);

        if (_currentUser.IsInProfile(GreenTransit.Domain.Authorization.ProfileConstants.Carrier) && linkedEntityId.HasValue)
            wmrBase = wmrBase.Where(r => r.IdCarrier == linkedEntityId.Value);
        else if (_currentUser.IsInProfile(GreenTransit.Domain.Authorization.ProfileConstants.Producer) && linkedEntityId.HasValue)
            wmrBase = wmrBase.Where(r =>
                r.WasteMove.ServiceOrderId != null &&
                r.WasteMove.ServiceOrder != null &&
                r.WasteMove.ServiceOrder.IdIssuedBy == linkedEntityId.Value);
        else if (_currentUser.IsInProfile(GreenTransit.Domain.Authorization.ProfileConstants.Scrap) && linkedEntityId.HasValue)
            wmrBase = wmrBase.Where(r =>
                r.WasteMove.IdScrap == linkedEntityId.Value ||
                r.WasteMove.IdScrap2 == linkedEntityId.Value);
        else if (_currentUser.IsInProfile(GreenTransit.Domain.Authorization.ProfileConstants.PublicEnt) && linkedEntityId.HasValue)
        {
            var soIds = _context.ServiceOrders
                .Where(so => so.IdIssuedBy == linkedEntityId.Value && (ownerId == Guid.Empty || so.OwnerId == ownerId))
                .Select(so => so.Id);
            wmrBase = wmrBase.Where(r =>
                r.WasteMove.ServiceOrderId != null && soIds.Contains(r.WasteMove.ServiceOrderId.Value));
        }
        else if (_currentUser.IsInProfile(GreenTransit.Domain.Authorization.ProfileConstants.PlantOp) && linkedEntityId.HasValue)
            wmrBase = wmrBase.Where(r => r.WasteMove.IdDestination == linkedEntityId.Value);
        else if (_currentUser.IsInProfile(GreenTransit.Domain.Authorization.ProfileConstants.CacOp) && linkedEntityId.HasValue)
            wmrBase = wmrBase.Where(r =>
                r.WasteMove.IdDestination == linkedEntityId.Value ||
                r.WasteMove.IdSource == linkedEntityId.Value);
        else if (coordinatorScrapQuery != null)
            wmrBase = wmrBase.Where(r => r.WasteMove.IdScrap.HasValue && coordinatorScrapQuery.Contains(r.WasteMove.IdScrap.Value));

        // ── Base de residuos de tratamiento (secciones 3, 4 y 9) ─────────────
        var tpResiduesBase = _context.TreatmentPlantResidues
            .AsNoTracking()
            .Where(r => ownerId == Guid.Empty || r.TreatmentPlant.OwnerId == ownerId);

        if (_currentUser.IsInProfile(GreenTransit.Domain.Authorization.ProfileConstants.Producer) && linkedEntityId.HasValue)
            tpResiduesBase = tpResiduesBase.Where(r =>
                r.TreatmentPlant.WasteMove != null &&
                r.TreatmentPlant.WasteMove.ServiceOrder != null &&
                r.TreatmentPlant.WasteMove.ServiceOrder.IdIssuedBy == linkedEntityId.Value);
        else if (_currentUser.IsInProfile(GreenTransit.Domain.Authorization.ProfileConstants.Scrap) && linkedEntityId.HasValue)
            tpResiduesBase = tpResiduesBase.Where(r =>
                r.TreatmentPlant.WasteMove != null &&
                (r.TreatmentPlant.WasteMove.IdScrap == linkedEntityId.Value ||
                 r.TreatmentPlant.WasteMove.IdScrap2 == linkedEntityId.Value));
        else if (_currentUser.IsInProfile(GreenTransit.Domain.Authorization.ProfileConstants.PublicEnt) && linkedEntityId.HasValue)
        {
            var soIds = _context.ServiceOrders
                .Where(so => so.IdIssuedBy == linkedEntityId.Value && (ownerId == Guid.Empty || so.OwnerId == ownerId))
                .Select(so => so.Id);
            tpResiduesBase = tpResiduesBase.Where(r =>
                r.TreatmentPlant.WasteMove != null &&
                r.TreatmentPlant.WasteMove.ServiceOrderId != null &&
                soIds.Contains(r.TreatmentPlant.WasteMove.ServiceOrderId.Value));
        }
        else if (_currentUser.IsInProfile(GreenTransit.Domain.Authorization.ProfileConstants.PlantOp) && linkedEntityId.HasValue)
            tpResiduesBase = tpResiduesBase.Where(r =>
                r.TreatmentPlant.WasteMove != null &&
                r.TreatmentPlant.WasteMove.IdDestination == linkedEntityId.Value);
        else if (coordinatorScrapQuery != null)
            tpResiduesBase = tpResiduesBase.Where(r =>
                r.TreatmentPlant.WasteMove != null &&
                r.TreatmentPlant.WasteMove.IdScrap.HasValue &&
                coordinatorScrapQuery.Contains(r.TreatmentPlant.WasteMove.IdScrap.Value));

        var kgCollectedThisMonth = await wmrBase
            .Where(r =>
                CollectedStatuses.Contains(r.WasteMove.ServiceStatus) &&
                r.WasteMove.ActualPickupStart >= firstOfMonth &&
                r.WasteMove.ActualPickupStart < firstOfNextMonth)
            .SumAsync(r => r.Weight ?? 0m, ct);

        // ── 3. Kg tratados este mes ───────────────────────────────────────────
        var kgTreatedThisMonth = await tpResiduesBase
            .Where(r =>
                r.TreatmentPlant.PlantTreatmentDate >= firstOfMonth &&
                r.TreatmentPlant.PlantTreatmentDate < firstOfNextMonth)
            .SumAsync(r => r.WeightTotal ?? 0m, ct);

        // ── 4. Tasas de tratamiento (mes en curso) ────────────────────────────
        var treatmentData = await tpResiduesBase
            .Where(r =>
                r.TreatmentPlant.PlantTreatmentDate >= firstOfMonth &&
                r.TreatmentPlant.PlantTreatmentDate < firstOfNextMonth &&
                r.TreatmentPlant.TreatmentOperation != null)
            .Select(r => new
            {
                r.WeightTotal,
                r.WeightValued,
                r.WeightReused,
                r.TreatmentPlant.TreatmentOperation!.IsRecycling,
                r.TreatmentPlant.TreatmentOperation!.IsEnergyRecovery,
                r.TreatmentPlant.TreatmentOperation!.IsPreparationForReuse
            })
            .ToListAsync(ct);

        var totalWeight          = treatmentData.Sum(d => d.WeightTotal    ?? 0m);
        var recycledWeight       = treatmentData.Where(d => d.IsRecycling)           .Sum(d => d.WeightValued ?? 0m);
        var energyWeight         = treatmentData.Where(d => d.IsEnergyRecovery)      .Sum(d => d.WeightValued ?? 0m);
        var reusedWeight         = treatmentData.Where(d => d.IsPreparationForReuse) .Sum(d => d.WeightReused ?? 0m);

        double SafeRate(decimal numerator) =>
            totalWeight > 0 ? Math.Round((double)(numerator / totalWeight) * 100, 2) : 0d;

        var recyclingRate  = SafeRate(recycledWeight);
        var energyRate     = SafeRate(energyWeight);
        var reuseRate      = SafeRate(reusedWeight);

        // ── 5. Huella CO₂ mes actual y mes anterior ───────────────────────────
        var totalCo2ThisMonth = await wmrBase
            .Where(r =>
                r.WasteMove.ActualPickupStart >= firstOfMonth &&
                r.WasteMove.ActualPickupStart < firstOfNextMonth)
            .SumAsync(r => r.TransportInfo_TransportCarbonEmissions ?? 0m, ct);

        var co2PrevMonth = await wmrBase
            .Where(r =>
                r.WasteMove.ActualPickupStart >= firstOfPrevMonth &&
                r.WasteMove.ActualPickupStart < firstOfMonth)
            .SumAsync(r => r.TransportInfo_TransportCarbonEmissions ?? 0m, ct);

        // ── 6. Incidencias abiertas por severidad ─────────────────────────────
        var incidentBase = _context.Incidents
            .AsNoTracking()
            .Where(i =>
                (ownerId == Guid.Empty || i.OwnerId == ownerId) &&
                i.ClosedAt == null);

        // PRODUCER: solo incidencias de sus SOs
        if (_currentUser.IsInProfile(GreenTransit.Domain.Authorization.ProfileConstants.Producer) && linkedEntityId.HasValue)
            incidentBase = incidentBase.Where(i =>
                i.ServiceOrderId != null &&
                i.ServiceOrder != null &&
                i.ServiceOrder.IdIssuedBy == linkedEntityId.Value);
        else if (_currentUser.IsInProfile(GreenTransit.Domain.Authorization.ProfileConstants.Scrap) && linkedEntityId.HasValue)
        {
            // JOIN SQL en lugar de correlated subquery
            var wmRefIds = _context.WasteMoves
                .Where(wm => wm.IdScrap == linkedEntityId.Value || wm.IdScrap2 == linkedEntityId.Value)
                .Select(wm => wm.WasteMoveReference)
                .Where(r => r != null);
            incidentBase = incidentBase.Where(i =>
                i.WasteMoveReference != null && wmRefIds.Contains(i.WasteMoveReference));
        }
        else if (_currentUser.IsInProfile(GreenTransit.Domain.Authorization.ProfileConstants.PublicEnt) && linkedEntityId.HasValue)
        {
            var soIds = _context.ServiceOrders
                .Where(so => so.IdIssuedBy == linkedEntityId.Value && (ownerId == Guid.Empty || so.OwnerId == ownerId))
                .Select(so => so.Id);
            incidentBase = incidentBase.Where(i =>
                i.ServiceOrderId != null && soIds.Contains(i.ServiceOrderId.Value));
        }
        else if (_currentUser.IsInProfile(GreenTransit.Domain.Authorization.ProfileConstants.PlantOp) && linkedEntityId.HasValue)
        {
            var wmRefIds = _context.WasteMoves
                .Where(wm => wm.IdDestination == linkedEntityId.Value)
                .Select(wm => wm.WasteMoveReference)
                .Where(r => r != null);
            incidentBase = incidentBase.Where(i =>
                i.WasteMoveReference != null && wmRefIds.Contains(i.WasteMoveReference));
        }
        else if (_currentUser.IsInProfile(GreenTransit.Domain.Authorization.ProfileConstants.CacOp) && linkedEntityId.HasValue)
        {
            var wmRefIds = _context.WasteMoves
                .Where(wm => wm.IdDestination == linkedEntityId.Value || wm.IdSource == linkedEntityId.Value)
                .Select(wm => wm.WasteMoveReference)
                .Where(r => r != null);
            incidentBase = incidentBase.Where(i =>
                i.WasteMoveReference != null && wmRefIds.Contains(i.WasteMoveReference));
        }
        else if (coordinatorScrapQuery != null)
        {
            var wmRefIds = _context.WasteMoves
                .Where(wm => wm.IdScrap.HasValue && coordinatorScrapQuery.Contains(wm.IdScrap.Value))
                .Select(wm => wm.WasteMoveReference)
                .Where(r => r != null);
            incidentBase = incidentBase.Where(i =>
                i.WasteMoveReference != null && wmRefIds.Contains(i.WasteMoveReference));
        }

        var openIncidentsBySeverity = (await incidentBase
            .GroupBy(i => i.Severity)
            .Select(g => new { Severity = g.Key, Count = g.Count() })
            .ToListAsync(ct))
            .ToDictionary(x => x.Severity, x => x.Count);

        // ── 7. Cumplimiento MarketShares (año en curso) ───────────────────────
        // Proyectar solo los campos necesarios — evita carga de entidades Scrap completas
        var marketShareTargets = await _context.MarketShares
            .AsNoTracking()
            .Where(ms =>
                (ownerId == Guid.Empty || ms.OwnerId == ownerId) &&
                ms.Year == now.Year)
            .Select(ms => new
            {
                ms.IdScrap,
                ms.Category,
                ms.AutonomousCommunity,
                ms.Weight,
                ScrapName = ms.Scrap != null ? ms.Scrap.Name : null
            })
            .ToListAsync(ct);

        // Peso real calculado directamente en BD agrupado por (IdScrap, Category)
        var actualByScrapCategory = await _context.WasteMoveResidues
            .AsNoTracking()
            .Where(wmr =>
                wmr.WasteMove != null &&
                wmr.WasteMove.ServiceStatus != GreenTransit.Domain.Constants.WasteMoveStatuses.Cancelado &&
                wmr.WasteMove.ActualPickupStart.HasValue &&
                wmr.WasteMove.ActualPickupStart!.Value >= yearStart &&
                wmr.WasteMove.ActualPickupStart!.Value < yearEnd &&
                (ownerId == Guid.Empty || wmr.WasteMove.OwnerId == ownerId) &&
                wmr.Weight.HasValue &&
                wmr.Residue != null)
            .GroupBy(wmr => new
            {
                IdScrap  = wmr.WasteMove!.IdScrap,
                Category = wmr.Residue!.ProductCategory
            })
            .Select(g => new
            {
                g.Key.IdScrap,
                g.Key.Category,
                TotalWeight = g.Sum(x => x.Weight!.Value)
            })
            .ToListAsync(ct);

        var actualMap = actualByScrapCategory
            .ToDictionary(k => (k.IdScrap, k.Category), k => k.TotalWeight);

        var compliance = marketShareTargets
            .GroupBy(ms => new { ms.IdScrap, ms.Category })
            .Select(g =>
            {
                var first  = g.First();
                var target = g.Sum(ms => ms.Weight);
                actualMap.TryGetValue((g.Key.IdScrap, g.Key.Category), out var actual);
                return new MarketShareComplianceDto(
                    g.Key.Category,
                    first.AutonomousCommunity,
                    target,
                    actual,
                    target > 0 ? Math.Round((double)(actual / target) * 100, 1) : 0d,
                    g.Key.IdScrap,
                    first.ScrapName);
            })
            .OrderBy(x => x.ScrapName)
            .ThenBy(x => x.Category)
            .ToList();

        // ── 8. Próximas recogidas (próximos 7 días) ───────────────────────────
        var soQuery = _context.ServiceOrders
            .AsNoTracking()
            .Where(so =>
                (ownerId == Guid.Empty || so.OwnerId == ownerId) &&
                so.PlannedPickupStart >= now &&
                so.PlannedPickupStart <= in7Days &&
                so.Status != "Completed" &&
                so.Status != "Cancelled");

        if (_currentUser.IsInProfile(GreenTransit.Domain.Authorization.ProfileConstants.Carrier))
            soQuery = soQuery.Where(so => so.IdCarrier != null);
        else if (_currentUser.IsInProfile(GreenTransit.Domain.Authorization.ProfileConstants.Producer) && linkedEntityId.HasValue)
            soQuery = soQuery.Where(so => so.IdIssuedBy == linkedEntityId.Value);
        else if (_currentUser.IsInProfile(GreenTransit.Domain.Authorization.ProfileConstants.PublicEnt) && linkedEntityId.HasValue)
            soQuery = soQuery.Where(so => so.IdIssuedBy == linkedEntityId.Value);
        else if (_currentUser.IsInProfile(GreenTransit.Domain.Authorization.ProfileConstants.Scrap) && linkedEntityId.HasValue)
        {
            // Subquery de SOs via ServiceOrderId en WasteMoves — evita correlated ANY
            var soIdsForScrap = _context.WasteMoves
                .Where(wm => (wm.IdScrap == linkedEntityId.Value || wm.IdScrap2 == linkedEntityId.Value)
                          && wm.ServiceOrderId != null)
                .Select(wm => wm.ServiceOrderId!.Value);
            soQuery = soQuery.Where(so => soIdsForScrap.Contains(so.Id));
        }
        else if (_currentUser.IsInProfile(GreenTransit.Domain.Authorization.ProfileConstants.PlantOp) && linkedEntityId.HasValue)
        {
            var soIdsForPlant = _context.WasteMoves
                .Where(wm => wm.IdDestination == linkedEntityId.Value && wm.ServiceOrderId != null)
                .Select(wm => wm.ServiceOrderId!.Value);
            soQuery = soQuery.Where(so => soIdsForPlant.Contains(so.Id));
        }
        else if (_currentUser.IsInProfile(GreenTransit.Domain.Authorization.ProfileConstants.CacOp) && linkedEntityId.HasValue)
        {
            var soIdsForCac = _context.WasteMoves
                .Where(wm => wm.IdDestination == linkedEntityId.Value && wm.ServiceOrderId != null)
                .Select(wm => wm.ServiceOrderId!.Value);
            soQuery = soQuery.Where(so =>
                so.IdPickupPoint == linkedEntityId.Value || soIdsForCac.Contains(so.Id));
        }
        else if (coordinatorScrapQuery != null)
        {
            var soIdsForCoord = _context.WasteMoves
                .Where(wm => wm.IdScrap.HasValue && coordinatorScrapQuery.Contains(wm.IdScrap.Value)
                          && wm.ServiceOrderId != null)
                .Select(wm => wm.ServiceOrderId!.Value);
            soQuery = soQuery.Where(so => soIdsForCoord.Contains(so.Id));
        }

        var upcomingPickups = (await soQuery
            .OrderBy(so => so.PlannedPickupStart)
            .Take(5)
            .Select(so => new
            {
                so.Id,
                so.ServiceOrderNumber,
                so.PlannedPickupStart,
                so.Priority,
                PickupPointName = so.PickupPoint != null ? so.PickupPoint.Name : (string?)null
            })
            .ToListAsync(ct))
            .Select(x => new UpcomingPickupDto(
                x.Id,
                x.ServiceOrderNumber,
                x.PickupPointName,
                x.PlannedPickupStart,
                x.Priority))
            .ToList();

        // ── 9. Últimos 6 meses: kg recogidos vs tratados ──────────────────────
        var collectedByMonth = await wmrBase
            .Where(r =>
                CollectedStatuses.Contains(r.WasteMove.ServiceStatus) &&
                r.WasteMove.ActualPickupStart >= first6MonthsAgo &&
                r.WasteMove.ActualPickupStart < firstOfNextMonth)
            .GroupBy(r => new
            {
                Year  = r.WasteMove.ActualPickupStart!.Value.Year,
                Month = r.WasteMove.ActualPickupStart!.Value.Month
            })
            .Select(g => new { g.Key.Year, g.Key.Month, Weight = g.Sum(r => r.Weight ?? 0m) })
            .ToListAsync(ct);

        var treatedByMonth = await tpResiduesBase
            .Where(r =>
                r.TreatmentPlant.PlantTreatmentDate >= first6MonthsAgo &&
                r.TreatmentPlant.PlantTreatmentDate < firstOfNextMonth)
            .GroupBy(r => new
            {
                Year  = r.TreatmentPlant.PlantTreatmentDate!.Value.Year,
                Month = r.TreatmentPlant.PlantTreatmentDate!.Value.Month
            })
            .Select(g => new { g.Key.Year, g.Key.Month, Weight = g.Sum(r => r.WeightTotal ?? 0m) })
            .ToListAsync(ct);

        var treatedDict = treatedByMonth.ToDictionary(x => (x.Year, x.Month), x => x.Weight);

        var monthlyData = Enumerable.Range(0, 6)
            .Select(i => firstOfMonth.AddMonths(-5 + i))
            .Select(d =>
            {
                var collected = collectedByMonth
                    .FirstOrDefault(x => x.Year == d.Year && x.Month == d.Month)?.Weight ?? 0m;
                var treated = treatedDict.TryGetValue((d.Year, d.Month), out var t) ? t : 0m;
                return new MonthlyKgDto(d.Year, d.Month, collected, treated);
            })
            .ToList();

        // ── 10. Puntos de entidades para el mapa ──────────────────────────────
        var entityMapPoints = (await _context.BusinessEntities
            .AsNoTracking()
            .Where(e => e.IsActive && e.Latitude != null && e.Longitude != null)
            .Select(e => new { e.Name, e.EntityRole, e.Latitude, e.Longitude })
            .Take(300)
            .ToListAsync(ct))
            .Where(e =>
                double.TryParse(e.Latitude, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out _) &&
                double.TryParse(e.Longitude, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out _))
            .Select(e => new EntityMapPointDto(
                e.Name,
                e.EntityRole,
                double.Parse(e.Latitude!, System.Globalization.CultureInfo.InvariantCulture),
                double.Parse(e.Longitude!, System.Globalization.CultureInfo.InvariantCulture)))
            .ToList();

        // ── 11. Zonas DUM activas para el mapa ───────────────────────────────
        var dumZones = (await _context.DumZones
            .AsNoTracking()
            .Where(z =>
                (ownerId == Guid.Empty || z.OwnerId == ownerId) &&
                z.Status == "Active")
            .Select(z => new { z.ZoneCode, z.Name, z.GeometryJson })
            .ToListAsync(ct))
            .Select(z => new DumZoneMapDto(z.ZoneCode, z.Name, z.GeometryJson))
            .ToList();

        return new DashboardSummaryDto(
            wasteMovesByStatus,
            kgCollectedThisMonth,
            kgTreatedThisMonth,
            recyclingRate,
            energyRate,
            reuseRate,
            totalCo2ThisMonth,
            co2PrevMonth,
            openIncidentsBySeverity,
            compliance,
            upcomingPickups,
            monthlyData,
            entityMapPoints,
            dumZones);
    }
}
