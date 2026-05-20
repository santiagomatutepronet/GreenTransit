using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Reporting.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.Reporting.Queries;

/// <summary>
/// Calcula los KPIs regulatorios para un periodo (año + trimestre opcional).
/// Filtros opcionales: IdScrap, AutonomousCommunity (solo en MarketShares), Category.
/// El filtro de AutonomousCommunity se aplica únicamente a la sección MarketShareCompliance
/// ya que las tablas de tratamiento no tienen campo CCAA directo.
/// Acceso: ADMIN, SCRAP, PUBLIC_ENT.
/// </summary>
public sealed record GetRegulatoryKpisQuery(
    int    Year,
    int?   Quarter,
    Guid?  IdScrap,
    string? AutonomousCommunity,
    string? Category
) : IRequest<RegulatoryKpisDto>;

public sealed class GetRegulatoryKpisQueryHandler
    : IRequestHandler<GetRegulatoryKpisQuery, RegulatoryKpisDto>
{
    private readonly IApplicationDbContext    _db;
    private readonly ICurrentUserService      _currentUser;
    private readonly IRegulatoryTargetDefaults _defaults;

    public GetRegulatoryKpisQueryHandler(
        IApplicationDbContext     db,
        ICurrentUserService       currentUser,
        IRegulatoryTargetDefaults defaults)
    {
        _db          = db;
        _currentUser = currentUser;
        _defaults    = defaults;
    }

    public async Task<RegulatoryKpisDto> Handle(
        GetRegulatoryKpisQuery request, CancellationToken ct)
    {
        var ownerId = _currentUser.OwnerId;
        var year    = request.Year;

        var yearStart = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var yearEnd   = yearStart.AddYears(1);

        // ── 1. Cargar datos de tratamiento del año completo ───────────────────
        // Se proyectan solo los campos necesarios para los KPIs.
        var tprYear = await _db.TreatmentPlantResidues
            .AsNoTracking()
            .Where(r =>
                r.TreatmentPlant != null &&
                r.TreatmentPlant.PlantTreatmentDate >= yearStart &&
                r.TreatmentPlant.PlantTreatmentDate < yearEnd &&
                (ownerId == Guid.Empty || r.TreatmentPlant.OwnerId == ownerId) &&
                (request.IdScrap == null || r.TreatmentPlant.WasteMove!.IdScrap == request.IdScrap) &&
                (request.Category == null || r.Category == request.Category))
            .Select(r => new TprRow(
                r.WeightTotal ?? 0m,
                r.WeightValued ?? 0m,
                r.WeightReused ?? 0m,
                r.Category,
                r.TreatmentPlant.PlantTreatmentDate,
                r.TreatmentPlant.TreatmentOperation != null && r.TreatmentPlant.TreatmentOperation.IsRecycling,
                r.TreatmentPlant.TreatmentOperation != null && r.TreatmentPlant.TreatmentOperation.IsPreparationForReuse,
                r.TreatmentPlant.IdWasteMove
            ))
            .ToListAsync(ct);

        // ── 2. CO2 y estado CLASIFICADO: subqueries IQueryable — evita IN (lista larga de GUIDs) ──
        // wmIdsYearQuery es un IQueryable<Guid> que EF traduce a subquery SQL, sin materializar.
        var wmIdsYearQuery = _db.TreatmentPlants
            .Where(tp =>
                tp.PlantTreatmentDate >= yearStart &&
                tp.PlantTreatmentDate < yearEnd &&
                (ownerId == Guid.Empty || tp.OwnerId == ownerId) &&
                (request.IdScrap == null || (tp.WasteMove != null && tp.WasteMove.IdScrap == request.IdScrap)) &&
                tp.IdWasteMove != null)
            .Select(tp => tp.IdWasteMove!.Value)
            .Distinct();

        var co2ByWm = await _db.WasteMoveResidues
            .AsNoTracking()
            .Where(r => wmIdsYearQuery.Contains(r.IdWasteMove))
            .GroupBy(r => r.IdWasteMove)
            .Select(g => new { WmId = g.Key, Co2 = g.Sum(r => r.TransportInfo_TransportCarbonEmissions ?? 0m) })
            .ToListAsync(ct);

        var co2MapYear = co2ByWm.ToDictionary(x => x.WmId, x => x.Co2);

        var classifiedWmIds = await _db.WasteMoves
            .AsNoTracking()
            .Where(w => w.ServiceStatus == "CLASIFICADO" && wmIdsYearQuery.Contains(w.Id))
            .Select(w => w.Id)
            .ToListAsync(ct);

        var classifiedSet = classifiedWmIds.ToHashSet();

        // ── 3. Objetivos normativos ────────────────────────────────────────────
        var (minRecycling, minReuse) = await GetTargetsAsync(ownerId, year, request.Category, ct);

        // ── 4. Calcular KPIs del periodo principal ────────────────────────────
        var periodRows = FilterByQuarter(tprYear, request.Quarter);

        var mainKpis = ComputeKpis(periodRows, co2MapYear, classifiedSet);

        // ── 5. ByQuarter (4 trimestres del año) ───────────────────────────────
        var byQuarter = Enumerable.Range(1, 4)
            .Select(q =>
            {
                var qRows = FilterByQuarter(tprYear, q);
                var (rec, reu, co2, weight, transports) = ComputeKpis(qRows, co2MapYear, classifiedSet);
                return new QuarterlyKpiDto(q, rec, reu, co2, weight, transports);
            })
            .ToList();

        // ── 6. ByCategory ─────────────────────────────────────────────────────
        var byCategory = periodRows
            .GroupBy(r => r.Category ?? "Sin categoría")
            .Select(g =>
            {
                var total   = g.Sum(r => r.WeightTotal);
                var valued  = g.Where(r => r.IsRecycling).Sum(r => r.WeightValued);
                var reused  = g.Where(r => r.IsPreparationForReuse).Sum(r => r.WeightReused);
                return new CategoryKpiDto(
                    g.Key,
                    total,
                    total > 0 ? Math.Round((double)(valued / total) * 100, 2) : 0,
                    total > 0 ? Math.Round((double)(reused / total) * 100, 2) : 0);
            })
            .OrderByDescending(c => c.TotalWeightKg)
            .ToList();

        // ── 7. MarketShare compliance ─────────────────────────────────────────
        var compliance = await GetMarketShareComplianceAsync(
            ownerId, year, request.Quarter, request.IdScrap,
            request.AutonomousCommunity, request.Category, ct);

        var (recRate, reuRate, co2Int, totalKg, totalTransp) = mainKpis;

        return new RegulatoryKpisDto(
            year, request.Quarter,
            recRate, reuRate, co2Int, totalKg, totalTransp,
            minRecycling, minReuse,
            byCategory, compliance, byQuarter);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static List<TprRow> FilterByQuarter(List<TprRow> rows, int? quarter)
    {
        if (quarter is null) return rows;
        var (start, end) = QuarterRange(quarter.Value);
        return rows.Where(r => r.Date >= start && r.Date < end).ToList();
    }

    private static (DateTime Start, DateTime End) QuarterRange(int q)
    {
        var startMonth = (q - 1) * 3 + 1;
        var start      = new DateTime(1, startMonth, 1, 0, 0, 0, DateTimeKind.Utc);
        return (start, start.AddMonths(3));
    }

    private static (double Rec, double Reu, double Co2, decimal Weight, int Transports)
        ComputeKpis(
            List<TprRow>         rows,
            Dictionary<Guid, decimal> co2Map,
            HashSet<Guid>        classifiedSet)
    {
        var totalWeight  = rows.Sum(r => r.WeightTotal);
        var valuedRec    = rows.Where(r => r.IsRecycling).Sum(r => r.WeightValued);
        var reusedPrep   = rows.Where(r => r.IsPreparationForReuse).Sum(r => r.WeightReused);

        var wmIds      = rows.Where(r => r.WasteMoveId.HasValue).Select(r => r.WasteMoveId!.Value).Distinct().ToHashSet();
        var totalCo2   = wmIds.Where(co2Map.ContainsKey).Sum(id => co2Map[id]);
        var transports = wmIds.Count(id => classifiedSet.Contains(id));

        double recRate = totalWeight > 0 ? Math.Round((double)(valuedRec / totalWeight) * 100, 2) : 0;
        double reuRate = totalWeight > 0 ? Math.Round((double)(reusedPrep / totalWeight) * 100, 2) : 0;
        double co2Int  = totalWeight > 0 ? Math.Round((double)totalCo2 / ((double)totalWeight / 1000), 2) : 0;

        return (recRate, reuRate, co2Int, totalWeight, transports);
    }

    private async Task<(double MinRecycling, double MinReuse)> GetTargetsAsync(
        Guid ownerId, int year, string? category, CancellationToken ct)
    {
        var target = await _db.RegulatoryTargets
            .AsNoTracking()
            .Where(t => (ownerId == Guid.Empty || t.OwnerId == ownerId)
                     && t.Year == year
                     && (category == null ? t.Category == null : t.Category == category))
            .OrderBy(t => t.Category == null ? 1 : 0) // exacta primero; genérica como fallback
            .FirstOrDefaultAsync(ct);

        if (target is not null)
            return (target.MinRecyclingPercent, target.MinReusePercent);

        return (_defaults.DefaultMinRecyclingPercent, _defaults.DefaultMinReusePercent);
    }

    private async Task<IReadOnlyList<MarketShareComplianceKpiDto>> GetMarketShareComplianceAsync(
        Guid ownerId, int year, int? quarter, Guid? idScrap,
        string? autonomousCommunity, string? category, CancellationToken ct)
    {
        var msQuery = _db.MarketShares
            .AsNoTracking()
            .Where(m =>
                (ownerId == Guid.Empty || m.OwnerId == ownerId) &&
                m.Year == year &&
                (idScrap == null || m.IdScrap == idScrap) &&
                (autonomousCommunity == null || m.AutonomousCommunity == autonomousCommunity) &&
                (category == null || m.Category == category));

        var targets = await msQuery
            .Select(m => new { m.Category, m.AutonomousCommunity, m.Weight })
            .ToListAsync(ct);

        if (!targets.Any()) return [];

        // Calcular kg reales desde TreatmentPlantResidues del año
        var yearStart = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var yearEnd   = yearStart.AddYears(1);

        var (periodStart, periodEnd) = quarter.HasValue
            ? (new DateTime(year, (quarter.Value - 1) * 3 + 1, 1, 0, 0, 0, DateTimeKind.Utc),
               new DateTime(year, (quarter.Value - 1) * 3 + 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(3))
            : (yearStart, yearEnd);

        var actualByCategory = await _db.TreatmentPlantResidues
            .AsNoTracking()
            .Where(r =>
                r.TreatmentPlant != null &&
                r.TreatmentPlant.PlantTreatmentDate >= periodStart &&
                r.TreatmentPlant.PlantTreatmentDate < periodEnd &&
                (ownerId == Guid.Empty || r.TreatmentPlant.OwnerId == ownerId) &&
                (idScrap == null || r.TreatmentPlant.WasteMove!.IdScrap == idScrap) &&
                (category == null || r.Category == category))
            .GroupBy(r => r.Category ?? "Sin categoría")
            .Select(g => new { Category = g.Key, Weight = g.Sum(r => r.WeightTotal ?? 0m) })
            .ToListAsync(ct);

        var actualMap = actualByCategory.ToDictionary(x => x.Category, x => x.Weight);

        return targets
            .GroupBy(t => (t.Category, t.AutonomousCommunity))
            .Select(g =>
            {
                var targetKg = g.Sum(t => t.Weight);
                var actualKg = actualMap.TryGetValue(g.Key.Category, out var a) ? a : 0m;
                var pct      = targetKg > 0 ? Math.Round((double)(actualKg / targetKg) * 100, 2) : 0;
                return new MarketShareComplianceKpiDto(g.Key.Category, g.Key.AutonomousCommunity, targetKg, actualKg, pct);
            })
            .OrderBy(c => c.Category)
            .ToList();
    }

    // ── Projection record (internal) ─────────────────────────────────────────
    private sealed record TprRow(
        decimal  WeightTotal,
        decimal  WeightValued,
        decimal  WeightReused,
        string?  Category,
        DateTime? Date,
        bool     IsRecycling,
        bool     IsPreparationForReuse,
        Guid?    WasteMoveId
    );
}
