namespace GreenTransit.Application.Features.Reporting.HeatMaps.DTOs;

// ── HM-A: Mapa de Calor de Densidad de Residuos ──────────────────────────────

/// <summary>DTO raíz del dashboard HM-A — Mapa de Calor de Densidad de Residuos (SCRAP).</summary>
public sealed record WasteDensityHeatMapDto(
    int     Year,
    int?    Month,
    // Puntos georreferenciados con intensidad
    IReadOnlyList<HeatMapPointDto>          HeatMapPoints,
    // Densidad por zona geográfica
    IReadOnlyList<DensityByZoneDto>         DensityByZone,
    // Tipología de residuos
    IReadOnlyList<WasteTypologyDto>         WasteTypology,
    // Top 20 puntos por volumen
    IReadOnlyList<TopPickupPointDto>        TopPickupPoints,
    // Frecuencia de recogida por punto
    IReadOnlyList<PickupFrequencyDto>       PickupFrequency,
    // KPIs de frecuencia
    double                                  AvgPickupsPerPointPerMonth,
    int                                     AnomalousFrequencyPoints,
    // Comparativa entre periodos
    IReadOnlyList<PeriodComparisonDto>      PeriodComparison,
    // Exportación
    IReadOnlyList<HeatMapExportRowDto>      ExportRows
);

public sealed record HeatMapPointDto(
    Guid    EntityId,
    string  EntityName,
    string? Address,
    double  Latitude,
    double  Longitude,
    decimal TotalKg,
    int     PickupCount,
    string? PredominantLerCode,
    string? PredominantLerDescription,
    DateTime? LastPickup
);

public sealed record DensityByZoneDto(
    string  ZoneCode,
    string  ZoneName,
    string  ZoneLevel,   // "AutonomousCommunity" | "Province" | "Municipality"
    decimal TotalKg,
    int     PickupCount,
    IReadOnlyList<WasteTypologyDto> ByTypology
);

public sealed record WasteTypologyDto(
    string  LerCode,
    string  LerDescription,
    bool    IsDangerous,
    decimal TotalKg,
    double  Percentage
);

public sealed record TopPickupPointDto(
    Guid    EntityId,
    string  EntityName,
    string? Municipality,
    string? Province,
    decimal TotalKg,
    int     PickupCount,
    decimal AvgKgPerPickup,
    string? PredominantLerCode,
    /// <summary>Red = P90+, Orange = P75–P90, Green = &lt;P75.</summary>
    string  TrafficLight
);

public sealed record PickupFrequencyDto(
    Guid    EntityId,
    string  EntityName,
    string? Municipality,
    double  AvgPickupsPerMonth,
    bool    IsAnomalous,
    IReadOnlyList<double> MonthlySparkline
);

public sealed record PeriodComparisonDto(
    string  ZoneCode,
    string  ZoneName,
    decimal PeriodAKg,
    int     PeriodAPickups,
    decimal PeriodBKg,
    int     PeriodBPickups,
    double  KgVariationPct,
    double  PickupVariationPct
);

public sealed record HeatMapExportRowDto(
    string  EntityName,
    string? Municipality,
    string? Province,
    string? LerCode,
    string? LerDescription,
    bool    IsDangerous,
    decimal TotalKg,
    int     PickupCount,
    DateTime? LastPickup
);

// ── HM-B: Análisis de Patrones y Estacionalidad ──────────────────────────────

/// <summary>DTO raíz del dashboard HM-B — Análisis de Patrones y Estacionalidad (SCRAP).</summary>
public sealed record WastePatternAnalysisDto(
    int     Year,
    // Heatmap temporal 12×tipología
    IReadOnlyList<TemporalHeatMapCellDto>   TemporalHeatMap,
    // Tendencia de volumen mensual
    IReadOnlyList<MonthlyTrendSeriesDto>    MonthlyTrends,
    // Heatmap semanal 7×24
    IReadOnlyList<WeeklyFrequencyCell>      WeeklyFrequency,
    // Índice de concentración
    double                                  ConcentrationIndex,
    double                                  ConcentrationIndexVariationPct,
    // Alertas de acumulación
    IReadOnlyList<AccumulationAlertDto>     Alerts
);

public sealed record TemporalHeatMapCellDto(
    int     Month,
    string  LerChapter,
    string  LerChapterDescription,
    decimal TotalKg
);

public sealed record MonthlyTrendSeriesDto(
    string  LerCode,
    string  LerDescription,
    IReadOnlyList<decimal>  MonthlyKg,
    IReadOnlyList<decimal>  MovingAvg3M
);

public sealed record WeeklyFrequencyCell(
    int DayOfWeek,   // 0=Lunes…6=Domingo
    int Hour,
    int PickupCount
);

public sealed record AccumulationAlertDto(
    string  AlertType,       // "OverloadPoint" | "HighDensityMunicipality" | "ReducedFrequency"
    string  Severity,        // "High" | "Medium" | "Low"
    string  EntityOrZoneName,
    string  Message,
    DateTime GeneratedAt
);

// ── HM-C: Vista de Entidades Públicas ────────────────────────────────────────

/// <summary>DTO raíz del dashboard HM-C — Vista de Mapas de Calor para Entidades Públicas.</summary>
public sealed record PublicEntityHeatMapDto(
    int     Year,
    int?    Month,
    // KPIs ejecutivos
    decimal TotalKg,
    int     ActivePickupPoints,
    string? PredominantLerCode,
    string? PredominantLerDescription,
    double  AvgPickupsPerPointPerMonth,
    double  TotalKgVariationPct,
    double  PickupPointsVariationPct,
    double  FrequencyVariationPct,
    // Mapa de calor territorial
    IReadOnlyList<HeatMapPointDto>          HeatMapPoints,
    // Distribución por tipología
    IReadOnlyList<WasteTypologyDto>         WasteTypology,
    // Evolución temporal por SCRAP
    IReadOnlyList<MonthlyTrendSeriesDto>    MonthlyTrends,
    // Detalle de puntos de recogida
    IReadOnlyList<TopPickupPointDto>        PickupPointDetails,
    // Indicadores de zonas sensibles
    IReadOnlyList<SensitiveZoneIndicatorDto> SensitiveZoneIndicators,
    // Exportación
    IReadOnlyList<HeatMapExportRowDto>      ExportRows
);

public sealed record SensitiveZoneIndicatorDto(
    string  EntityName,
    string? Address,
    string? ZoneName,
    decimal TotalKg,
    bool    ExceedsThreshold,
    string  TrafficLight
);

// ── Frecuencia por punto de recogida (widget compartido) ─────────────────────

public sealed record WasteFrequencyByPickupPointDto(
    Guid    EntityId,
    string  EntityName,
    string? Municipality,
    string? Province,
    int     TotalPickups,
    decimal TotalKg,
    double  AvgPickupsPerMonth,
    DateTime? LastPickup
);

// ── Estacionalidad (widget compartido) ───────────────────────────────────────

public sealed record SeasonalityAnalysisDto(
    IReadOnlyList<SeasonalityMonthDto> Months
);

public sealed record SeasonalityMonthDto(
    int     Month,
    decimal TotalKg,
    int     PickupCount,
    double  KgVsPreviousYearPct
);

// ── Exportación XLSX ──────────────────────────────────────────────────────────

public sealed record HeatMapExportResultDto(
    byte[]  Content,
    string  ContentType,
    string  FileName
);
