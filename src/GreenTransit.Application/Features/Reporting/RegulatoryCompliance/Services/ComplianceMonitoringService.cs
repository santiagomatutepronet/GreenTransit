using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Reporting.RegulatoryCompliance.DTOs;
using Microsoft.Extensions.Configuration;

namespace GreenTransit.Application.Features.Reporting.RegulatoryCompliance.Services;

/// <summary>
/// Motor centralizado de alertas y cálculo de desviaciones para el módulo
/// de Análisis y Cumplimiento Normativo (UC-CN).
/// Los umbrales son configurables desde appsettings.json bajo la sección "ComplianceThresholds".
/// </summary>
public sealed class ComplianceMonitoringService
{
    private readonly IApplicationDbContext _db;

    // Umbrales cacheados en el constructor (no parsear IConfiguration en cada acceso)
    private readonly double _expiryAlertDays;
    private readonly double _expiryCriticalDays;
    private readonly double _marketShareAlertPct;
    private readonly double _deviationAlertPct;

    private double ExpiryAlertDays     => _expiryAlertDays;
    private double ExpiryCriticalDays  => _expiryCriticalDays;
    private double MarketShareAlertPct => _marketShareAlertPct;
    private double DeviationAlertPct   => _deviationAlertPct;

    public ComplianceMonitoringService(
        IApplicationDbContext db,
        IConfiguration        config)
    {
        _db                  = db;
        _expiryAlertDays     = double.TryParse(config["ComplianceThresholds:ExpiryAlertDays"],    out var v1) ? v1 : 90d;
        _expiryCriticalDays  = double.TryParse(config["ComplianceThresholds:ExpiryCriticalDays"], out var v2) ? v2 : 30d;
        _marketShareAlertPct = double.TryParse(config["ComplianceThresholds:MarketShareAlertPct"],out var v3) ? v3 : 80d;
        _deviationAlertPct   = double.TryParse(config["ComplianceThresholds:DeviationAlertPct"],  out var v4) ? v4 : 15d;
    }

    /// <summary>
    /// Genera alertas de cumplimiento para un SCRAP concreto en un año.
    /// <paramref name="realWeightByCategory"/> es el diccionario ya calculado por el caller
    /// para evitar la doble consulta a EntryPlants.
    /// </summary>
    public async Task<IReadOnlyList<ComplianceAlertDto>> GetScrapAlertsAsync(
        Guid    scrapId,
        Guid    ownerId,
        int     year,
        CancellationToken ct,
        Dictionary<string, decimal>? realWeightByCategory = null)
    {
        var alerts = new List<ComplianceAlertDto>();
        var now    = DateTime.UtcNow;

        // ── Convenios próximos a vencer ───────────────────────────────────────
        var expiringAgreements = await _db.Agreements.AsNoTracking()
            .Where(a => a.IdScrap == scrapId
                     && a.OwnerId == ownerId
                     && a.Status  == "Active"
                     && a.EffectiveTo.HasValue
                     && a.EffectiveTo.Value > now)
            .Select(a => new { a.AgreementNumber, a.EffectiveTo, EntityId = a.IdPublicEntity })
            .ToListAsync(ct);

        foreach (var ag in expiringAgreements)
        {
            var days = (ag.EffectiveTo!.Value - now).TotalDays;
            if (days <= ExpiryCriticalDays)
                alerts.Add(new ComplianceAlertDto("AGREEMENT_EXPIRY", "HIGH",
                    $"Convenio {ag.AgreementNumber} vence en {(int)days} días.", null, ag.EntityId, now));
            else if (days <= ExpiryAlertDays)
                alerts.Add(new ComplianceAlertDto("AGREEMENT_EXPIRY", "MEDIUM",
                    $"Convenio {ag.AgreementNumber} vence en {(int)days} días.", null, ag.EntityId, now));
        }

        // ── Liquidaciones rechazadas ──────────────────────────────────────────
        var rejectedSettlements = await _db.Settlements.AsNoTracking()
            .Where(s => s.IdScrap  == scrapId
                     && s.OwnerId  == ownerId
                     && s.Year     == year
                     && s.ValidationStatus == "Rejected")
            .Select(s => new { s.SettlementNumber, s.IdPublicEntity })
            .ToListAsync(ct);

        foreach (var st in rejectedSettlements)
            alerts.Add(new ComplianceAlertDto("SETTLEMENT_REJECTED", "HIGH",
                $"Liquidación {st.SettlementNumber} ha sido rechazada.", null, st.IdPublicEntity, now));

        // ── Cuotas de mercado en riesgo (<80% a fecha proporcional) ──────────
        var monthsElapsed = Math.Max(1, now.Month);
        var proRatedFactor = (decimal)monthsElapsed / 12m;

        var marketShares = await _db.MarketShares.AsNoTracking()
            .Where(ms => ms.IdScrap == scrapId
                      && ms.OwnerId == ownerId
                      && ms.Year    == year)
            .ToListAsync(ct);

        // Pesos reales acumulados para el SCRAP este año — reutiliza el dict del caller si se pasa
        if (realWeightByCategory is null)
        {
            realWeightByCategory = await _db.EntryPlants.AsNoTracking()
                .Where(ep => ep.WasteMove.OwnerId == ownerId
                          && (ep.WasteMove.IdScrap == scrapId || ep.WasteMove.IdScrap2 == scrapId)
                          && ep.WasteMove.ActualPickupStart.HasValue
                          && ep.WasteMove.ActualPickupStart.Value.Year == year)
                .SelectMany(ep => ep.EntryPlantResidues)
                .GroupBy(epr => epr.Residue.ProductCategory ?? "")
                .Select(g => new { Category = g.Key, WeightKg = g.Sum(r => r.Weight) })
                .ToDictionaryAsync(x => x.Category, x => (decimal)(x.WeightKg ?? 0m), ct);
        }

        foreach (var ms in marketShares)
        {
            var proRated = ms.Weight * proRatedFactor;
            var real     = realWeightByCategory.GetValueOrDefault(ms.Category ?? "", 0m);
            if (proRated > 0 && (double)(real / proRated * 100) < MarketShareAlertPct)
                alerts.Add(new ComplianceAlertDto("MARKET_SHARE_RISK", "MEDIUM",
                    $"Cuota de mercado para categoría '{ms.Category}' en {ms.AutonomousCommunity} al {(real / proRated * 100):N1}% del objetivo proporcional.",
                    ms.AutonomousCommunity, null, now));
        }

        return alerts;
    }

    /// <summary>
    /// Genera alertas para el coordinador/oficina de asignación (transversales a varios SCRAPs).
    /// </summary>
    public async Task<IReadOnlyList<ComplianceAlertDto>> GetCoordinatorAlertsAsync(
        Guid    coordinatorId,
        Guid    ownerId,
        int     year,
        CancellationToken ct)
    {
        var alerts = new List<ComplianceAlertDto>();
        var now    = DateTime.UtcNow;

        // Obtener SCRAPs del coordinador
        var scrapIds = await _db.Agreements.AsNoTracking()
            .Where(a => a.IdCoordinator == coordinatorId && a.OwnerId == ownerId)
            .Select(a => a.IdScrap)
            .Distinct()
            .ToListAsync(ct);

        // Convenios próximos a vencer
        var expiringAgreements = await _db.Agreements.AsNoTracking()
            .Where(a => a.IdCoordinator == coordinatorId
                     && a.OwnerId      == ownerId
                     && a.Status       == "Active"
                     && a.EffectiveTo.HasValue
                     && a.EffectiveTo.Value > now
                     && a.EffectiveTo.Value <= now.AddDays(ExpiryAlertDays))
            .Select(a => new { a.AgreementNumber, a.EffectiveTo, EntityId = a.IdPublicEntity })
            .ToListAsync(ct);

        foreach (var ag in expiringAgreements)
        {
            var days = (ag.EffectiveTo!.Value - now).TotalDays;
            var severity = days <= ExpiryCriticalDays ? "HIGH" : "MEDIUM";
            alerts.Add(new ComplianceAlertDto("AGREEMENT_EXPIRY", severity,
                $"Convenio {ag.AgreementNumber} vence en {(int)days} días.", null, ag.EntityId, now));
        }

        // Liquidaciones pendientes (> 30 días sin resolución)
        var pendingSettlements = await _db.Settlements.AsNoTracking()
            .Where(s => scrapIds.Contains(s.IdScrap)
                     && s.OwnerId == ownerId
                     && s.Year    == year
                     && s.ValidationStatus == "Pending"
                     && s.CreatedAt < now.AddDays(-30))
            .Select(s => new { s.SettlementNumber })
            .ToListAsync(ct);

        foreach (var st in pendingSettlements)
            alerts.Add(new ComplianceAlertDto("SETTLEMENT_PENDING", "MEDIUM",
                $"Liquidación {st.SettlementNumber} lleva más de 30 días pendiente.", null, null, now));

        return alerts;
    }

    /// <summary>
    /// Calcula el semáforo de cumplimiento según el porcentaje y los objetivos regulatorios.
    /// </summary>
    public static string GetStatus(decimal pct, decimal target)
    {
        if (pct >= target)          return "GREEN";
        if (pct >= target * 0.80m)  return "ORANGE";
        return "RED";
    }

    /// <summary>
    /// Calcula el semáforo de vencimiento de un convenio.
    /// </summary>
    public string GetExpiryStatus(DateTime? effectiveTo)
    {
        if (effectiveTo is null) return "GREEN";
        var days = (effectiveTo.Value - DateTime.UtcNow).TotalDays;
        if (days <= ExpiryCriticalDays) return "RED";
        if (days <= ExpiryAlertDays)    return "ORANGE";
        return "GREEN";
    }
}
