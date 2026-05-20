using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Reporting.CarbonFootprint.DTOs;
using GreenTransit.Domain.Authorization;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace GreenTransit.Application.Features.Reporting.CarbonFootprint.Queries;

/// <summary>
/// Devuelve los datos del dashboard HC-C — Huella Energética de Plantas (Scope 2).
/// El factor kWh → kgCO₂e se lee de la configuración (sección PlantEnergy:GridEmissionFactor).
/// </summary>
public sealed record GetPlantEnergyFootprintQuery(
    DateTime DateFrom,
    DateTime DateTo,
    string?  PlantName         = null,
    string?  PlantCenterCode   = null,
    string?  Source            = null
) : IRequest<CarbonPlantEnergyDto>;

public sealed class GetPlantEnergyFootprintQueryHandler
    : IRequestHandler<GetPlantEnergyFootprintQuery, CarbonPlantEnergyDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;
    private readonly decimal               _gridEmissionFactor;

    public GetPlantEnergyFootprintQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser,
        IConfiguration        configuration)
    {
        _context            = context;
        _currentUser        = currentUser;
        _gridEmissionFactor = decimal.TryParse(configuration["PlantEnergy:GridEmissionFactor"],
            System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var gf) && gf > 0 ? gf : 0.27m;
    }

    public async Task<CarbonPlantEnergyDto> Handle(
        GetPlantEnergyFootprintQuery request, CancellationToken ct)
    {
        var ownerId    = _currentUser.OwnerId;
        var isPlantOp  = _currentUser.IsInProfile(ProfileConstants.PlantOp);
        var isScrap    = _currentUser.IsInProfile(ProfileConstants.Scrap);

        // ── Restricción por perfil: PLANT_OP → solo su planta ─────────────────
        string? plantOpCenterCode = null;
        if (isPlantOp)
        {
            plantOpCenterCode = await _context.BusinessEntities.AsNoTracking()
                .Where(e => e.Id == _currentUser.LinkedEntityId)
                .Select(e => e.CenterCode)
                .FirstOrDefaultAsync(ct);
        }

        // ── Restricción por perfil: SCRAP → plantas destino de sus traslados ──
        List<string>? scrapPlantCenterCodes = null;
        List<Guid>?   scrapPlantEntityIds   = null;
        if (isScrap)
        {
            scrapPlantEntityIds = await _context.WasteMoves.AsNoTracking()
                .Where(wm => (wm.IdScrap == _currentUser.LinkedEntityId
                           || wm.IdScrap2 == _currentUser.LinkedEntityId)
                          && wm.OwnerId == ownerId
                          && wm.IdDestination.HasValue)
                .Select(wm => wm.IdDestination!.Value)
                .Distinct()
                .ToListAsync(ct);

            scrapPlantCenterCodes = await _context.BusinessEntities.AsNoTracking()
                .Where(e => scrapPlantEntityIds.Contains(e.Id) && e.CenterCode != null)
                .Select(e => e.CenterCode!)
                .Distinct()
                .ToListAsync(ct);
        }

        // ── Energía de plantas ────────────────────────────────────────────────
        var energyQuery = _context.PlantEnergies.AsNoTracking()
            .Where(pe => pe.OwnerId == ownerId
                      && pe.Year >= request.DateFrom.Year
                      && pe.Year <= request.DateTo.Year);

        if (isPlantOp && plantOpCenterCode is not null)
            energyQuery = energyQuery.Where(pe => pe.PlantCenterCode == plantOpCenterCode);
        else if (isScrap && scrapPlantCenterCodes is not null)
            energyQuery = energyQuery.Where(pe => scrapPlantCenterCodes.Contains(pe.PlantCenterCode));

        if (!string.IsNullOrEmpty(request.PlantName))
            energyQuery = energyQuery.Where(pe => pe.PlantName == request.PlantName);
        if (!string.IsNullOrEmpty(request.PlantCenterCode))
            energyQuery = energyQuery.Where(pe => pe.PlantCenterCode == request.PlantCenterCode);
        if (!string.IsNullOrEmpty(request.Source))
            energyQuery = energyQuery.Where(pe => pe.Source == request.Source);

        var energyData = await energyQuery.Select(pe => new
        {
            pe.PlantName, pe.PlantCenterCode, pe.Year, pe.Month,
            pe.KwhTotal, pe.Source, pe.GridMixRef, pe.AllocationMethod
        }).ToListAsync(ct);

        // Filtrar meses en rango del periodo exacto
        energyData = energyData.Where(pe =>
        {
            if (!pe.Month.HasValue) return true;
            var date = new DateTime(pe.Year, pe.Month.Value, 1);
            return date >= new DateTime(request.DateFrom.Year, request.DateFrom.Month, 1)
                && date <= new DateTime(request.DateTo.Year, request.DateTo.Month, 1);
        }).ToList();

        // ── EntryPlants: toneladas tratadas por planta/mes ────────────────────
        var epQuery = _context.EntryPlants.AsNoTracking()
            .Where(ep => ep.OwnerId == ownerId
                      && ep.PlantEntryDate >= request.DateFrom
                      && ep.PlantEntryDate <  request.DateTo.AddDays(1));

        // Aplicar restricción de planta/traslados al query de entradas
        if (isPlantOp && _currentUser.LinkedEntityId.HasValue)
        {
            var plantId = _currentUser.LinkedEntityId.Value;
            var plantWmIds = await _context.WasteMoves.AsNoTracking()
                .Where(wm => wm.IdDestination == plantId && wm.OwnerId == ownerId)
                .Select(wm => wm.Id)
                .ToListAsync(ct);
            epQuery = epQuery.Where(ep => plantWmIds.Contains(ep.IdWasteMove));
        }
        else if (isScrap && scrapPlantEntityIds is not null)
        {
            var scrapWmIds = await _context.WasteMoves.AsNoTracking()
                .Where(wm => (wm.IdScrap == _currentUser.LinkedEntityId
                           || wm.IdScrap2 == _currentUser.LinkedEntityId)
                          && wm.OwnerId == ownerId)
                .Select(wm => wm.Id)
                .ToListAsync(ct);
            epQuery = epQuery.Where(ep => scrapWmIds.Contains(ep.IdWasteMove));
        }

        var entryData = await epQuery
            .Join(_context.WasteMoves.AsNoTracking(),
                  ep => ep.IdWasteMove, wm => wm.Id,
                  (ep, wm) => new { ep.NetWeight, ep.PlantEntryDate, wm.IdDestination })
            .ToListAsync(ct);

        // Lookup entidad → nombre de planta
        var plantEntityIds = entryData
            .Where(e => e.IdDestination.HasValue)
            .Select(e => e.IdDestination!.Value)
            .Distinct().ToList();

        var plantEntityNames = await _context.BusinessEntities.AsNoTracking()
            .Where(e => plantEntityIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.Name ?? e.Id.ToString(), ct);

        // Agrupar toneladas por planta
        var treatmentByPlant = entryData
            .Where(e => e.IdDestination.HasValue)
            .GroupBy(e => plantEntityNames.GetValueOrDefault(e.IdDestination!.Value, "—"))
            .ToDictionary(g => g.Key, g => g.Sum(e => e.NetWeight ?? 0));

        // ── Periodo anterior para variación ───────────────────────────────────
        var periodDays = (request.DateTo - request.DateFrom).TotalDays + 1;
        var prevFrom   = request.DateFrom.AddDays(-periodDays);
        var prevTo     = request.DateFrom.AddDays(-1);

        var prevEnergyQuery = _context.PlantEnergies.AsNoTracking()
            .Where(pe => pe.OwnerId == ownerId
                      && pe.Year >= prevFrom.Year
                      && pe.Year <= prevTo.Year);

        if (isPlantOp && plantOpCenterCode is not null)
            prevEnergyQuery = prevEnergyQuery.Where(pe => pe.PlantCenterCode == plantOpCenterCode);
        else if (isScrap && scrapPlantCenterCodes is not null)
            prevEnergyQuery = prevEnergyQuery.Where(pe => scrapPlantCenterCodes.Contains(pe.PlantCenterCode));

        var prevEnergy = await prevEnergyQuery.SumAsync(pe => pe.KwhTotal, ct);

        var currentKwh = energyData.Sum(pe => pe.KwhTotal);
        var prevCO2    = prevEnergy * _gridEmissionFactor;
        var currentCO2 = currentKwh * _gridEmissionFactor;
        var variation  = prevCO2 > 0 ? (double)((currentCO2 - prevCO2) / prevCO2 * 100) : 0;

        var totalTreatmentKg = treatmentByPlant.Values.Sum();
        var scope2CO2ePerTonne = totalTreatmentKg > 0
            ? currentCO2 / (totalTreatmentKg / 1000m)
            : 0;

        // ── Comparativa por planta ────────────────────────────────────────────
        var plantComparison = energyData
            .GroupBy(pe => pe.PlantName)
            .Select(g =>
            {
                var kwh      = g.Sum(pe => pe.KwhTotal);
                var co2      = kwh * _gridEmissionFactor;
                var wt       = treatmentByPlant.GetValueOrDefault(g.Key, 0);
                var co2pert  = wt > 0 ? co2 / (wt / 1000m) : 0;
                return new PlantEnergyComparisonDto(g.Key, kwh, co2, wt, co2pert);
            })
            .OrderByDescending(p => p.KwhTotal)
            .ToList();

        // ── Evolución mensual ─────────────────────────────────────────────────
        var monthlyEvolution = energyData
            .Where(pe => pe.Month.HasValue)
            .GroupBy(pe => new { pe.Year, Month = pe.Month!.Value, pe.PlantName })
            .Select(g =>
            {
                var kwh = g.Sum(pe => pe.KwhTotal);
                return new MonthlyPlantEnergyDto(g.Key.Year, g.Key.Month, g.Key.PlantName, kwh, 0);
            })
            .OrderBy(m => m.Year).ThenBy(m => m.Month)
            .ToList();

        // ── Por fuente energética ─────────────────────────────────────────────
        var totalKwh = energyData.Sum(pe => pe.KwhTotal);
        var bySource = energyData
            .GroupBy(pe => pe.Source ?? "Desconocida")
            .Select(g =>
            {
                var kwh = g.Sum(pe => pe.KwhTotal);
                return new EnergySourceBreakdownDto(g.Key, kwh, totalKwh > 0 ? (double)(kwh / totalKwh * 100) : 0);
            })
            .OrderByDescending(s => s.KwhTotal)
            .ToList();

        // ── Detalle ───────────────────────────────────────────────────────────
        var details = energyData.Select(pe =>
        {
            var co2kg    = pe.KwhTotal * _gridEmissionFactor;
            var wt       = treatmentByPlant.GetValueOrDefault(pe.PlantName, 0);
            var co2pert  = wt > 0 ? co2kg / (wt / 1000m) : 0;
            return new PlantEnergyDetailDto(
                pe.PlantName, pe.PlantCenterCode, pe.Year, pe.Month ?? 0,
                pe.KwhTotal, pe.Source, pe.GridMixRef, pe.AllocationMethod,
                wt, co2kg, co2pert);
        })
        .OrderBy(d => d.PlantName).ThenBy(d => d.Year).ThenBy(d => d.Month)
        .ToList();

        return new CarbonPlantEnergyDto(
            DateFrom:            request.DateFrom,
            DateTo:              request.DateTo,
            TotalKwhPeriod:      currentKwh,
            Scope2CO2eTonnes:    currentCO2 / 1000m,
            Scope2CO2ePerTonneKg: scope2CO2ePerTonne,
            VariationVsPreviousPct: variation,
            PlantComparison:     plantComparison,
            MonthlyEvolution:    monthlyEvolution,
            BySource:            bySource,
            Details:             details
        );
    }
}
