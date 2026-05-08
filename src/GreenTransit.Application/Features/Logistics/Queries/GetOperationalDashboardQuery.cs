using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Logistics.DTOs;
using GreenTransit.Domain.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.Logistics.Queries;

/// <summary>
/// Devuelve el Panel Operativo (Dashboard 3) adaptado al perfil activo:
/// DISPATCH_OFFICE → W1-W4, CAC_OP → W5-W7, PLANT_OP → W8-W11.
/// ADMIN recibe todos los widgets.
/// Todas las queries con GroupBy proyectan a tipos anónimos antes de mapear a DTOs.
/// </summary>
public sealed record GetOperationalDashboardQuery(
    int     Year,
    int?    Month    = null,
    Guid?   IdEntity = null   // filtro opcional (ADMIN puede seleccionar entidad)
) : IRequest<OperationalDashboardDto>;

public sealed class GetOperationalDashboardQueryHandler
    : IRequestHandler<GetOperationalDashboardQuery, OperationalDashboardDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetOperationalDashboardQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<OperationalDashboardDto> Handle(
        GetOperationalDashboardQuery request, CancellationToken ct)
    {
        var ownerId        = _currentUser.OwnerId;
        var linkedEntityId = _currentUser.LinkedEntityId;
        var isDispatch     = _currentUser.IsInProfile(ProfileConstants.DispatchOffice);
        var isCacOp        = _currentUser.IsInProfile(ProfileConstants.CacOp);
        var isPlantOp      = _currentUser.IsInProfile(ProfileConstants.PlantOp);
        var isAdmin        = _currentUser.IsInProfile(ProfileConstants.Admin);

        // El entityId efectivo: filtro manual (ADMIN) o entidad vinculada
        var effectiveEntityId = request.IdEntity ?? linkedEntityId;

        // Rango temporal
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

        var now    = DateTime.UtcNow;
        var today  = now.Date;
        var week7  = today.AddDays(7);

        // Parámetros capturados para lambdas EF
        var scopeOwnerId      = ownerId;
        var scopeEntityId     = effectiveEntityId;

        // Determina qué perfil usar para la etiqueta del DTO
        var activeProfile = isDispatch ? ProfileConstants.DispatchOffice
                          : isCacOp   ? ProfileConstants.CacOp
                          : isPlantOp ? ProfileConstants.PlantOp
                          :             ProfileConstants.Admin;

        // ── W1: SO pendientes de planificar (DISPATCH_OFFICE / ADMIN) ─────────
        List<PendingServiceOrderDto> pendingServiceOrders = [];
        if (isDispatch || isAdmin)
        {
            var pendingRaw = await _context.ServiceOrders
                .AsNoTracking()
                .Where(so => (scopeOwnerId == Guid.Empty || so.OwnerId == scopeOwnerId)
                          && so.Status == "PENDIENTE")
                .OrderBy(so => so.Priority == "Alta" ? 0 : so.Priority == "Media" ? 1 : 2)
                .ThenBy(so => so.PlannedPickupStart)
                .Take(50)
                .Select(so => new
                {
                    so.Id,
                    so.ServiceOrderNumber,
                    so.Priority,
                    so.WasteStream,
                    so.PlannedPickupStart,
                    PickupName = so.PickupPoint != null ? so.PickupPoint.Name : null
                })
                .ToListAsync(ct);

            pendingServiceOrders = pendingRaw
                .Select(s => new PendingServiceOrderDto(
                    s.Id, s.ServiceOrderNumber, s.Priority ?? "Normal",
                    s.WasteStream, s.PlannedPickupStart, s.PickupName))
                .ToList();
        }

        // ── W2: Embudo traslados por estado (DISPATCH_OFFICE / ADMIN) ─────────
        List<WasteMoveFunnelItemDto> wasteMovesFunnel = [];
        if (isDispatch || isAdmin)
        {
            var funnelRaw = await _context.WasteMoves
                .AsNoTracking()
                .Where(wm => (scopeOwnerId == Guid.Empty || wm.OwnerId == scopeOwnerId)
                          && wm.GatheredDate >= dateFrom
                          && wm.GatheredDate <  dateTo)
                .GroupBy(wm => wm.ServiceStatus)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var order = new[] { "PLANIFICADO", "ASIGNADO", "EN_RUTA", "RECOGIDO", "ENTREGADO", "CANCELADO" };
            wasteMovesFunnel = funnelRaw
                .Select(x => new WasteMoveFunnelItemDto(x.Status ?? "SIN_ESTADO", x.Count))
                .OrderBy(x => Array.IndexOf(order, x.ServiceStatus) is var i && i >= 0 ? i : 99)
                .ToList();
        }

        // ── W3: Planificación semanal próximos 7 días (DISPATCH_OFFICE / ADMIN) ─
        List<WeeklyPlanItemDto> weeklyPlan = [];
        if (isDispatch || isAdmin)
        {
            var todayUtc = today.ToUniversalTime();
            var week7Utc = week7.ToUniversalTime();

            var planRaw = await _context.ServiceOrders
                .AsNoTracking()
                .Where(so => (scopeOwnerId == Guid.Empty || so.OwnerId == scopeOwnerId)
                          && so.PlannedPickupStart >= todayUtc
                          && so.PlannedPickupStart <  week7Utc)
                .Select(so => new { so.PlannedPickupStart, so.Priority })
                .ToListAsync(ct);

            weeklyPlan = planRaw
                .GroupBy(x => x.PlannedPickupStart!.Value.Date)
                .Select(g => new WeeklyPlanItemDto(
                    g.Key,
                    g.Count(),
                    g.Any(x => x.Priority == "Alta") ? "Alta"
                    : g.Any(x => x.Priority == "Media") ? "Media" : "Normal"))
                .OrderBy(x => x.Date)
                .ToList();
        }

        // ── W4: Incidencias abiertas (DISPATCH_OFFICE / ADMIN) ────────────────
        List<OpenIncidentRowDto> openIncidents = [];
        if (isDispatch || isAdmin)
        {
            var incRaw = await _context.Incidents
                .AsNoTracking()
                .Where(i => (scopeOwnerId == Guid.Empty || i.OwnerId == scopeOwnerId)
                         && i.ClosedAt == null)
                .OrderByDescending(i => i.Severity == "Critical" ? 3
                               : i.Severity == "High" ? 2
                               : i.Severity == "Medium" ? 1 : 0)
                .ThenBy(i => i.OpenedAt)
                .Take(30)
                .Select(i => new
                {
                    i.Id, i.WasteMoveReference, i.Type,
                    i.Severity, i.OpenedAt, i.Description
                })
                .ToListAsync(ct);

            openIncidents = incRaw
                .Select(i => new OpenIncidentRowDto(
                    i.Id, i.WasteMoveReference, i.Type, i.Severity,
                    i.OpenedAt, (int)(now - i.OpenedAt).TotalDays, i.Description))
                .ToList();
        }

        // ── W5: Entradas en CAC hoy (CAC_OP / ADMIN) ─────────────────────────
        List<CacEntryTodayDto> cacEntriesToday = [];
        if (isCacOp || isAdmin)
        {
            var todayMin = new DateTime(today.Year, today.Month, today.Day, 0, 0, 0, DateTimeKind.Utc);
            var todayMax = todayMin.AddDays(1);

            var cacQuery = _context.EntryCACs
                .AsNoTracking()
                .Where(e => (scopeOwnerId == Guid.Empty || e.OwnerId == scopeOwnerId)
                         && e.CACEntryDate >= todayMin
                         && e.CACEntryDate <  todayMax);

            if ((isCacOp && !isAdmin) && scopeEntityId.HasValue)
            {
                var eid = scopeEntityId.Value;
                cacQuery = cacQuery.Where(e =>
                    e.WasteMove.IdDestination == eid || e.WasteMove.IdSource == eid);
            }

            var cacRaw = await cacQuery
                .Select(e => new
                {
                    e.Id,
                    e.WasteMoveReference,
                    e.CollectionMethod,
                    TotalKg = e.EntryCACResidues.Sum(r => r.Weight ?? 0m)
                })
                .ToListAsync(ct);

            cacEntriesToday = cacRaw
                .Select(e => new CacEntryTodayDto(
                    e.Id, e.WasteMoveReference, e.TotalKg, e.CollectionMethod))
                .OrderByDescending(e => e.TotalKg)
                .ToList();
        }

        // ── W6: Stock acumulado CAC por residuo (CAC_OP / ADMIN) ─────────────
        List<CacStockByResidueDto> cacStockByResidue = [];
        if (isCacOp || isAdmin)
        {
            var stockQuery = _context.EntryCACResidues
                .AsNoTracking()
                .Where(r => (scopeOwnerId == Guid.Empty || r.EntryCAC.OwnerId == scopeOwnerId)
                         && r.EntryCAC.CACEntryDate >= dateFrom
                         && r.EntryCAC.CACEntryDate <  dateTo);

            if ((isCacOp && !isAdmin) && scopeEntityId.HasValue)
            {
                var eid = scopeEntityId.Value;
                stockQuery = stockQuery.Where(r =>
                    r.EntryCAC.WasteMove.IdDestination == eid ||
                    r.EntryCAC.WasteMove.IdSource      == eid);
            }

            var stockRaw = await stockQuery
                .GroupBy(r => r.Residue != null ? r.Residue.Name : null)
                .Select(g => new
                {
                    ResidueName = g.Key,
                    TotalKg     = g.Sum(r => r.Weight ?? 0m),
                    Entries     = g.Count()
                })
                .OrderByDescending(x => x.TotalKg)
                .ToListAsync(ct);

            cacStockByResidue = stockRaw
                .Select(x => new CacStockByResidueDto(x.ResidueName, x.TotalKg, x.Entries))
                .ToList();
        }

        // ── W7: Tickets pendientes de pesaje en CAC (CAC_OP / ADMIN) ─────────
        int cacTicketsPending = 0;
        if (isCacOp || isAdmin)
        {
            var pendQuery = _context.EntryCACs
                .AsNoTracking()
                .Where(e => (scopeOwnerId == Guid.Empty || e.OwnerId == scopeOwnerId)
                         && e.CACEntryDate >= dateFrom
                         && e.CACEntryDate <  dateTo
                         && !e.EntryCACResidues.Any(r => r.Weight != null && r.Weight > 0));

            if ((isCacOp && !isAdmin) && scopeEntityId.HasValue)
            {
                var eid = scopeEntityId.Value;
                pendQuery = pendQuery.Where(e =>
                    e.WasteMove.IdDestination == eid || e.WasteMove.IdSource == eid);
            }

            cacTicketsPending = await pendQuery.CountAsync(ct);
        }

        // ── W8: Entradas en planta hoy (PLANT_OP / ADMIN) ────────────────────
        List<PlantEntryTodayDto> plantEntriesToday = [];
        if (isPlantOp || isAdmin)
        {
            var todayMin = new DateTime(today.Year, today.Month, today.Day, 0, 0, 0, DateTimeKind.Utc);
            var todayMax = todayMin.AddDays(1);

            var epQuery = _context.EntryPlants
                .AsNoTracking()
                .Where(ep => (scopeOwnerId == Guid.Empty || ep.OwnerId == scopeOwnerId)
                          && ep.PlantEntryDate >= todayMin
                          && ep.PlantEntryDate <  todayMax);

            if ((isPlantOp && !isAdmin) && scopeEntityId.HasValue)
            {
                var eid = scopeEntityId.Value;
                epQuery = epQuery.Where(ep =>
                    ep.WasteMove.IdDestination == eid);
            }

            var epRaw = await epQuery
                .Select(ep => new
                {
                    ep.Id, ep.WasteMoveReference, ep.TicketScale,
                    ep.NetWeight, ep.GrossWeight, ep.TareWeight
                })
                .ToListAsync(ct);

            plantEntriesToday = epRaw
                .Select(ep => new PlantEntryTodayDto(
                    ep.Id, ep.WasteMoveReference, ep.TicketScale,
                    ep.NetWeight, ep.GrossWeight, ep.TareWeight))
                .ToList();
        }

        // ── W9: Balance de tratamiento (PLANT_OP / ADMIN) ────────────────────
        var treatmentBalance = new TreatmentBalanceDto(0, 0, 0, 0, 0);
        if (isPlantOp || isAdmin)
        {
            var tpQuery = _context.TreatmentPlantResidues
                .AsNoTracking()
                .Where(r => (scopeOwnerId == Guid.Empty ||
                             r.TreatmentPlant.OwnerId == scopeOwnerId)
                         && r.TreatmentPlant.PlantTreatmentDate >= dateFrom
                         && r.TreatmentPlant.PlantTreatmentDate <  dateTo);

            if ((isPlantOp && !isAdmin) && scopeEntityId.HasValue)
            {
                var eid = scopeEntityId.Value;
                tpQuery = tpQuery.Where(r =>
                    r.TreatmentPlant.WasteMove != null &&
                    r.TreatmentPlant.WasteMove.IdDestination == eid);
            }

            var balanceRaw = await tpQuery
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    TotalIn  = g.Sum(r => r.WeightTotal  ?? 0m),
                    Reused   = g.Sum(r => r.WeightReused ?? 0m),
                    Valued   = g.Sum(r => r.WeightValued ?? 0m),
                    Removed  = g.Sum(r => r.WeightRemove ?? 0m)
                })
                .FirstOrDefaultAsync(ct);

            if (balanceRaw is not null)
            {
                var recycled = balanceRaw.Reused + balanceRaw.Valued;
                var rate     = balanceRaw.TotalIn > 0
                    ? (double)Math.Round(recycled / balanceRaw.TotalIn * 100m, 1)
                    : 0d;
                treatmentBalance = new TreatmentBalanceDto(
                    Math.Round(balanceRaw.TotalIn, 2),
                    Math.Round(balanceRaw.Reused,  2),
                    Math.Round(balanceRaw.Valued,  2),
                    Math.Round(balanceRaw.Removed, 2),
                    rate);
            }
        }

        // ── W10: Impropios detectados (PLANT_OP / ADMIN) ──────────────────────
        decimal improperWeightKg = 0m;
        if (isPlantOp || isAdmin)
        {
            var impQuery = _context.TreatmentPlants
                .AsNoTracking()
                .Where(tp => (scopeOwnerId == Guid.Empty || tp.OwnerId == scopeOwnerId)
                          && tp.PlantTreatmentDate >= dateFrom
                          && tp.PlantTreatmentDate <  dateTo
                          && tp.ImproperWeight != null);

            if ((isPlantOp && !isAdmin) && scopeEntityId.HasValue)
            {
                var eid = scopeEntityId.Value;
                impQuery = impQuery.Where(tp =>
                    tp.WasteMove != null && tp.WasteMove.IdDestination == eid);
            }

            improperWeightKg = Math.Round(
                await impQuery.SumAsync(tp => tp.ImproperWeight ?? 0m, ct), 2);
        }

        // ── W11: Incidencias de planta abiertas (PLANT_OP / ADMIN) ───────────
        List<OpenIncidentRowDto> plantOpenIncidents = [];
        if (isPlantOp || isAdmin)
        {
            var pIncRaw = await _context.Incidents
                .AsNoTracking()
                .Where(i => (scopeOwnerId == Guid.Empty || i.OwnerId == scopeOwnerId)
                         && i.ClosedAt == null)
                .OrderByDescending(i => i.OpenedAt)
                .Take(20)
                .Select(i => new
                {
                    i.Id, i.WasteMoveReference, i.Type,
                    i.Severity, i.OpenedAt, i.Description
                })
                .ToListAsync(ct);

            plantOpenIncidents = pIncRaw
                .Select(i => new OpenIncidentRowDto(
                    i.Id, i.WasteMoveReference, i.Type, i.Severity,
                    i.OpenedAt, (int)(now - i.OpenedAt).TotalDays, i.Description))
                .ToList();
        }

        return new OperationalDashboardDto(
            PendingServiceOrders: pendingServiceOrders,
            WasteMovesFunnel:     wasteMovesFunnel,
            WeeklyPlan:           weeklyPlan,
            OpenIncidents:        openIncidents,
            CacEntriesToday:      cacEntriesToday,
            CacStockByResidue:    cacStockByResidue,
            CacTicketsPending:    cacTicketsPending,
            PlantEntriesToday:    plantEntriesToday,
            TreatmentBalance:     treatmentBalance,
            ImproperWeightKg:     improperWeightKg,
            PlantOpenIncidents:   plantOpenIncidents,
            ActiveProfile:        activeProfile,
            Year:                 request.Year,
            Month:                request.Month);
    }
}
