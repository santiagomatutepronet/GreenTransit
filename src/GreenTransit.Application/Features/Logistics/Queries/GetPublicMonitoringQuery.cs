using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Logistics.DTOs;
using GreenTransit.Domain.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.Logistics.Queries;

/// <summary>
/// Devuelve todos los KPIs del Dashboard 2 — Panel de Monitorización para Entidades Públicas.
/// Filtrado por OwnerId (multi-tenant). Para perfil PUBLIC_ENT, restringe los datos a los
/// traslados y liquidaciones vinculados a su LinkedEntityId.
/// Todas las queries con GroupBy proyectan a tipos anónimos antes de mapear a DTOs.
/// </summary>
public sealed record GetPublicMonitoringQuery(
    int     Year,
    int?    Month       = null,
    Guid?   IdScrap     = null,
    string? WasteStream = null
) : IRequest<PublicMonitoringDto>;

public sealed class GetPublicMonitoringQueryHandler
    : IRequestHandler<GetPublicMonitoringQuery, PublicMonitoringDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetPublicMonitoringQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<PublicMonitoringDto> Handle(
        GetPublicMonitoringQuery request, CancellationToken ct)
    {
        var ownerId        = _currentUser.OwnerId;
        var linkedEntityId = _currentUser.LinkedEntityId;
        var isPublicEnt    = _currentUser.IsInProfile(ProfileConstants.PublicEnt);

        // ── Rango temporal ────────────────────────────────────────────────────
        DateTime dateFrom, dateTo;
        if (request.Month.HasValue)
        {
            dateFrom = new DateTime(request.Year, request.Month.Value, 1, 0, 0, 0, DateTimeKind.Utc);
            dateTo   = dateFrom.AddMonths(1);
        }
        else
        {
            dateFrom = new DateTime(request.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            dateTo   = dateFrom.AddYears(1);
        }

        var prevFrom = request.Month.HasValue ? dateFrom.AddMonths(-1) : dateFrom.AddYears(-1);
        var prevTo   = dateFrom;

        // Parámetros capturados para uso en lambdas EF Core
        var scopeOwnerId        = ownerId;
        var scopeLinkedEntityId = linkedEntityId;
        var scopeIsPublicEnt    = isPublicEnt;
        var scopeFilterScrapId  = request.IdScrap;

        // ── 1. Servicios prestados por SCRAP ──────────────────────────────────
        // Partimos de WasteMoves JOIN ServiceOrders para obtener el IdScrap
        var wmBaseQuery = _context.WasteMoves
            .AsNoTracking()
            .Where(wm => (scopeOwnerId == Guid.Empty || wm.OwnerId == scopeOwnerId)
                      && wm.ServiceOrder != null
                      && wm.ServiceOrder.PlannedPickupStart >= dateFrom
                      && wm.ServiceOrder.PlannedPickupStart <  dateTo);

        if (scopeIsPublicEnt && scopeLinkedEntityId.HasValue)
        {
            var lid = scopeLinkedEntityId.Value;
            wmBaseQuery = wmBaseQuery.Where(wm =>
                wm.ServiceOrder != null && wm.ServiceOrder.IdIssuedBy == lid);
        }

        if (scopeFilterScrapId.HasValue)
        {
            var fid = scopeFilterScrapId.Value;
            wmBaseQuery = wmBaseQuery.Where(wm => wm.IdScrap == fid || wm.IdScrap2 == fid);
        }

        if (!string.IsNullOrEmpty(request.WasteStream))
            wmBaseQuery = wmBaseQuery.Where(wm =>
                wm.ServiceOrder != null && wm.ServiceOrder.WasteStream == request.WasteStream);

        var wmRaw = await wmBaseQuery
            .Select(wm => new
            {
                wm.IdScrap,
                Status  = wm.ServiceOrder!.Status,
                EstKg   = wm.ServiceOrder.EstimatedWeight
            })
            .ToListAsync(ct);

        // Obtener nombres de SCRAPs referenciados
        var scrapIds = wmRaw
            .Where(x => x.IdScrap.HasValue)
            .Select(x => x.IdScrap!.Value)
            .Distinct()
            .ToList();

        Dictionary<Guid, string> scrapNames = [];
        if (scrapIds.Count > 0)
        {
            var names = await _context.BusinessEntities
                .AsNoTracking()
                .Where(e => scrapIds.Contains(e.Id))
                .Select(e => new { e.Id, e.Name })
                .ToListAsync(ct);
            scrapNames = names.ToDictionary(e => e.Id, e => e.Name ?? e.Id.ToString());
        }

        var scrapServices = wmRaw
            .GroupBy(x => x.IdScrap)
            .Select(g =>
            {
                var sid  = g.Key;
                string name;
                if (sid.HasValue && scrapNames.TryGetValue(sid.Value, out var sn))
                    name = sn;
                else
                    name = "Sin asignar";
                return new ScrapServiceSummaryDto(
                    IdScrap:        sid ?? Guid.Empty,
                    ScrapName:      name,
                    TotalMoves:     g.Count(),
                    TotalKg:        g.Sum(x => x.EstKg ?? 0m),
                    PendingMoves:   g.Count(x => x.Status is "PENDIENTE" or "EN_PROGRESO"),
                    CompletedMoves: g.Count(x => x.Status is "COMPLETADO" or "CERRADO"),
                    CancelledMoves: g.Count(x => x.Status == "CANCELADO"));
            })
            .OrderByDescending(s => s.TotalKg)
            .ToList();

        // ── 2. Histórico mensual de recogidas ─────────────────────────────────
        // 24 meses hacia atrás para dar contexto histórico suficiente
        var histFrom = new DateTime(request.Year - 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var histTo   = dateTo;

        var epHistBaseQuery = _context.EntryPlants
            .AsNoTracking()
            .Where(ep => (scopeOwnerId == Guid.Empty || ep.OwnerId == scopeOwnerId)
                      && ep.PlantEntryDate >= histFrom
                      && ep.PlantEntryDate <  histTo
                      && ep.PlantEntryDate.HasValue);

        if (scopeIsPublicEnt && scopeLinkedEntityId.HasValue)
        {
            var lid = scopeLinkedEntityId.Value;
            epHistBaseQuery = epHistBaseQuery.Where(ep =>
                ep.WasteMove.ServiceOrder != null &&
                ep.WasteMove.ServiceOrder.IdIssuedBy == lid);
        }

        if (scopeFilterScrapId.HasValue)
        {
            var fid = scopeFilterScrapId.Value;
            epHistBaseQuery = epHistBaseQuery.Where(ep =>
                ep.WasteMove.IdScrap == fid || ep.WasteMove.IdScrap2 == fid);
        }

        var epHistRaw = await epHistBaseQuery
            .Select(ep => new
            {
                Year      = ep.PlantEntryDate!.Value.Year,
                Month     = ep.PlantEntryDate.Value.Month,
                NetWeight = ep.NetWeight ?? 0m,
                ScrapId   = ep.WasteMove.IdScrap
            })
            .ToListAsync(ct);

        // Nombres de SCRAPs del histórico
        var histScrapIds = epHistRaw
            .Where(x => x.ScrapId.HasValue)
            .Select(x => x.ScrapId!.Value)
            .Distinct()
            .Except(scrapNames.Keys)
            .ToList();

        if (histScrapIds.Count > 0)
        {
            var extraNames = await _context.BusinessEntities
                .AsNoTracking()
                .Where(e => histScrapIds.Contains(e.Id))
                .Select(e => new { e.Id, e.Name })
                .ToListAsync(ct);
            foreach (var item in extraNames)
                scrapNames[item.Id] = item.Name ?? item.Id.ToString();
        }

        var monthlyPickupHistory = epHistRaw
            .GroupBy(x => x.ScrapId)
            .Select(g =>
            {
                var sid  = g.Key;
                string name;
                if (sid.HasValue && scrapNames.TryGetValue(sid.Value, out var sn2))
                    name = sn2;
                else
                    name = "Sin asignar";
                var points = g
                    .GroupBy(x => new { x.Year, x.Month })
                    .Select(gm => new MonthlyPickupPointDto(gm.Key.Year, gm.Key.Month, Math.Round(gm.Sum(x => x.NetWeight), 2)))
                    .OrderBy(p => p.Year).ThenBy(p => p.Month)
                    .ToList();
                return new MonthlyPickupSeriesDto(sid ?? Guid.Empty, name, points);
            })
            .OrderByDescending(s => s.Points.Sum(p => p.TotalKg))
            .ToList();

        // ── 3. Liquidaciones ──────────────────────────────────────────────────
        var settlementsQuery = _context.Settlements
            .AsNoTracking()
            .Where(s => (scopeOwnerId == Guid.Empty || s.OwnerId == scopeOwnerId)
                     && s.Year == request.Year);

        if (scopeIsPublicEnt && scopeLinkedEntityId.HasValue)
            settlementsQuery = settlementsQuery.Where(s => s.IdPublicEntity == scopeLinkedEntityId.Value);

        if (request.Month.HasValue)
            settlementsQuery = settlementsQuery.Where(s => s.Month == request.Month.Value);

        if (scopeFilterScrapId.HasValue)
            settlementsQuery = settlementsQuery.Where(s => s.IdScrap == scopeFilterScrapId.Value);

        var settlementsRaw = await settlementsQuery
            .OrderByDescending(s => s.Year).ThenByDescending(s => s.Month)
            .Select(s => new
            {
                s.Id, s.SettlementNumber, s.Year, s.Month,
                s.Status, s.TotalAmount, s.Currency, s.ValidatedAt,
                s.IdScrap,
                ScrapName = s.Scrap != null ? s.Scrap.Name : null
            })
            .ToListAsync(ct);

        var settlements = settlementsRaw
            .Select(s => new SettlementRowDto(
                s.Id, s.SettlementNumber, s.Year, s.Month,
                s.Status, s.TotalAmount, s.Currency, s.ValidatedAt,
                s.IdScrap, s.ScrapName))
            .ToList();

        // ── 4. Comparativa de emisiones CO₂e ─────────────────────────────────
        var emissionsCurrentQuery = _context.WasteMoveResidues
            .AsNoTracking()
            .Where(r => (scopeOwnerId == Guid.Empty || r.WasteMove.OwnerId == scopeOwnerId)
                     && r.WasteMove.GatheredDate >= dateFrom
                     && r.WasteMove.GatheredDate <  dateTo
                     && r.TransportInfo_TransportCarbonEmissions != null);

        if (scopeIsPublicEnt && scopeLinkedEntityId.HasValue)
        {
            var lid = scopeLinkedEntityId.Value;
            emissionsCurrentQuery = emissionsCurrentQuery.Where(r =>
                r.WasteMove.ServiceOrder != null &&
                r.WasteMove.ServiceOrder.IdIssuedBy == lid);
        }

        var emCurrentRaw = await emissionsCurrentQuery
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalCO2e = g.Sum(r => r.TransportInfo_TransportCarbonEmissions ?? 0m),
                TotalKg   = g.Sum(r => r.Weight ?? 0m)
            })
            .FirstOrDefaultAsync(ct);

        var emPrevQuery = _context.WasteMoveResidues
            .AsNoTracking()
            .Where(r => (scopeOwnerId == Guid.Empty || r.WasteMove.OwnerId == scopeOwnerId)
                     && r.WasteMove.GatheredDate >= prevFrom
                     && r.WasteMove.GatheredDate <  prevTo
                     && r.TransportInfo_TransportCarbonEmissions != null);

        if (scopeIsPublicEnt && scopeLinkedEntityId.HasValue)
        {
            var lid = scopeLinkedEntityId.Value;
            emPrevQuery = emPrevQuery.Where(r =>
                r.WasteMove.ServiceOrder != null &&
                r.WasteMove.ServiceOrder.IdIssuedBy == lid);
        }

        var prevCO2e = await emPrevQuery
            .SumAsync(r => r.TransportInfo_TransportCarbonEmissions ?? 0m, ct);

        var currCO2e = emCurrentRaw?.TotalCO2e ?? 0m;
        var currKg   = emCurrentRaw?.TotalKg   ?? 0m;

        double? emTrend = prevCO2e > 0
            ? (double)Math.Round((currCO2e - prevCO2e) / prevCO2e * 100m, 1)
            : null;

        var emissions = new EmissionComparisonDto(
            CurrentCO2eKg:   Math.Round(currCO2e, 2),
            PreviousCO2eKg:  Math.Round(prevCO2e, 2),
            TrendPercent:    emTrend,
            CurrentTotalKg:  Math.Round(currKg, 2),
            CO2ePerTonne:    currKg > 0 ? Math.Round(currCO2e / (currKg / 1000m), 2) : 0m);

        // ── 5. Cumplimiento de objetivos municipales ──────────────────────────
        // Obtener comunidad autónoma (StateCode) de la entidad pública
        string? autonomousCommunity = null;
        if (scopeIsPublicEnt && scopeLinkedEntityId.HasValue)
        {
            autonomousCommunity = await _context.BusinessEntities
                .AsNoTracking()
                .Where(e => e.Id == scopeLinkedEntityId.Value)
                .Select(e => e.StateCode)
                .FirstOrDefaultAsync(ct);
        }

        var marketShareQuery = _context.MarketShares
            .AsNoTracking()
            .Where(ms => (scopeOwnerId == Guid.Empty || ms.OwnerId == scopeOwnerId)
                      && ms.Year == request.Year);

        if (!string.IsNullOrEmpty(autonomousCommunity))
            marketShareQuery = marketShareQuery.Where(ms => ms.AutonomousCommunity == autonomousCommunity);

        if (scopeFilterScrapId.HasValue)
            marketShareQuery = marketShareQuery.Where(ms => ms.IdScrap == scopeFilterScrapId.Value);

        var marketSharesRaw = await marketShareQuery
            .Select(ms => new { ms.IdScrap, ms.Weight, ms.AutonomousCommunity,
                ScrapName = ms.Scrap != null ? ms.Scrap.Name : null })
            .ToListAsync(ct);

        // Peso real entrado en planta en el año (por SCRAP)
        var realKgByScrapQuery = _context.EntryPlants
            .AsNoTracking()
            .Where(ep => (scopeOwnerId == Guid.Empty || ep.OwnerId == scopeOwnerId)
                      && ep.PlantEntryDate.HasValue
                      && ep.PlantEntryDate.Value.Year == request.Year);

        if (scopeFilterScrapId.HasValue)
        {
            var fid = scopeFilterScrapId.Value;
            realKgByScrapQuery = realKgByScrapQuery.Where(ep =>
                ep.WasteMove.IdScrap == fid || ep.WasteMove.IdScrap2 == fid);
        }

        var realKgRaw = await realKgByScrapQuery
            .GroupBy(ep => ep.WasteMove.IdScrap)
            .Select(g => new { ScrapId = g.Key, TotalKg = g.Sum(ep => ep.NetWeight ?? 0m) })
            .ToListAsync(ct);

        var realKgByScrap = realKgRaw.ToDictionary(x => x.ScrapId, x => x.TotalKg);

        var municipalTargets = marketSharesRaw
            .GroupBy(ms => ms.IdScrap)
            .Select(g =>
            {
                var sid      = g.Key;
                var target   = g.Sum(ms => ms.Weight);
                var real     = sid.HasValue && realKgByScrap.TryGetValue(sid, out var kg) ? kg : 0m;
                var pct      = target > 0 ? (double)Math.Round(real / target * 100m, 1) : 0d;
                var name     = g.First().ScrapName
                               ?? (sid.HasValue && scrapNames.TryGetValue(sid.Value, out var n) ? n : "Sin asignar");
                return new MunicipalTargetDto(
                    IdScrap:             sid ?? Guid.Empty,
                    ScrapName:           name,
                    AutonomousCommunity: g.First().AutonomousCommunity,
                    TargetKg:            Math.Round(target, 2),
                    RealKg:              Math.Round(real,   2),
                    CompliancePercent:   pct);
            })
            .OrderByDescending(t => t.CompliancePercent)
            .ToList();

        return new PublicMonitoringDto(
            ScrapServices:        scrapServices,
            MonthlyPickupHistory: monthlyPickupHistory,
            Settlements:          settlements,
            Emissions:            emissions,
            MunicipalTargets:     municipalTargets,
            Year:                 request.Year,
            Month:                request.Month);
    }
}
