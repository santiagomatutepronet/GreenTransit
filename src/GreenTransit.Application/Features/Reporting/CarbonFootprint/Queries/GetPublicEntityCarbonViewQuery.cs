using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Reporting.CarbonFootprint.DTOs;
using GreenTransit.Domain.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GreenTransit.Application.Features.Reporting.CarbonFootprint.Queries;

/// <summary>
/// Devuelve los datos del dashboard HC-E — Panel de Emisiones para Entidades Públicas.
/// PUBLIC_ENT ve los traslados cuyo punto de recogida o SO fueron emitidas por su entidad.
/// DISPATCH_OFFICE / ADMIN ven todos los del tenant.
/// Los umbrales de alerta son configurables (sección CarbonAlerts).
/// </summary>
public sealed record GetPublicEntityCarbonViewQuery(
    DateTime DateFrom,
    DateTime DateTo,
    Guid?    IdScrap     = null,
    string?  WasteStream = null,
    string?  LerCode     = null
) : IRequest<CarbonPublicViewDto>;

public sealed class GetPublicEntityCarbonViewQueryHandler
    : IRequestHandler<GetPublicEntityCarbonViewQuery, CarbonPublicViewDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;
    private readonly double                _avgExceedThresholdPct;
    private readonly double                _monthlyGrowthThresholdPct;

    public GetPublicEntityCarbonViewQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser,
        IConfiguration        configuration)
    {
        _context                    = context;
        _currentUser                = currentUser;
        _avgExceedThresholdPct      = double.TryParse(configuration["CarbonAlerts:AvgExceedThresholdPct"],
            System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var av) && av > 0 ? av : 20.0;
        _monthlyGrowthThresholdPct  = double.TryParse(configuration["CarbonAlerts:MonthlyGrowthThresholdPct"],
            System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var mv) && mv > 0 ? mv : 15.0;
    }

    public async Task<CarbonPublicViewDto> Handle(
        GetPublicEntityCarbonViewQuery request, CancellationToken ct)
    {
        var ownerId        = _currentUser.OwnerId;
        var isAdmin        = _currentUser.IsInProfile(ProfileConstants.Admin);
        var isDispatch     = _currentUser.IsInProfile(ProfileConstants.DispatchOffice);
        var seeAll         = isAdmin || isDispatch;
        var linkedEntityId = _currentUser.LinkedEntityId;

        // Nombre de municipio de la entidad
        string municipalityName = "—";
        if (!seeAll && linkedEntityId.HasValue)
        {
            var ent = await _context.BusinessEntities.AsNoTracking()
                .Where(e => e.Id == linkedEntityId.Value)
                .Select(e => new { e.MunicipalityCode })
                .FirstOrDefaultAsync(ct);
            if (ent?.MunicipalityCode is not null)
            {
                var mun = await _context.Municipalities.AsNoTracking()
                    .Where(m => m.Code == ent.MunicipalityCode)
                    .Select(m => m.Name)
                    .FirstOrDefaultAsync(ct);
                municipalityName = mun ?? ent.MunicipalityCode;
            }
        }

        // ── IDs de SO visibles ────────────────────────────────────────────────
        List<Guid>? restrictedSoIds = null;
        if (!seeAll && linkedEntityId.HasValue)
        {
            // SO emitidas por la entidad pública O cuyo punto de recogida pertenece a su municipio
            var entityMunCode = await _context.BusinessEntities.AsNoTracking()
                .Where(e => e.Id == linkedEntityId.Value)
                .Select(e => e.MunicipalityCode)
                .FirstOrDefaultAsync(ct);

            Guid[] pickupPointIds = [];
            if (!string.IsNullOrEmpty(entityMunCode))
            {
                pickupPointIds = await _context.BusinessEntities.AsNoTracking()
                    .Where(e => e.MunicipalityCode == entityMunCode)
                    .Select(e => e.Id)
                    .ToArrayAsync(ct);
            }

            restrictedSoIds = await _context.ServiceOrders.AsNoTracking()
                .Where(so => so.OwnerId == ownerId
                          && (so.IdIssuedBy == linkedEntityId.Value
                          || (so.IdPickupPoint.HasValue && pickupPointIds.Contains(so.IdPickupPoint.Value))))
                .Select(so => so.Id)
                .ToListAsync(ct);
        }

        // ── WasteMoves ───────────────────────────────────────────────────────
        var wmQuery = _context.WasteMoves.AsNoTracking()
            .Where(wm => wm.OwnerId == ownerId
                      && wm.ActualPickupStart >= request.DateFrom
                      && wm.ActualPickupStart <  request.DateTo.AddDays(1));

        if (restrictedSoIds is not null)
            wmQuery = wmQuery.Where(wm => wm.ServiceOrderId.HasValue
                                       && restrictedSoIds.Contains(wm.ServiceOrderId.Value));
        if (request.IdScrap.HasValue)
            wmQuery = wmQuery.Where(wm => wm.IdScrap == request.IdScrap.Value);

        var wmRaw = await wmQuery.Select(wm => new
        {
            wm.Id, wm.IdScrap, wm.ActualPickupStart,
            Residues = wm.WasteMoveResidues.Select(r => new
            {
                r.Weight, r.FuelType, r.VehicleType, r.EuroClass,
                r.TransportInfo_TransportDistance,
                r.TransportInfo_TransportCarbonEmissions
            }).ToList()
        }).ToListAsync(ct);

        // ── Factores activos ──────────────────────────────────────────────────
        var factorLookup = await BuildFactorLookupAsync(ct);

        // ── SCRAP names ───────────────────────────────────────────────────────
        var scrapIds = wmRaw.Where(w => w.IdScrap.HasValue).Select(w => w.IdScrap!.Value).Distinct().ToList();
        var scrapNames = await _context.BusinessEntities.AsNoTracking()
            .Where(e => scrapIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.Name ?? e.Id.ToString(), ct);

        // ── Agregaciones ──────────────────────────────────────────────────────
        decimal totalCO2 = 0, totalWt = 0;
        int     totalServices = 0;

        var monthly      = new Dictionary<(int Year, int Month), (decimal CO2, decimal Wt)>();
        var byScrap      = new Dictionary<Guid, (string Name, decimal CO2, decimal Wt, decimal Km, int Count)>();
        var monthlyFuel  = new Dictionary<(int Year, int Month, string Fuel), decimal>();

        foreach (var wm in wmRaw)
        {
            totalServices++;
            decimal wmCO2 = 0, wmKm = 0, wmWt = 0;

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
                }
                if (lineEmission > 0)
                {
                    wmCO2 += lineEmission.Value;
                    if (wm.ActualPickupStart.HasValue)
                    {
                        var fkey = (Year: wm.ActualPickupStart.Value.Year, Month: wm.ActualPickupStart.Value.Month, Fuel: r.FuelType ?? "Desconocido");
                        monthlyFuel[fkey] = (monthlyFuel.TryGetValue(fkey, out var fp) ? fp : 0) + lineEmission.Value;
                    }
                }
            }

            totalCO2 += wmCO2;
            totalWt  += wmWt;

            var mkey  = (Year: wm.ActualPickupStart?.Year ?? request.DateFrom.Year,
                         Month: wm.ActualPickupStart?.Month ?? request.DateFrom.Month);
            var mPrev = monthly.TryGetValue(mkey, out var mp) ? mp : (CO2: 0m, Wt: 0m);
            monthly[mkey] = (CO2: mPrev.CO2 + wmCO2, Wt: mPrev.Wt + wmWt);

            if (wm.IdScrap.HasValue)
            {
                var sn    = scrapNames.GetValueOrDefault(wm.IdScrap.Value, "—");
                var sPrev = byScrap.TryGetValue(wm.IdScrap.Value, out var sp) ? sp : (Name: sn, CO2: 0m, Wt: 0m, Km: 0m, Count: 0);
                byScrap[wm.IdScrap.Value] = (Name: sPrev.Name, CO2: sPrev.CO2 + wmCO2, Wt: sPrev.Wt + wmWt, Km: sPrev.Km + wmKm, Count: sPrev.Count + 1);
            }
        }

        var totalTonnes  = totalWt / 1000m;
        var intensity    = totalTonnes > 0 ? totalCO2 / totalTonnes : 0;

        // ── Periodo anterior ──────────────────────────────────────────────────
        var periodDays = (request.DateTo - request.DateFrom).TotalDays + 1;
        var prevFrom   = request.DateFrom.AddDays(-periodDays);
        var prevTo     = request.DateFrom.AddDays(-1);

        var prevQuery = _context.WasteMoves.AsNoTracking()
            .Where(wm => wm.OwnerId == ownerId
                      && wm.ActualPickupStart >= prevFrom
                      && wm.ActualPickupStart <  prevTo.AddDays(1));
        if (restrictedSoIds is not null)
            prevQuery = prevQuery.Where(wm => wm.ServiceOrderId.HasValue
                                           && restrictedSoIds.Contains(wm.ServiceOrderId.Value));
        var prevCO2 = await prevQuery
            .SelectMany(wm => wm.WasteMoveResidues)
            .SumAsync(r => r.TransportInfo_TransportCarbonEmissions ?? 0, ct);
        var variationPct = prevCO2 > 0 ? (double)((totalCO2 - prevCO2) / prevCO2 * 100) : 0;

        // Media global del tenant
        var tenantAvgIntensity = await _context.WasteMoveResidues.AsNoTracking()
            .Where(r => r.WasteMove!.OwnerId == ownerId
                     && r.WasteMove.ActualPickupStart >= request.DateFrom
                     && r.WasteMove.ActualPickupStart <  request.DateTo.AddDays(1)
                     && r.TransportInfo_TransportCarbonEmissions > 0
                     && r.Weight > 0)
            .Select(r => new { r.TransportInfo_TransportCarbonEmissions, r.Weight })
            .ToListAsync(ct);
        var ecoAvg = tenantAvgIntensity.Any()
            ? tenantAvgIntensity.Sum(r => r.TransportInfo_TransportCarbonEmissions ?? 0)
              / (tenantAvgIntensity.Sum(r => r.Weight ?? 0) / 1000m)
            : 0;

        // ── Alertas ───────────────────────────────────────────────────────────
        var alerts = new List<CarbonAlertDto>();
        if (ecoAvg > 0 && intensity > ecoAvg * (1 + (decimal)(_avgExceedThresholdPct / 100)))
            alerts.Add(new CarbonAlertDto("danger",
                $"La intensidad del municipio ({intensity:F1} kgCO₂e/t) supera la media del ecosistema ({ecoAvg:F1} kgCO₂e/t) en más de un {_avgExceedThresholdPct}%."));
        if (prevCO2 > 0 && totalCO2 > prevCO2 * (1 + (decimal)(_monthlyGrowthThresholdPct / 100)))
            alerts.Add(new CarbonAlertDto("warning",
                $"Las emisiones del periodo actual superan el periodo anterior en más de un {_monthlyGrowthThresholdPct}% (+{variationPct:F1}%)."));

        // ── Series ────────────────────────────────────────────────────────────
        var monthlyList = monthly
            .Select(kv => new MonthlyPublicEmissionDto(
                kv.Key.Year, kv.Key.Month,
                kv.Value.CO2,
                kv.Value.Wt > 0 ? kv.Value.CO2 / (kv.Value.Wt / 1000m) : 0))
            .OrderBy(m => m.Year).ThenBy(m => m.Month)
            .ToList();

        var byScrapList = byScrap.Select(kv => new EmissionByScrapPublicDto(
            kv.Key, kv.Value.Name, kv.Value.CO2, kv.Value.Wt,
            kv.Value.Wt > 0 ? kv.Value.CO2 / (kv.Value.Wt / 1000m) : 0,
            kv.Value.Count,
            kv.Value.Count > 0 ? kv.Value.Km / kv.Value.Count : 0))
            .OrderByDescending(s => s.CO2eKg).ToList();

        var monthlyFuelList = monthlyFuel
            .Select(kv => new MonthlyFuelStackDto(kv.Key.Year, kv.Key.Month, kv.Key.Fuel, kv.Value))
            .OrderBy(m => m.Year).ThenBy(m => m.Month).ThenBy(m => m.FuelType)
            .ToList();

        return new CarbonPublicViewDto(
            DateFrom:               request.DateFrom,
            DateTo:                 request.DateTo,
            MunicipalityName:       municipalityName,
            TotalCO2eKg:            totalCO2,
            TotalTonnesManaged:     totalTonnes,
            IntensityCO2ePerTonne:  intensity,
            TotalServices:          totalServices,
            VariationVsPreviousPct: variationPct,
            MonthlyEvolution:       monthlyList,
            ByScrap:                byScrapList,
            MonthlyByFuel:          monthlyFuelList,
            Alerts:                 alerts
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
            new TupleComparerE())
            ?? [];
    }
}

file sealed class TupleComparerE : IEqualityComparer<(string, string, string)>
{
    public bool Equals((string, string, string) x, (string, string, string) y)
        => string.Equals(x.Item1, y.Item1, StringComparison.OrdinalIgnoreCase)
        && string.Equals(x.Item2, y.Item2, StringComparison.OrdinalIgnoreCase)
        && string.Equals(x.Item3, y.Item3, StringComparison.OrdinalIgnoreCase);

    public int GetHashCode((string, string, string) obj)
        => HashCode.Combine(obj.Item1.ToUpperInvariant(), obj.Item2.ToUpperInvariant(), obj.Item3.ToUpperInvariant());
}
