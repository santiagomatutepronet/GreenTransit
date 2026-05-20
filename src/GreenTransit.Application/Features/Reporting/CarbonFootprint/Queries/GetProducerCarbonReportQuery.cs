using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Reporting.CarbonFootprint.DTOs;
using GreenTransit.Domain.Authorization;
using MediatR;

namespace GreenTransit.Application.Features.Reporting.CarbonFootprint.Queries;

/// <summary>
/// Devuelve los datos del dashboard HC-D — Reporte de Huella de Carbono para Productores.
/// PRODUCER ve solo los traslados de ServiceOrders emitidas por su entidad.
/// ADMIN ve todos los del tenant.
/// </summary>
public sealed record GetProducerCarbonReportQuery(
    DateTime DateFrom,
    DateTime DateTo,
    string?  LerCode     = null,
    string?  WasteStream = null
) : IRequest<CarbonProducerReportDto>;

public sealed class GetProducerCarbonReportQueryHandler
    : IRequestHandler<GetProducerCarbonReportQuery, CarbonProducerReportDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetProducerCarbonReportQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<CarbonProducerReportDto> Handle(
        GetProducerCarbonReportQuery request, CancellationToken ct)
    {
        var ownerId        = _currentUser.OwnerId;
        var isAdmin        = _currentUser.IsInProfile(ProfileConstants.Admin);
        var linkedEntityId = _currentUser.LinkedEntityId;

        // ── ServiceOrders del productor ───────────────────────────────────────
        IQueryable<Guid> soIds;
        if (isAdmin)
        {
            soIds = _context.ServiceOrders.AsNoTracking()
                .Where(so => so.OwnerId == ownerId)
                .Select(so => so.Id);
        }
        else
        {
            if (!linkedEntityId.HasValue)
                return EmptyResult(request);

            soIds = _context.ServiceOrders.AsNoTracking()
                .Where(so => so.OwnerId == ownerId
                          && so.IdIssuedBy == linkedEntityId.Value)
                .Select(so => so.Id);
        }

        if (!string.IsNullOrEmpty(request.WasteStream))
        {
            soIds = _context.ServiceOrders.AsNoTracking()
                .Where(so => so.OwnerId == ownerId
                          && (isAdmin || so.IdIssuedBy == linkedEntityId!.Value)
                          && so.WasteStream == request.WasteStream)
                .Select(so => so.Id);
        }

        var soIdList = await soIds.ToListAsync(ct);
        if (soIdList.Count == 0)
            return EmptyResult(request);

        // ── WasteMoves del periodo vinculados a esas SOs ───────────────────────
        var wmQuery = _context.WasteMoves.AsNoTracking()
            .Where(wm => wm.ServiceOrderId.HasValue
                      && soIdList.Contains(wm.ServiceOrderId.Value)
                      && wm.ActualPickupStart >= request.DateFrom
                      && wm.ActualPickupStart <  request.DateTo.AddDays(1));

        var wmRaw = await wmQuery.Select(wm => new
        {
            wm.Id, wm.WasteMoveReference, wm.IdDestination, wm.ActualPickupStart,
            wm.ServiceOrderId,
            Residues = wm.WasteMoveResidues.Select(r => new
            {
                r.Weight, r.VehicleType, r.FuelType, r.EuroClass,
                r.IdResidue,
                r.TransportInfo_TransportDistance,
                r.TransportInfo_TransportCarbonEmissions
            }).ToList()
        }).ToListAsync(ct);

        // ── Filtro LER ────────────────────────────────────────────────────────
        if (!string.IsNullOrEmpty(request.LerCode))
        {
            var lerResidueIds = await _context.Residues.AsNoTracking()
                .Join(_context.LerCodes.AsNoTracking(),
                      r => r.IdLERCode, l => l.Id,
                      (r, l) => new { r.Id, l.Code })
                .Where(x => x.Code == request.LerCode)
                .Select(x => x.Id)
                .ToListAsync(ct);

            wmRaw = wmRaw.Where(wm =>
                wm.Residues.Any(r => r.IdResidue.HasValue && lerResidueIds.Contains(r.IdResidue.Value)))
                .ToList();
        }

        // ── Factores activos ──────────────────────────────────────────────────
        var factorLookup = await BuildFactorLookupAsync(ct);

        // ── Lookups ───────────────────────────────────────────────────────────
        var destIds = wmRaw.Where(w => w.IdDestination.HasValue).Select(w => w.IdDestination!.Value).Distinct().ToList();
        var destNames = await _context.BusinessEntities.AsNoTracking()
            .Where(e => destIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.Name ?? e.Id.ToString(), ct);

        // Residuo → LER
        var residueIds = wmRaw.SelectMany(w => w.Residues.Where(r => r.IdResidue.HasValue).Select(r => r.IdResidue!.Value)).Distinct().ToList();
        var residueLer = await _context.Residues.AsNoTracking()
            .Where(r => residueIds.Contains(r.Id))
            .Join(_context.LerCodes.AsNoTracking(),
                  r => r.IdLERCode, l => l.Id,
                  (r, l) => new { r.Id, LerCode = l.Code, LerDescription = l.Description })
            .ToDictionaryAsync(x => x.Id, ct);

        // ── Periodo anterior ──────────────────────────────────────────────────
        var periodDays = (request.DateTo - request.DateFrom).TotalDays + 1;
        var prevFrom   = request.DateFrom.AddDays(-periodDays);
        var prevTo     = request.DateFrom.AddDays(-1);
        var prevWmIds  = await _context.WasteMoves.AsNoTracking()
            .Where(wm => wm.ServiceOrderId.HasValue
                      && soIdList.Contains(wm.ServiceOrderId.Value)
                      && wm.ActualPickupStart >= prevFrom
                      && wm.ActualPickupStart <  prevTo.AddDays(1))
            .SelectMany(wm => wm.WasteMoveResidues.Select(r => r.TransportInfo_TransportCarbonEmissions ?? 0))
            .SumAsync(ct);
        decimal prevCO2 = prevWmIds;

        // ── Agregaciones ──────────────────────────────────────────────────────
        decimal totalCO2 = 0, totalWt = 0;
        int     totalServices = 0;

        var monthly   = new Dictionary<(int Year, int Month), (decimal CO2, decimal Wt)>();
        var byLer     = new Dictionary<string, (string Desc, decimal CO2, decimal Wt, int Count)>();
        var byDest    = new Dictionary<string, (decimal CO2, decimal Km, decimal Wt, int Count)>();
        var details   = new List<ProducerOperationDetailDto>();

        foreach (var wm in wmRaw)
        {
            totalServices++;
            decimal wmCO2 = 0, wmKm = 0, wmWt = 0;
            bool missingFactor = false;
            string? firstVeh = null, firstFuel = null;
            string? lerCode = null, lerDesc = null;

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
                if (lineEmission > 0) wmCO2 += lineEmission.Value;

                firstVeh  ??= r.VehicleType;
                firstFuel ??= r.FuelType;

                if (r.IdResidue.HasValue && residueLer.TryGetValue(r.IdResidue.Value, out var rl))
                {
                    lerCode ??= rl.LerCode;
                    lerDesc ??= rl.LerDescription;
                    var lk = rl.LerCode ?? "—";
                        var prev = byLer.TryGetValue(lk, out var bl) ? bl : (Desc: rl.LerDescription ?? "—", CO2: 0m, Wt: 0m, Count: 0);
                        byLer[lk] = (Desc: prev.Desc, CO2: prev.CO2 + (lineEmission ?? 0), Wt: prev.Wt + wt, Count: prev.Count + 1);
                }
            }

            totalCO2 += wmCO2;
            totalWt  += wmWt;

            var mkey = (Year: wm.ActualPickupStart?.Year ?? request.DateFrom.Year,
                        Month: wm.ActualPickupStart?.Month ?? request.DateFrom.Month);
            var mPrev = monthly.TryGetValue(mkey, out var mp) ? mp : (CO2: 0m, Wt: 0m);
            monthly[mkey] = (CO2: mPrev.CO2 + wmCO2, Wt: mPrev.Wt + wmWt);

            var destName = wm.IdDestination.HasValue
                ? destNames.GetValueOrDefault(wm.IdDestination.Value, "—")
                : "—";
            var dPrev = byDest.TryGetValue(destName, out var dp) ? dp : (CO2: 0m, Km: 0m, Wt: 0m, Count: 0);
            byDest[destName] = (CO2: dPrev.CO2 + wmCO2, Km: dPrev.Km + wmKm, Wt: dPrev.Wt + wmWt, Count: dPrev.Count + 1);

            details.Add(new ProducerOperationDetailDto(
                WasteMoveReference: wm.WasteMoveReference ?? wm.Id.ToString(),
                Date:               wm.ActualPickupStart,
                LerCode:            lerCode,
                LerDescription:     lerDesc,
                DestinationName:    destName,
                DistanceKm:         wmKm,
                VehicleType:        firstVeh,
                FuelType:           firstFuel,
                WeightKg:           wmWt,
                CO2eKg:             wmCO2 > 0 ? wmCO2 : null,
                MissingFactor:      missingFactor));
        }

        var totalTonnes     = totalWt / 1000m;
        var intensity       = totalTonnes > 0 ? totalCO2 / totalTonnes : 0;
        var variationPct    = prevCO2 > 0 ? (double)((totalCO2 - prevCO2) / prevCO2 * 100) : 0;

        // Media ecosistema (todos del tenant)
        var ecoTotal = await _context.WasteMoveResidues.AsNoTracking()
            .Where(r => r.WasteMove!.OwnerId == ownerId
                     && r.WasteMove.ActualPickupStart >= request.DateFrom
                     && r.WasteMove.ActualPickupStart <  request.DateTo.AddDays(1)
                     && r.TransportInfo_TransportCarbonEmissions > 0
                     && r.Weight > 0)
            .Select(r => new { r.TransportInfo_TransportCarbonEmissions, r.Weight })
            .ToListAsync(ct);
        var ecoAvgIntensity = ecoTotal.Any()
            ? ecoTotal.Sum(r => r.TransportInfo_TransportCarbonEmissions ?? 0)
              / (ecoTotal.Sum(r => r.Weight ?? 0) / 1000m)
            : 0;

        var monthlyList = monthly
            .Select(kv => new MonthlyProducerEmissionDto(
                kv.Key.Year, kv.Key.Month,
                kv.Value.CO2,
                kv.Value.Wt > 0 ? kv.Value.CO2 / (kv.Value.Wt / 1000m) : 0))
            .OrderBy(m => m.Year).ThenBy(m => m.Month)
            .ToList();

        var byLerList = byLer.Select(kv => new EmissionByLerCodeDto(
            kv.Key, kv.Value.Desc, kv.Value.CO2, kv.Value.Wt,
            kv.Value.Wt > 0 ? kv.Value.CO2 / (kv.Value.Wt / 1000m) : 0,
            kv.Value.Count))
            .OrderByDescending(l => l.CO2eKg).ToList();

        var byDestList = byDest.Select(kv => new EmissionByDestinationDto(
            kv.Key,
            kv.Value.Count > 0 ? kv.Value.Km / kv.Value.Count : 0,
            kv.Value.CO2,
            kv.Value.Wt > 0 ? kv.Value.CO2 / (kv.Value.Wt / 1000m) : 0,
            kv.Value.Count))
            .OrderByDescending(d => d.CO2eKg).ToList();

        return new CarbonProducerReportDto(
            DateFrom:                  request.DateFrom,
            DateTo:                    request.DateTo,
            TotalCO2eKg:               totalCO2,
            TotalTonnesManaged:        totalTonnes,
            IntensityCO2ePerTonne:     intensity,
            TotalServices:             totalServices,
            VariationVsPreviousPct:    variationPct,
            MonthlyEvolution:          monthlyList,
            ByLerCode:                 byLerList,
            ByDestination:             byDestList,
            EcosystemAvgCO2ePerTonne:  ecoAvgIntensity,
            Details:                   details.OrderByDescending(d => d.CO2eKg ?? 0).ToList()
        );
    }

    private static CarbonProducerReportDto EmptyResult(GetProducerCarbonReportQuery request) =>
        new(request.DateFrom, request.DateTo, 0, 0, 0, 0, 0,
            [], [], [], 0, []);

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
            new TupleComparerD())
            ?? [];
    }
}

file sealed class TupleComparerD : IEqualityComparer<(string, string, string)>
{
    public bool Equals((string, string, string) x, (string, string, string) y)
        => string.Equals(x.Item1, y.Item1, StringComparison.OrdinalIgnoreCase)
        && string.Equals(x.Item2, y.Item2, StringComparison.OrdinalIgnoreCase)
        && string.Equals(x.Item3, y.Item3, StringComparison.OrdinalIgnoreCase);

    public int GetHashCode((string, string, string) obj)
        => HashCode.Combine(obj.Item1.ToUpperInvariant(), obj.Item2.ToUpperInvariant(), obj.Item3.ToUpperInvariant());
}
