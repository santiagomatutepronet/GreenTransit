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
