namespace GreenTransit.Application.Features.Mobility.DTOs;

// ── DTOs comunes ─────────────────────────────────────────────────────────────

/// <summary>Celda del heatmap semanal 7×24 (día de semana × hora).</summary>
public sealed record WeeklyHeatmapCellDto(
    /// <summary>0=lunes … 6=domingo.</summary>
    int DayOfWeek,
    /// <summary>0–23.</summary>
    int Hour,
    int PickupCount
);

/// <summary>Métricas de conflicto logístico-movilidad para un municipio.</summary>
public sealed record MunicipalConflictIndexDto(
    string  MunicipalityCode,
    string? MunicipalityName,
    string? ProvinceCode,
    string? ProvinceName,
    int     TotalPickups,
    double  PeakHourPercent,
    double  OutsideDumWindowPercent,
    int     LogisticsIncidents,
    /// <summary>Índice de conflicto 0–100 (ponderado según MobilitySettings).</summary>
    double  ConflictIndex,
    /// <summary>Semáforo: Green &lt;40, Orange 40–70, Red &gt;70.</summary>
    string  TrafficLight
);

/// <summary>Recomendación automática generada por el motor de reglas.</summary>
public sealed record MobilityRecommendationDto(
    string Severity,
    string MunicipalityCode,
    string? MunicipalityName,
    string Message
);

/// <summary>Comparativa de eficiencia entre dos periodos.</summary>
public sealed record EfficiencyComparisonDto(
    string  PeriodA,
    string  PeriodB,
    decimal AvgDistanceKmA,
    decimal AvgDistanceKmB,
    decimal AvgDurationMinA,
    decimal AvgDurationMinB,
    decimal CO2ePerTonneA,
    decimal CO2ePerTonneB,
    double  PeakHourPercentA,
    double  PeakHourPercentB
);

// ── UC3-A — Coordinador ──────────────────────────────────────────────────────

/// <summary>DTO raíz del Dashboard UC3-A — Análisis de Impacto en Movilidad (Coordinador).</summary>
public sealed record MobilityCoordinatorAnalysisDto(
    /// <summary>Celdas del heatmap semanal.</summary>
    IReadOnlyList<WeeklyHeatmapCellDto> WeeklyHeatmap,

    /// <summary>% de recogidas que caen en franja de hora pico.</summary>
    double PeakHourPercent,

    /// <summary>Tabla de municipios ordenada por índice de conflicto desc.</summary>
    IReadOnlyList<MunicipalConflictIndexDto> ConflictIndex,

    /// <summary>Comparativa pre/post optimización.</summary>
    EfficiencyComparisonDto? EfficiencyComparison,

    /// <summary>Recomendaciones automáticas generadas por el motor de reglas.</summary>
    IReadOnlyList<MobilityRecommendationDto> Recommendations,

    int Year,
    int? Month
);

// ── UC3-B — Ayuntamiento ─────────────────────────────────────────────────────

/// <summary>KPI card con valor actual y variación vs periodo anterior.</summary>
public sealed record MobilityKpiCardDto(
    string  Label,
    double  Value,
    string  Unit,
    double? TrendPercent
);

/// <summary>Item del calendario de recogidas planificadas.</summary>
public sealed record PlannedPickupCalendarItemDto(
    Guid     ServiceOrderId,
    string   ServiceOrderNumber,
    DateTime? PlannedPickupStart,
    DateTime? PlannedPickupEnd,
    string  ScrapName,
    /// <summary>Green | Orange | Red según conflicto con DUM/hora pico.</summary>
    string  TrafficLight
);

/// <summary>Serie histórica de recogidas + incidencias por semana/mes.</summary>
public sealed record PickupIncidentSeriesDto(
    string  Period,
    int     PickupCount,
    int     IncidentCount
);

/// <summary>Fila de cumplimiento por SCRAP para el ayuntamiento.</summary>
public sealed record ScrapMobilityComplianceRowDto(
    Guid    IdScrap,
    string  ScrapName,
    int     TotalMoves,
    decimal TotalKg,
    double  PeakHourPercent,
    double  DumCompliancePercent,
    int     OpenIncidents,
    double  ConflictIndex,
    string  TrafficLight
);

/// <summary>Notificación activa de impacto en movilidad.</summary>
public sealed record MobilityNotificationDto(
    string   Type,
    string   Message,
    DateTime GeneratedAt,
    string   Severity
);

/// <summary>DTO raíz del Dashboard UC3-B — Monitorización de Movilidad (Ayuntamiento).</summary>
public sealed record MobilityMunicipalMonitoringDto(
    IReadOnlyList<MobilityKpiCardDto>              KpiCards,
    IReadOnlyList<PlannedPickupCalendarItemDto>    PlannedPickups,
    IReadOnlyList<PickupIncidentSeriesDto>         HistoricalSeries,
    IReadOnlyList<ScrapMobilityComplianceRowDto>   ScrapCompliance,
    IReadOnlyList<MobilityNotificationDto>         ActiveNotifications,
    int Year,
    int? Month
);

// ── UC3-C — Oficina de Asignación ────────────────────────────────────────────

/// <summary>Fila del dataset exportable de impacto en movilidad.</summary>
public sealed record MobilityExportRowDto(
    DateTime? PickupDate,
    string?   MunicipalityCode,
    string?   MunicipalityName,
    string?   ProvinceCode,
    string?   ProvinceName,
    string?   ScrapName,
    string?   VehicleType,
    decimal   WeightKg,
    decimal   DistanceKm,
    decimal   DurationMin,
    decimal   CO2eKg,
    string?   DumZoneCode,
    bool      DumCompliant,
    bool      InPeakHour
);

/// <summary>Resumen operativo por SCRAP para la oficina de asignación.</summary>
public sealed record DispatchScrapSummaryDto(
    Guid    IdScrap,
    string  ScrapName,
    int     TotalPickups,
    double  PeakHourPercent,
    double  DumCompliancePercent,
    int     OpenIncidents
);

/// <summary>Item de planificación semanal con indicador de movilidad.</summary>
public sealed record WeeklyPlanMobilityItemDto(
    Guid      ServiceOrderId,
    string    ServiceOrderNumber,
    DateTime? PlannedPickupStart,
    string   PickupPointName,
    string?  MunicipalityCode,
    string?  MunicipalityName,
    string?  ProvinceCode,
    string?  ProvinceName,
    string   ScrapName,
    string   TrafficLight
);

/// <summary>Serie mensual de métricas de movilidad.</summary>
public sealed record MonthlyMobilitySeriesDto(
    string Period,
    double PeakHourPercent,
    double DumCompliancePercent,
    double AvgConflictIndex
);

/// <summary>DTO raíz de la vista UC3-C — Datos de Impacto RAEE en Movilidad (Oficina de Asignación).</summary>
public sealed record MobilityDispatchDataDto(
    IReadOnlyList<MobilityExportRowDto>       ExportDataset,
    IReadOnlyList<DispatchScrapSummaryDto>    ScrapSummaries,
    IReadOnlyList<WeeklyPlanMobilityItemDto>  WeeklyPlan,
    IReadOnlyList<MonthlyMobilitySeriesDto>   MonthlySeries,
    int Year,
    int? Month
);
