namespace GreenTransit.Application.Features.Reporting.RegulatoryCompliance.DTOs;

// ── Compartidos ───────────────────────────────────────────────────────────────

public sealed record ComplianceAlertDto(
    string  AlertType,
    string  Severity,           // "HIGH" | "MEDIUM" | "LOW"
    string  Message,
    string? RelatedEntityName,
    Guid?   RelatedEntityId,
    DateTime GeneratedAt
);

public sealed record RecyclingRateByFlowDto(
    string  FlowType,
    string  Category,
    decimal RecyclingPct,
    decimal ValorizationPct,
    decimal ReusePct,
    decimal TargetRecyclingPct,
    bool    IsAtRisk
);

// ── CN-A: Panel Cumplimiento Normativo — Visión SCRAP ────────────────────────

public sealed record ScrapComplianceOverviewDto(
    int     Year,
    int?    Quarter,
    int?    Month,
    // KPIs
    decimal RecyclingRatePct,
    decimal ValorizationRatePct,
    decimal ReuseRatePct,
    decimal MarketShareCompliancePct,
    int     ActiveAgreements,
    double  VariationRecyclingVsPrevPct,
    double  VariationValorizationVsPrevPct,
    double  VariationMarketShareVsPrevPct,
    // Semáforo: "GREEN" | "ORANGE" | "RED"
    string  RecyclingStatus,
    string  ValorizationStatus,
    string  MarketShareStatus,
    // Gráfico: evolución trimestral
    IReadOnlyList<QuarterlyComplianceTrendDto>   QuarterlyTrend,
    // Tabla: cuotas por categoría y CCAA
    IReadOnlyList<MarketShareRowDto>             MarketShareRows,
    // Tabla: convenios
    IReadOnlyList<AgreementSummaryRowDto>        Agreements,
    // Gráfico + tabla: liquidaciones
    IReadOnlyList<SettlementMonthlyBarDto>       SettlementMonthly,
    IReadOnlyList<SettlementRowDto>              Settlements,
    // Alertas
    IReadOnlyList<ComplianceAlertDto>            Alerts
);

public sealed record QuarterlyComplianceTrendDto(
    int     Year,
    int     Quarter,
    decimal RecyclingPct,
    decimal ValorizationPct,
    decimal ReusePct,
    decimal TargetRecyclingPct
);

public sealed record MarketShareRowDto(
    string  Category,
    string  AutonomousCommunity,
    string  FlowType,
    decimal TargetWeightKg,
    decimal RealWeightKg,
    decimal CompliancePct,
    string  Status,             // "GREEN" | "ORANGE" | "RED"
    bool    IsAtRisk,
    IReadOnlyList<decimal> MonthlySparkline
);

public sealed record AgreementSummaryRowDto(
    Guid    Id,
    string  AgreementNumber,
    string  PublicEntityName,
    string  AutonomousCommunity,
    string  ProvinceName,
    string  MunicipalityName,
    string  WasteStream,
    string  Status,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    int     DaysToExpiry,
    string  ExpiryStatus        // "GREEN" | "ORANGE" | "RED"
);

public sealed record SettlementMonthlyBarDto(
    int     Year,
    int     Month,
    decimal PendingAmount,
    decimal ApprovedAmount,
    decimal RejectedAmount
);

public sealed record SettlementRowDto(
    Guid    Id,
    string  SettlementNumber,
    int     Year,
    int     Month,
    string  PublicEntityName,
    decimal TotalAmount,
    string  Currency,
    string  ValidationStatus,
    DateTime? ValidatedAt
);

// ── CN-B: Auditoría de Cuotas de Mercado ────────────────────────────────────

public sealed record MarketShareAuditDto(
    int     Year,
    // Donut: proporción por SCRAP
    IReadOnlyList<ScrapShareSliceDto>            ScrapShares,
    // Tabla resumen por SCRAP
    IReadOnlyList<ScrapShareSummaryDto>          ScrapSummaries,
    // Heatmap SCRAP x Categoría
    IReadOnlyList<ScrapCategoryHeatmapRowDto>    Heatmap,
    // Stacked area: evolución mensual
    IReadOnlyList<MonthlyScrapWeightDto>         MonthlyEvolution,
    // Tabla expandible por CCAA × SCRAP
    IReadOnlyList<TerritorialBreakdownDto>       TerritorialBreakdown,
    // Bar horizontal: índice de desviación
    IReadOnlyList<DeviationIndexDto>             DeviationIndex
);

public sealed record ScrapShareSliceDto(
    Guid    ScrapId,
    string  ScrapName,
    decimal TargetWeightKg,
    double  Pct
);

public sealed record ScrapShareSummaryDto(
    Guid    ScrapId,
    string  ScrapName,
    decimal TargetWeightKg,
    decimal RealWeightKg,
    decimal CompliancePct,
    decimal DeviationKg,
    decimal DeviationPct
);

public sealed record ScrapCategoryHeatmapRowDto(
    Guid    ScrapId,
    string  ScrapName,
    IReadOnlyList<CategoryComplianceCellDto> Cells
);

public sealed record CategoryComplianceCellDto(
    string  Category,
    decimal CompliancePct,
    string  Status          // "GREEN" | "ORANGE" | "RED"
);

public sealed record MonthlyScrapWeightDto(
    int     Year,
    int     Month,
    Guid    ScrapId,
    string  ScrapName,
    decimal RealWeightKg,
    decimal TargetWeightKg
);

public sealed record TerritorialBreakdownDto(
    string  AutonomousCommunity,
    Guid    ScrapId,
    string  ScrapName,
    string  Category,
    decimal TargetWeightKg,
    decimal RealWeightKg,
    decimal CompliancePct,
    string  Status
);

public sealed record DeviationIndexDto(
    Guid    ScrapId,
    string  ScrapName,
    decimal DeviationPct,
    string  Color           // "GREEN" | "RED"
);

// ── CN-C: Monitorización de Convenios — Coordinador ──────────────────────────

public sealed record AgreementComplianceMonitoringDto(
    int     Year,
    int?    Month,
    // KPIs
    int     TotalActiveAgreements,
    int     AgreementsExpiringSoon,
    int     PendingSettlements,
    decimal TotalApprovedAmountYear,
    double  VariationVsPrevYearPct,
    // Gráfico: mapa de cobertura por CCAA
    IReadOnlyList<AgreementCoverageByRegionDto>  CoverageByRegion,
    // Tabla: convenios por SCRAP y entidad pública
    IReadOnlyList<AgreementDetailRowDto>         AgreementRows,
    // Gráfico + tabla: liquidaciones por convenio
    IReadOnlyList<SettlementMonthlyByScrapDto>   SettlementMonthlyByScrap,
    IReadOnlyList<SettlementDetailRowDto>        SettlementRows,
    // Tabla: servicios vs compromisos
    IReadOnlyList<ServiceVsCommitmentsDto>       ServiceVsCommitments,
    // Alertas
    IReadOnlyList<ComplianceAlertDto>            Alerts
);

public sealed record AgreementCoverageByRegionDto(
    string  AutonomousCommunity,
    Guid    ScrapId,
    string  ScrapName,
    int     ActiveAgreements,
    decimal TotalTonnesManaged
);

public sealed record AgreementDetailRowDto(
    Guid    Id,
    string  AgreementNumber,
    string  ScrapName,
    string  PublicEntityName,
    string  AutonomousCommunity,
    string  ProvinceName,
    string  MunicipalityName,
    string  WasteStream,
    string? SubStream,
    string  Status,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    string  TariffModelType
);

public sealed record SettlementMonthlyByScrapDto(
    int     Year,
    int     Month,
    Guid    ScrapId,
    string  ScrapName,
    decimal TotalAmount
);

public sealed record SettlementDetailRowDto(
    Guid    Id,
    string  SettlementNumber,
    string  ScrapName,
    string  PublicEntityName,
    int     Year,
    int     Month,
    decimal BaseAmount,
    decimal AdjustmentsAmount,
    decimal TaxAmount,
    decimal TotalAmount,
    string  ValidationStatus
);

public sealed record ServiceVsCommitmentsDto(
    Guid    AgreementId,
    string  AgreementNumber,
    string  ScrapName,
    string  PublicEntityName,
    int     ServicesCompleted,
    decimal TonnesManaged,
    decimal? MinimumTonnes,
    int?    MinimumServices,
    string  ComplianceStatus    // "GREEN" | "RED"
);

// ── CN-D: Cumplimiento Normativo — Entidad Pública ───────────────────────────

public sealed record PublicEntityComplianceViewDto(
    int     Year,
    int?    Month,
    // KPIs
    decimal TotalTonnesCollected,
    int     ServicesCompleted,
    int     ScrapCount,
    decimal TotalCompensatedAmount,
    double  VariationVsPrevPeriodPct,
    // Gráfico: evolución mensual por SCRAP
    IReadOnlyList<MonthlyCollectionByScrapDto>   MonthlyByScrap,
    // Tabla: cumplimiento por SCRAP
    IReadOnlyList<ScrapTerritorialComplianceDto> ScrapCompliance,
    // Tabla: liquidaciones de compensación
    IReadOnlyList<CompensationSettlementRowDto>  CompensationSettlements,
    // Donut: toneladas por método de recolección
    IReadOnlyList<CollectionMethodSliceDto>      CollectionMethods,
    // Tabla: incidencias
    IReadOnlyList<IncidentRowDto>                Incidents,
    int                                          OpenIncidentsCount
);

public sealed record MonthlyCollectionByScrapDto(
    int     Year,
    int     Month,
    Guid    ScrapId,
    string  ScrapName,
    decimal WeightKg,
    decimal TargetWeightKg
);

public sealed record ScrapTerritorialComplianceDto(
    Guid    ScrapId,
    string  ScrapName,
    string  Category,
    string  FlowType,
    decimal TargetWeightKg,
    decimal RealWeightKg,
    decimal CompliancePct,
    string  Status
);

public sealed record CompensationSettlementRowDto(
    Guid    Id,
    string  ScrapName,
    string  SettlementNumber,
    int     Year,
    int     Month,
    decimal BaseAmount,
    decimal AdjustmentsAmount,
    decimal TotalAmount,
    string  Status,
    DateTime? ValidatedAt
);

public sealed record CollectionMethodSliceDto(
    string  Method,
    decimal WeightKg,
    double  Pct
);

public sealed record IncidentRowDto(
    Guid    Id,
    string  Type,
    string  Severity,
    string? WasteMoveReference,
    string? ScrapName,
    DateTime OpenedAt,
    int     DaysOpen
);

// ── CN-E: Datos de Cumplimiento — Oficina de Asignación ──────────────────────

public sealed record DispatchOfficeComplianceDataDto(
    int     Year,
    // KPIs
    decimal EcosystemRecyclingPct,
    decimal EcosystemValorizationPct,
    decimal EcosystemReusePct,
    int     ActiveScraps,
    int     ActiveAgreements,
    decimal TotalApprovedAmountYear,
    double  VariationRecyclingVsPrevPct,
    double  VariationAmountVsPrevPct,
    // Gráfico: ranking bar horizontal
    IReadOnlyList<ScrapComplianceRankingDto>     ScrapRanking,
    // Tabla exportable
    IReadOnlyList<ComplianceExportRowDto>        ExportRows,
    // Gráfico: evolución interanual
    IReadOnlyList<InterannualRateTrendDto>        InterannualTrend,
    // Heatmap geográfico
    IReadOnlyList<GeoComplianceHeatmapDto>       GeoHeatmap,
    // Panel normativo
    IReadOnlyList<RegulatoryChangeDto>           RegulatoryChanges
);

public sealed record ScrapComplianceRankingDto(
    Guid    ScrapId,
    string  ScrapName,
    decimal CompliancePct,
    decimal TargetWeightKg,
    decimal RealWeightKg,
    decimal RecyclingPct,
    decimal ValorizationPct,
    int     ActiveAgreements,
    decimal TotalApprovedAmount,
    string  Status
);

public sealed record ComplianceExportRowDto(
    string  ScrapName,
    string  Category,
    string  AutonomousCommunity,
    string  ProvinceName,
    string  MunicipalityName,
    string  FlowType,
    int     Year,
    string  Period,
    decimal TargetWeightKg,
    decimal RealWeightKg,
    decimal CompliancePct,
    decimal RecyclingPct,
    decimal ValorizationPct,
    int     ActiveAgreements,
    decimal ApprovedAmount
);

public sealed record InterannualRateTrendDto(
    int     Year,
    decimal RecyclingPct,
    decimal ValorizationPct,
    decimal ReusePct,
    decimal TargetRecyclingPct
);

public sealed record GeoComplianceHeatmapDto(
    string  AutonomousCommunity,
    Guid    ScrapId,
    string  ScrapName,
    decimal CompliancePct,
    string  Status
);

public sealed record RegulatoryChangeDto(
    string  ChangeType,     // "RegulatoryTarget" | "EmissionFactor" | "EcoModulation"
    string  Description,
    string  Version,
    DateTime ValidFrom,
    DateTime? ValidTo
);
