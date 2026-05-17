using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Reporting.CarbonFootprint.DTOs;
using GreenTransit.Domain.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.Reporting.CarbonFootprint.Queries;

/// <summary>
/// Devuelve los datos del dashboard CO2-B — Emisiones por Transporte.
/// Los factores de emisión se obtienen del set activo; operaciones sin factor
/// se incluyen en la tabla con MissingFactor=true y se excluyen de los KPIs.
/// </summary>
public sealed record GetCarbonTransportQuery(
    DateTime  DateFrom,
    DateTime  DateTo,
    string?   VehicleType         = null,
    string?   FuelType            = null,
    Guid?     IdScrap             = null,
    string?   ProvinceCodeOrigin  = null,
    string?   MunicipalityCodeOrigin = null,
    string?   ProvinceCodeDest    = null,
    string?   MunicipalityCodeDest = null
) : IRequest<CarbonTransportDto>;

public sealed class GetCarbonTransportQueryHandler
    : IRequestHandler<GetCarbonTransportQuery, CarbonTransportDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetCarbonTransportQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<CarbonTransportDto> Handle(
        GetCarbonTransportQuery request, CancellationToken ct)
    {
        var ownerId    = _currentUser.OwnerId;
        var seeAll     = _currentUser.IsInProfile(ProfileConstants.Admin)
                      || _currentUser.IsInProfile(ProfileConstants.DispatchOffice);

        // ── Set activo de factores ────────────────────────────────────────────
        var factorLookup = await BuildFactorLookupAsync(ct);

        // ── WasteMoves base ───────────────────────────────────────────────────
        var wmQuery = _context.WasteMoves.AsNoTracking()
            .Where(wm => wm.ActualPickupStart >= request.DateFrom
                      && wm.ActualPickupStart <  request.DateTo.AddDays(1));
        if (!seeAll) wmQuery = wmQuery.Where(wm => wm.OwnerId == ownerId);
        if (request.IdScrap.HasValue) wmQuery = wmQuery.Where(wm => wm.IdScrap == request.IdScrap.Value);

        var wmRaw = await wmQuery.Select(wm => new
        {
            wm.Id, wm.WasteMoveReference, wm.IdScrap, wm.IdSource, wm.IdDestination,
            wm.ActualPickupStart,
            Residues = wm.WasteMoveResidues.Select(r => new
            {
                r.Weight, r.VehicleType, r.FuelType, r.EuroClass,
                r.TransportInfo_TransportDistance,
                r.TransportInfo_TransportCarbonEmissions
            }).ToList()
        }).ToListAsync(ct);

        // ── Lookup geográfico ─────────────────────────────────────────────────
        var allEntityIds = wmRaw.Select(w => w.IdSource).Where(id => id.HasValue).Select(id => id!.Value)
            .Concat(wmRaw.Select(w => w.IdDestination).Where(id => id.HasValue).Select(id => id!.Value))
            .Distinct().ToList();

        var entityGeo = await _context.BusinessEntities.AsNoTracking()
            .Where(e => allEntityIds.Contains(e.Id))
            .Select(e => new { e.Id, e.Name, e.ProvinceCode, e.MunicipalityCode })
            .ToDictionaryAsync(e => e.Id, ct);

        var provCodes = entityGeo.Values.Select(e => e.ProvinceCode).Where(c => c != null).Distinct().ToList();
        var munCodes  = entityGeo.Values.Select(e => e.MunicipalityCode).Where(c => c != null).Distinct().ToList();

        var provNames = await _context.Provinces.AsNoTracking()
            .Where(p => provCodes.Contains(p.Code))
            .ToDictionaryAsync(p => p.Code!, p => p.Name ?? p.Ref, ct);
        var munNames = await _context.Municipalities.AsNoTracking()
            .Where(m => munCodes.Contains(m.Code))
            .ToDictionaryAsync(m => m.Code, m => m.Name, ct);

        string ProvName(string? code) => code is not null && provNames.TryGetValue(code, out var n) ? n : "—";
        string MunName(string? code)  => code is not null && munNames.TryGetValue(code, out var n) ? n : "—";

        // ── Filtros geográficos ───────────────────────────────────────────────
        if (!string.IsNullOrEmpty(request.ProvinceCodeOrigin))
        {
            var ids = entityGeo.Where(kv => kv.Value.ProvinceCode == request.ProvinceCodeOrigin).Select(kv => kv.Key).ToHashSet();
            wmRaw = wmRaw.Where(w => w.IdSource.HasValue && ids.Contains(w.IdSource.Value)).ToList();
        }
        if (!string.IsNullOrEmpty(request.ProvinceCodeDest))
        {
            var ids = entityGeo.Where(kv => kv.Value.ProvinceCode == request.ProvinceCodeDest).Select(kv => kv.Key).ToHashSet();
            wmRaw = wmRaw.Where(w => w.IdDestination.HasValue && ids.Contains(w.IdDestination.Value)).ToList();
        }
        if (!string.IsNullOrEmpty(request.VehicleType))
            wmRaw = wmRaw.Where(w => w.Residues.Any(r => r.VehicleType == request.VehicleType)).ToList();
        if (!string.IsNullOrEmpty(request.FuelType))
            wmRaw = wmRaw.Where(w => w.Residues.Any(r => r.FuelType == request.FuelType)).ToList();

        // ── Scrap IDs → nombres ───────────────────────────────────────────────
        var scrapIds = wmRaw.Where(w => w.IdScrap.HasValue).Select(w => w.IdScrap!.Value).Distinct().ToList();
        var scrapNames = await _context.BusinessEntities.AsNoTracking()
            .Where(e => scrapIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.Name ?? e.Id.ToString(), ct);

        // ── Agregaciones ──────────────────────────────────────────────────────
        decimal totalCO2eKg = 0, totalKm = 0, totalWt = 0, lowEmissionKm = 0;

        var monthlyByFuel = new Dictionary<(int Year, int Month, string Fuel), decimal>();
        var byScrap       = new Dictionary<Guid, (string Name, decimal CO2e, decimal Km, decimal Wt)>();
        var scatter       = new List<OperationScatterDto>();
        var details       = new List<TransportOperationDetailDto>();

        foreach (var wm in wmRaw)
        {
            decimal wmCO2 = 0, wmKm = 0, wmWt = 0;
            bool missingFactor = false;
            string? firstVeh = null, firstFuel = null;

            foreach (var r in wm.Residues)
            {
                var km  = r.TransportInfo_TransportDistance ?? 0;
                var wt  = r.Weight ?? 0;
                wmKm   += km;
                wmWt   += wt;

                decimal? lineEmission = r.TransportInfo_TransportCarbonEmissions;
                if ((lineEmission is null or 0) && km > 0)
                {
                    var key = (r.VehicleType ?? "", r.FuelType ?? "", r.EuroClass ?? "");
                    if (factorLookup.TryGetValue(key, out var fv))
                        lineEmission = km * fv;
                    else
                        missingFactor = true;
                }

                if (lineEmission > 0)
                    wmCO2 += lineEmission.Value;

                firstVeh  ??= r.VehicleType;
                firstFuel ??= r.FuelType;

                // Baja emisión: eléctrico o GNC
                var fuel = r.FuelType?.ToUpperInvariant() ?? "";
                if (fuel.Contains("ELÉCTRIC") || fuel.Contains("ELECTRIC") || fuel == "GNC" || fuel == "GNL")
                    lowEmissionKm += km;

                if (lineEmission > 0 && wm.ActualPickupStart.HasValue)
                {
                    var mkey = (wm.ActualPickupStart.Value.Year, wm.ActualPickupStart.Value.Month, r.FuelType ?? "Desconocido");
                    monthlyByFuel[mkey] = (monthlyByFuel.TryGetValue(mkey, out var prev) ? prev : 0) + lineEmission.Value / 1000m;
                }
            }

            totalCO2eKg += wmCO2;
            totalKm     += wmKm;
            totalWt     += wmWt;

            if (wm.IdScrap.HasValue)
            {
                var sn = scrapNames.GetValueOrDefault(wm.IdScrap.Value, "—");
                var prev = byScrap.TryGetValue(wm.IdScrap.Value, out var bs) ? bs : (Name: sn, CO2e: 0m, Km: 0m, Wt: 0m);
                byScrap[wm.IdScrap.Value] = (Name: prev.Name, CO2e: prev.CO2e + wmCO2, Km: prev.Km + wmKm, Wt: prev.Wt + wmWt);
            }

            if (wmCO2 > 0)
                scatter.Add(new OperationScatterDto(wm.WasteMoveReference ?? wm.Id.ToString(), wmKm, wmCO2, firstVeh));

            var srcGeo = wm.IdSource.HasValue && entityGeo.TryGetValue(wm.IdSource.Value, out var sg) ? sg : null;
            var dstGeo = wm.IdDestination.HasValue && entityGeo.TryGetValue(wm.IdDestination.Value, out var dg) ? dg : null;

            details.Add(new TransportOperationDetailDto(
                WasteMoveReference:    wm.WasteMoveReference ?? wm.Id.ToString(),
                Date:                  wm.ActualPickupStart,
                OriginMunicipality:    MunName(srcGeo?.MunicipalityCode),
                OriginProvince:        ProvName(srcGeo?.ProvinceCode),
                DestinationMunicipality: MunName(dstGeo?.MunicipalityCode),
                DestinationProvince:   ProvName(dstGeo?.ProvinceCode),
                DistanceKm:            wmKm,
                VehicleType:           firstVeh,
                FuelType:              firstFuel,
                WeightKg:              wmWt,
                CO2eKg:                wmCO2 > 0 ? wmCO2 : null,
                MissingFactor:         missingFactor));
        }

        // ── KPIs ──────────────────────────────────────────────────────────────
        var totalTonnes       = totalCO2eKg / 1000m;
        var intensityPerKm    = totalKm > 0 ? totalCO2eKg / totalKm * 1000 : 0; // gCO2e/km
        var totalTonneKm      = totalWt / 1000m * totalKm;
        var intensityPerTonneKm = totalTonneKm > 0 ? totalCO2eKg / totalTonneKm * 1000 : 0; // gCO2e/t·km
        var lowEmissionPct    = totalKm > 0 ? (double)(lowEmissionKm / totalKm * 100) : 0;

        // ── Series ────────────────────────────────────────────────────────────
        var monthlyByFuelList = monthlyByFuel
            .Select(kv => new MonthlyEmissionByFuelDto(kv.Key.Year, kv.Key.Month, kv.Key.Fuel, kv.Value))
            .OrderBy(m => m.Year).ThenBy(m => m.Month).ThenBy(m => m.FuelType)
            .ToList();

        var byScrapList = byScrap.Select(kv => new EmissionByScrapDto(
            kv.Key, kv.Value.Name,
            kv.Value.CO2e / 1000m, kv.Value.Km, kv.Value.Wt,
            kv.Value.Wt > 0 ? kv.Value.CO2e / (kv.Value.Wt / 1000m) : 0))
            .OrderByDescending(s => s.CO2eTonnes).ToList();

        return new CarbonTransportDto(
            DateFrom:             request.DateFrom,
            DateTo:               request.DateTo,
            TransportCO2eTonnes:  totalTonnes,
            IntensityGCO2ePerKm:  intensityPerKm,
            IntensityGCO2ePerTonneKm: intensityPerTonneKm,
            LowEmissionKmPct:     lowEmissionPct,
            MonthlyByFuel:        monthlyByFuelList,
            ByScrap:              byScrapList,
            ScatterData:          scatter.OrderByDescending(s => s.CO2eKg).Take(200).ToList(),
            OperationDetails:     details.OrderByDescending(d => d.CO2eKg ?? 0).ToList()
        );
    }

    private async Task<Dictionary<(string, string, string), decimal>> BuildFactorLookupAsync(CancellationToken ct)
    {
        var set = await _context.EmissionFactorSets.AsNoTracking()
            .Where(s => s.Status == "Active")
            .OrderByDescending(s => s.ValidFrom)
            .Select(s => new { Factors = s.EmissionFactors.Select(f => new { f.VehicleType, f.FuelType, f.EuroClass, f.Value }).ToList() })
            .FirstOrDefaultAsync(ct);

        return set?.Factors.ToDictionary(
            f => (f.VehicleType ?? "", f.FuelType ?? "", f.EuroClass ?? ""),
            f => f.Value,
            new TupleComparerB())
            ?? [];
    }
}

file sealed class TupleComparerB : IEqualityComparer<(string, string, string)>
{
    public bool Equals((string, string, string) x, (string, string, string) y)
        => string.Equals(x.Item1, y.Item1, StringComparison.OrdinalIgnoreCase)
        && string.Equals(x.Item2, y.Item2, StringComparison.OrdinalIgnoreCase)
        && string.Equals(x.Item3, y.Item3, StringComparison.OrdinalIgnoreCase);

    public int GetHashCode((string, string, string) obj)
        => HashCode.Combine(obj.Item1.ToUpperInvariant(), obj.Item2.ToUpperInvariant(), obj.Item3.ToUpperInvariant());
}
