namespace GreenTransit.Application.Features.Ecomodulation.DTOs;

// ── UC5-A: SCRAP Overview ─────────────────────────────────────────────────────

/// <summary>DTO raíz del dashboard UC5-A — Panel de Datos de Ecomodulación (SCRAP).</summary>
public sealed record EcomodulationScrapOverviewDto(
    int     Year,
    // KPIs de cobertura
    int     TotalProducts,
    int     ProductsWithFullSpec,
    int     ProductsWithPartialSpec,
    int     ProductsWithNoSpec,
    double  PctWithFullSpec,
    double  PctWithLerCode,
    // Índice de circularidad por categoría
    IReadOnlyList<CircularityByCategory>   CircularityByCategory,
    // Impacto económico de reglas
    IReadOnlyList<EcomodRuleImpactDto>     RuleImpacts,
    // Estado validación por productor
    IReadOnlyList<ProducerSpecCoverageDto> ProducerCoverage,
    // Composición de materiales
    IReadOnlyList<MaterialCompositionDto>  MaterialComposition,
    // Dataset exportable (resumen)
    IReadOnlyList<EcomodulationExportRowDto> ExportRows,
    // Recomendaciones
    IReadOnlyList<string>                  Recommendations
);

public sealed record CircularityByCategory(
    string  Category,
    double  CircularityIndex,
    double  AvgRecycledContentPct,
    double  AvgReparabilityIndex,
    double  AvgDisassemblyEase,
    double  PctNonHazardous,
    double  PctWithLerCodes,
    /// <summary>Green &gt;70, Orange 40–70, Red &lt;40.</summary>
    string  TrafficLight
);

public sealed record EcomodRuleImpactDto(
    string  RuleName,
    string  ImpactType,
    int     ProductsAffected,
    decimal TotalEconomicAdjustment,
    decimal PreviousPeriodAdjustment,
    double  VariationPct,
    IReadOnlyList<decimal> MonthlySparkline
);

public sealed record ProducerSpecCoverageDto(
    Guid    ProducerId,
    string  ProducerName,
    int     TotalProducts,
    int     FullSpec,
    int     PartialSpec,
    int     NoSpec,
    double  CoveragePct,
    /// <summary>Green &gt;80%, Orange 50–80%, Red &lt;50%.</summary>
    string  TrafficLight
);

public sealed record MaterialCompositionDto(
    string  Material,
    double  TotalPercentage,
    int     ProductCount
);

public sealed record EcomodulationExportRowDto(
    string  ProductReference,
    string  ProducerName,
    string  Category,
    string? LerCode,
    double? ReparabilityIndex,
    double? RecycledContentPercent,
    string? DisassemblyEase,
    bool    ContainsHazardous,
    string? Composition,
    string? EcomodRuleName,
    decimal? EconomicAdjustment
);

// ── UC5-B: Regulatory View ────────────────────────────────────────────────────

/// <summary>DTO raíz del dashboard UC5-B — Panel Regulatorio.</summary>
public sealed record EcomodulationRegulatoryViewDto(
    int     Year,
    // Resumen ejecutivo
    int     TotalScrapsWithData,
    int     TotalProductSpecs,
    double  EcosystemCircularityIndex,
    double  PctProductsWithImprovementPotential,
    double  PreviousCircularityIndex,
    // Ranking de SCRAPs
    IReadOnlyList<ScrapMaturityRankDto>    ScrapRanking,
    // Análisis comparativo por categoría
    IReadOnlyList<CategoryEcodesignDto>    CategoryComparison,
    // Evolución temporal
    IReadOnlyList<EcomodTrendDto>          Trends,
    // Alertas
    IReadOnlyList<EcomodComplianceAlertDto> ComplianceAlerts
);

public sealed record ScrapMaturityRankDto(
    Guid    ScrapId,
    string  ScrapName,
    int     ProductCount,
    double  SpecCoveragePct,
    double  AvgCircularityIndex,
    int     ActiveRules,
    double  MaturityIndex,
    string  TrafficLight
);

public sealed record CategoryEcodesignDto(
    string  Category,
    double  AvgReparability,
    double  AvgRecycledContent,
    double  PctDisassemblyEasy,
    double  PctNonHazardous,
    double  PctWithLerCodes
);

public sealed record EcomodTrendDto(
    string  Period,
    double  CircularityIndex,
    double  SpecCoveragePct,
    double  AvgRecycledContent
);

public sealed record EcomodComplianceAlertDto(
    string  Severity,
    string  Message,
    Guid?   RelatedEntityId,
    string? RelatedEntityName
);

// ── UC5-C: DPP Readiness ──────────────────────────────────────────────────────

/// <summary>DTO raíz del dashboard UC5-C — Preparación DPP.</summary>
public sealed record EcomodulationDppReadinessDto(
    int     Year,
    // Score global
    double  GlobalDppScore,
    double  PreviousDppScore,
    // Por producto
    IReadOnlyList<ProductDppReadinessDto>  Products,
    // Heatmap categoría × campo
    IReadOnlyList<DppHeatmapRowDto>        Heatmap,
    // Top-N prioritarios
    IReadOnlyList<ProductDppReadinessDto>  PriorityProducts,
    // Por SCRAP
    IReadOnlyList<ScrapDppScoreDto>        ScrapScores,
    // Histórico
    IReadOnlyList<EcomodTrendDto>          History
);

public sealed record ProductDppReadinessDto(
    Guid    ProductId,
    string  ProductReference,
    string? ProductName,
    string? ProducerName,
    bool    HasName,
    bool    HasReference,
    bool    HasLerCode,
    bool    HasReparabilityIndex,
    bool    HasDisassemblyEase,
    bool    HasRecycledContent,
    bool    HasComposition,
    bool    HasHazardousInfo,
    bool    HasPotentialLerCodes,
    bool    HasProducer,
    double  DppScore,
    IReadOnlyList<string> MissingFields
);

public sealed record DppHeatmapRowDto(
    string  Category,
    double  PctName,
    double  PctReference,
    double  PctLerCode,
    double  PctReparability,
    double  PctDisassembly,
    double  PctRecycledContent,
    double  PctComposition,
    double  PctHazardousInfo,
    double  PctPotentialLerCodes,
    double  PctProducer
);

public sealed record ScrapDppScoreDto(
    Guid    ScrapId,
    string  ScrapName,
    double  AvgDppScore,
    int     ProductCount
);

// ── EC-A: ScrapEcoModulationPanel ─────────────────────────────────────────────

/// <summary>DTO raíz del dashboard EC-A — Panel SCRAP de Ecomodulación (impacto económico).</summary>
public sealed record ScrapEcoModulationPanelDto(
    int     Year,
    int?    Quarter,
    int?    Month,
    // KPIs económicos
    decimal TotalAdjustmentsAmount,
    decimal PreviousPeriodAmount,
    double  VariationPct,
    int     TotalSettlements,
    // Desglose por período
    IReadOnlyList<EcomodSettlementPeriodDto>  ByPeriod,
    // Top productores bonificados/penalizados
    IReadOnlyList<EcomodProducerImpactDto>    TopProducers,
    // Reglas activas y % cumplimiento
    IReadOnlyList<EcomodActiveRuleDto>        ActiveRules,
    // Detalle de liquidaciones
    IReadOnlyList<EcomodSettlementDetailDto>  Settlements
);

public sealed record EcomodSettlementPeriodDto(
    string  Period,
    decimal AdjustmentsAmount,
    int     SettlementCount
);

public sealed record EcomodProducerImpactDto(
    Guid    ProducerId,
    string  ProducerName,
    decimal TotalAdjustment,
    /// <summary>Positive = bonificación, Negative = penalización.</summary>
    string  ImpactDirection
);

public sealed record EcomodActiveRuleDto(
    string  RuleCode,
    string  FeeImpactType,
    decimal FeeImpactValue,
    int?    ProductCategory,
    int     SettlementLinesAffected,
    decimal TotalEconomicImpact
);

public sealed record EcomodSettlementDetailDto(
    Guid    SettlementId,
    string  SettlementNumber,
    int     Year,
    int?    Month,
    decimal BaseAmount,
    decimal AdjustmentsAmount,
    decimal TotalAmount,
    string  Status
);

// ── EC-B: EcoModulationRegulatoryEconomicView ─────────────────────────────────

/// <summary>DTO raíz del dashboard EC-B — Vista Regulatoria de Ecomodulación (impacto económico supervisión).</summary>
public sealed record EcoModulationRegulatoryEconomicViewDto(
    int     Year,
    // KPIs globales
    decimal TotalAdjustmentsEcosystem,
    int     TotalScrapsWithSettlements,
    int     TotalActiveRules,
    // Comparativa por SCRAP
    IReadOnlyList<EcomodScrapSettlementSummaryDto> ScrapComparison,
    // Reglas del catálogo y su aplicación
    IReadOnlyList<EcomodRuleEcosystemImpactDto>    RulesEcosystemImpact,
    // Evolución por período
    IReadOnlyList<EcomodSettlementPeriodDto>       EcosystemTrend
);

public sealed record EcomodScrapSettlementSummaryDto(
    Guid    ScrapId,
    string  ScrapName,
    decimal TotalBaseAmount,
    decimal TotalAdjustmentsAmount,
    decimal TotalAmount,
    int     SettlementCount,
    double  AdjustmentRatePct
);

public sealed record EcomodRuleEcosystemImpactDto(
    string  RuleCode,
    string  FeeImpactType,
    decimal FeeImpactValue,
    int?    ProductCategory,
    int     TotalLinesAffected,
    decimal TotalEconomicImpact,
    int     ScrapsApplyingRule
);

// ── EC-C: DPPPreparation ──────────────────────────────────────────────────────

/// <summary>DTO raíz del dashboard EC-C — Preparación DPP por productor.</summary>
public sealed record DPPPreparationDto(
    // KPIs resumen
    int    TotalProducts,
    int    ProductsWithFullSpec,
    int    ProductsDppReady,
    double PctFullSpec,
    double PctDppReady,
    // Vista del perfil activo
    string ActiveProfile,
    // Por producto: completitud y criterios DPP
    IReadOnlyList<DPPProductReadinessDto>     Products,
    // Productores adheridos (vista SCRAP)
    IReadOnlyList<DPPProducerAdherenceSummaryDto> ProducerAdherence,
    // Reglas eco-modulación aplicables
    IReadOnlyList<DPPEcomodRuleApplicabilityDto>  ApplicableRules
);

public sealed record DPPProductReadinessDto(
    Guid    ProductSpecId,
    string  ProductRef,
    string? ProductName,
    string? ProductCategory,
    string? ProducerName,
    // Campos DPP obligatorios
    bool    HasName,
    bool    HasReference,
    bool    HasLerCode,
    bool    HasReparabilityIndex,
    bool    HasDisassemblyEase,
    bool    HasRecycledContent,
    bool    HasComposition,
    bool    HasHazardousInfo,
    bool    HasPotentialLerCodes,
    bool    HasProducer,
    double  DppCompletionPct,
    bool    IsDppReady,
    IReadOnlyList<string> MissingFields,
    // Reglas eco-modulación que aplican a este producto
    IReadOnlyList<string> ApplicableEcomodRules
);

public sealed record DPPProducerAdherenceSummaryDto(
    Guid    ProducerId,
    string  ProducerName,
    int     TotalProducts,
    int     DppReadyProducts,
    double  DppReadyPct,
    string  TrafficLight
);

public sealed record DPPEcomodRuleApplicabilityDto(
    string  RuleCode,
    string  FeeImpactType,
    decimal FeeImpactValue,
    int?    ProductCategory,
    int     ProductsAffected
);
