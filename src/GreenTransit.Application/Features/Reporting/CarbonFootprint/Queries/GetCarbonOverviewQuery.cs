using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Reporting.CarbonFootprint.DTOs;
using GreenTransit.Domain.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.Reporting.CarbonFootprint.Queries;

/// <summary>
/// Devuelve los datos del dashboard CO2-A — Panel de Control General de Huella de Carbono.
/// ADMIN y DISPATCH_OFFICE ven todos los datos; el resto solo los de su entidad/OwnerId.
/// Los factores de emisión se obtienen del set activo; operaciones sin factor se excluyen
/// de las sumas pero se contabilizan en ServicesWithoutFactor.
/// </summary>
public sealed record GetCarbonOverviewQuery(
    DateTime  DateFrom,
    DateTime  DateTo,
    string?   ProvinceCode     = null,
    string?   MunicipalityCode = null,
    string?   LerCode          = null,
    Guid?     IdScrap          = null,
    string?   VehicleType      = null
) : IRequest<CarbonOverviewDto>;

public sealed class GetCarbonOverviewQueryHandler
    : IRequestHandler<GetCarbonOverviewQuery, CarbonOverviewDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetCarbonOverviewQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<CarbonOverviewDto> Handle(
        GetCarbonOverviewQuery request, CancellationToken ct)
    {
        var ownerId    = _currentUser.OwnerId;
        var isAdmin    = _currentUser.IsInProfile(ProfileConstants.Admin);
        var isDispatch = _currentUser.IsInProfile(ProfileConstants.DispatchOffice);
        var seeAll     = isAdmin || isDispatch;

        // ── Set activo de factores de emisión ─────────────────────────────────
        var activeFactors = await _context.EmissionFactorSets
            .AsNoTracking()
            .Where(s => s.Status == "Active")
            .OrderByDescending(s => s.ValidFrom)
            .Select(s => new { s.Id, s.Version, Factors = s.EmissionFactors.Select(f => new {
                f.VehicleType, f.FuelType, f.EuroClass, f.Value
            }).ToList() })
            .FirstOrDefaultAsync(ct);

        var factorLookup = activeFactors?.Factors
            .ToDictionary(
                f => (f.VehicleType, f.FuelType, f.EuroClass),
                f => f.Value,
                new TupleComparer())
            ?? [];

        var factorVersion = activeFactors?.Version;

        // ── WasteMoves base del periodo ───────────────────────────────────────
        var wmQuery = _context.WasteMoves.AsNoTracking()
            .Where(wm => wm.ActualPickupStart >= request.DateFrom
                      && wm.ActualPickupStart <  request.DateTo.AddDays(1));

        if (!seeAll)
            wmQuery = wmQuery.Where(wm => wm.OwnerId == ownerId);

        if (request.IdScrap.HasValue)
            wmQuery = wmQuery.Where(wm => wm.IdScrap == request.IdScrap.Value);

        var wmRaw = await wmQuery.Select(wm => new
        {
            wm.Id, wm.OwnerId, wm.WasteMoveReference,
            wm.IdSource, wm.IdDestination, wm.IdScrap,
            wm.ActualPickupStart,
            Residues = wm.WasteMoveResidues.Select(r => new
            {
                r.Weight, r.VehicleType, r.FuelType, r.EuroClass,
                r.TransportInfo_TransportDistance,
                r.TransportInfo_TransportCarbonEmissions
            }).ToList()
        }).ToListAsync(ct);

        // ── Filtro geográfico: resolución por IdSource ────────────────────────
        var sourceIds = wmRaw.Where(w => w.IdSource.HasValue).Select(w => w.IdSource!.Value).Distinct().ToList();
        var entityGeo = await _context.BusinessEntities.AsNoTracking()
            .Where(e => sourceIds.Contains(e.Id))
            .Select(e => new { e.Id, e.ProvinceCode, e.MunicipalityCode })
            .ToDictionaryAsync(e => e.Id, ct);

        if (!string.IsNullOrEmpty(request.ProvinceCode))
        {
            var matchedIds = entityGeo.Where(kv => kv.Value.ProvinceCode == request.ProvinceCode)
                                      .Select(kv => kv.Key).ToHashSet();
            wmRaw = wmRaw.Where(w => w.IdSource.HasValue && matchedIds.Contains(w.IdSource.Value)).ToList();
        }

        if (!string.IsNullOrEmpty(request.MunicipalityCode))
        {
            var matchedIds = entityGeo.Where(kv =>
                    kv.Value.ProvinceCode + kv.Value.MunicipalityCode == request.MunicipalityCode
                    || kv.Value.MunicipalityCode == request.MunicipalityCode)
                .Select(kv => kv.Key).ToHashSet();
            wmRaw = wmRaw.Where(w => w.IdSource.HasValue && matchedIds.Contains(w.IdSource.Value)).ToList();
        }

        // ── Filtro tipo de vehículo ───────────────────────────────────────────
        if (!string.IsNullOrEmpty(request.VehicleType))
            wmRaw = wmRaw.Where(w => w.Residues.Any(r => r.VehicleType == request.VehicleType)).ToList();

        // ── Cálculo de emisiones ──────────────────────────────────────────────
        decimal totalCO2eKg    = 0;
        decimal totalKm        = 0;
        decimal totalWeightKg  = 0;
        int     withoutFactor  = 0;
        int     totalServices  = wmRaw.Count;

        var byFuel   = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var monthMap = new Dictionary<(int Year, int Month), decimal>();
        var top10    = new List<(string Ref, Guid? srcId, Guid? dstId, decimal km, string? veh, string? fuel, decimal wt, decimal co2)>();

        foreach (var wm in wmRaw)
        {
            decimal wmCO2 = 0;
            bool    wmHasFactor = false;
            bool    wmMissingFactor = false;

            foreach (var r in wm.Residues)
            {
                var km     = r.TransportInfo_TransportDistance ?? 0;
                var weight = r.Weight ?? 0;
                totalWeightKg += weight;
                totalKm       += km;

                // Usar emisión ya calculada si existe; si no, calcular desde factores
                decimal? lineEmission = r.TransportInfo_TransportCarbonEmissions;

                if (lineEmission is null or 0 && km > 0)
                {
                    var key = (r.VehicleType ?? "", r.FuelType ?? "", r.EuroClass ?? "");
                    if (factorLookup.TryGetValue(key, out var factor))
                    {
                        lineEmission = km * factor;
                        wmHasFactor  = true;
                    }
                    else if (km > 0)
                    {
                        wmMissingFactor = true;
                    }
                }
                else if (lineEmission > 0)
                {
                    wmHasFactor = true;
                }

                if (lineEmission > 0)
                {
                    wmCO2          += lineEmission.Value;
                    totalCO2eKg    += lineEmission.Value;
                    var fuelKey    =  r.FuelType ?? "Desconocido";
                    byFuel[fuelKey] = (byFuel.TryGetValue(fuelKey, out var prev) ? prev : 0) + lineEmission.Value;
                }
            }

            if (!wmHasFactor && wmMissingFactor)
                withoutFactor++;

            if (wm.ActualPickupStart.HasValue)
            {
                var key = (wm.ActualPickupStart.Value.Year, wm.ActualPickupStart.Value.Month);
                monthMap[key] = (monthMap.TryGetValue(key, out var mp) ? mp : 0) + wmCO2;
            }

            if (wmCO2 > 0)
            {
                var dist = wm.Residues.Sum(r => r.TransportInfo_TransportDistance ?? 0);
                var wt   = wm.Residues.Sum(r => r.Weight ?? 0);
                var fst  = wm.Residues.FirstOrDefault(r => r.VehicleType != null);
                top10.Add((wm.WasteMoveReference ?? wm.Id.ToString(), wm.IdSource, wm.IdDestination,
                    dist, fst?.VehicleType, fst?.FuelType, wt, wmCO2));
            }
        }

        // ── Nombres geográficos para Top10 ────────────────────────────────────
        var dstIds = top10.Where(t => t.dstId.HasValue).Select(t => t.dstId!.Value).Distinct().ToList();
        var allGeoIds = sourceIds.Concat(dstIds).Distinct().ToList();
        var entityNames = await _context.BusinessEntities.AsNoTracking()
            .Where(e => allGeoIds.Contains(e.Id))
            .Select(e => new { e.Id, e.Name, e.MunicipalityCode, e.ProvinceCode })
            .ToDictionaryAsync(e => e.Id, ct);

        var munCodes  = entityNames.Values.Select(e => e.MunicipalityCode).Where(c => c != null).Distinct().ToList();
        var provCodes = entityNames.Values.Select(e => e.ProvinceCode).Where(c => c != null).Distinct().ToList();

        var munNames = await _context.Municipalities.AsNoTracking()
            .Where(m => munCodes.Contains(m.Code))
            .Select(m => new { m.Code, m.Name })
            .ToDictionaryAsync(m => m.Code, ct);
        var provNames = await _context.Provinces.AsNoTracking()
            .Where(p => provCodes.Contains(p.Code))
            .Select(p => new { p.Code, p.Name })
            .ToDictionaryAsync(p => p.Code!, ct);

        string EntityLabel(Guid? id)
        {
            if (id is null || !entityNames.TryGetValue(id.Value, out var e)) return "—";
            var mun  = e.MunicipalityCode is not null && munNames.TryGetValue(e.MunicipalityCode, out var mn) ? mn.Name : null;
            var prov = e.ProvinceCode is not null && provNames.TryGetValue(e.ProvinceCode, out var pn) ? pn.Name : null;
            return mun is not null && prov is not null ? $"{mun} ({prov})" : e.Name;
        }

        var top10Sorted = top10.OrderByDescending(t => t.co2).Take(10)
            .Select(t => new TopEmissionOperationDto(
                t.Ref, EntityLabel(t.srcId), EntityLabel(t.dstId),
                t.km, t.veh, t.fuel, t.wt, t.co2))
            .ToList();

        // ── Periodo anterior (mismo rango desplazado hacia atrás) ─────────────
        var span       = request.DateTo - request.DateFrom;
        var prevFrom   = request.DateFrom - span - TimeSpan.FromDays(1);
        var prevTo     = request.DateFrom - TimeSpan.FromDays(1);
        var prevQuery  = _context.WasteMoves.AsNoTracking()
            .Where(wm => wm.ActualPickupStart >= prevFrom && wm.ActualPickupStart < prevTo);
        if (!seeAll) prevQuery = prevQuery.Where(wm => wm.OwnerId == ownerId);
        var prevCO2eKg = await prevQuery.SelectMany(wm => wm.WasteMoveResidues)
            .SumAsync(r => r.TransportInfo_TransportCarbonEmissions ?? 0, ct);

        double variation = prevCO2eKg > 0
            ? (double)((totalCO2eKg - prevCO2eKg) / prevCO2eKg * 100)
            : 0;

        // ── Serie mensual (año actual y anterior) ─────────────────────────────
        var allYears = Enumerable.Range(request.DateFrom.Year, request.DateTo.Year - request.DateFrom.Year + 1).ToList();
        if (!allYears.Contains(request.DateFrom.Year - 1)) allYears.Add(request.DateFrom.Year - 1);
        var monthlyQuery = _context.WasteMoves.AsNoTracking()
            .Where(wm => wm.ActualPickupStart!.Value.Year >= request.DateFrom.Year - 1
                      && wm.ActualPickupStart!.Value.Year <= request.DateTo.Year);
        if (!seeAll) monthlyQuery = monthlyQuery.Where(wm => wm.OwnerId == ownerId);
        var monthlySrc = await monthlyQuery
            .SelectMany(wm => wm.WasteMoveResidues, (wm, r) => new
            {
                Year  = wm.ActualPickupStart!.Value.Year,
                Month = wm.ActualPickupStart!.Value.Month,
                CO2e  = r.TransportInfo_TransportCarbonEmissions ?? 0
            })
            .GroupBy(x => new { x.Year, x.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, CO2eTonnes = g.Sum(x => x.CO2e) / 1000m })
            .ToListAsync(ct);

        var monthlyEvolution = monthlySrc
            .Select(m => new MonthlyEmissionSeriesDto(m.Year, m.Month, m.CO2eTonnes))
            .OrderBy(m => m.Year).ThenBy(m => m.Month)
            .ToList();

        // ── By Fuel ───────────────────────────────────────────────────────────
        var totalForPct = byFuel.Values.Sum();
        var byFuelList = byFuel.Select(kv => new EmissionByFuelDto(
            kv.Key, kv.Value / 1000m,
            totalForPct > 0 ? (double)(kv.Value / totalForPct * 100) : 0))
            .OrderByDescending(f => f.CO2eTonnes).ToList();

        // ── KPIs ──────────────────────────────────────────────────────────────
        var totalCO2eTonnes = totalCO2eKg / 1000m;
        var co2PerTonneKg   = totalWeightKg > 0 ? totalCO2eKg / (totalWeightKg / 1000m) : 0;

        return new CarbonOverviewDto(
            DateFrom:             request.DateFrom,
            DateTo:               request.DateTo,
            TotalCO2eTonnes:      totalCO2eTonnes,
            CO2ePerTonneKg:       co2PerTonneKg,
            VariationVsPreviousPct: variation,
            TotalDistanceKm:      totalKm,
            TotalServices:        totalServices,
            ServicesWithoutFactor: withoutFactor,
            MonthlyEvolution:     monthlyEvolution,
            ByFuelType:           byFuelList,
            Top10Operations:      top10Sorted
        );
    }
}

// ── Helper: comparador de tuplas para el diccionario de factores ──────────────
file sealed class TupleComparer : IEqualityComparer<(string?, string?, string?)>
{
    public bool Equals((string?, string?, string?) x, (string?, string?, string?) y)
        => string.Equals(x.Item1, y.Item1, StringComparison.OrdinalIgnoreCase)
        && string.Equals(x.Item2, y.Item2, StringComparison.OrdinalIgnoreCase)
        && string.Equals(x.Item3, y.Item3, StringComparison.OrdinalIgnoreCase);

    public int GetHashCode((string?, string?, string?) obj)
        => HashCode.Combine(
            obj.Item1?.ToUpperInvariant(),
            obj.Item2?.ToUpperInvariant(),
            obj.Item3?.ToUpperInvariant());
}
