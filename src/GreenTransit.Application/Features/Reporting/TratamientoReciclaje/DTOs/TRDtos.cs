namespace GreenTransit.Application.Features.Reporting.TratamientoReciclaje.DTOs;

// ── DTOs comunes ─────────────────────────────────────────────────────────────

/// <summary>Balance de tratamiento por planta o global.</summary>
public sealed record TreatmentBalanceDto(
    Guid    PlantId,
    string  PlantName,
    decimal WeightReused,
    decimal WeightValued,
    decimal WeightRemove,
    decimal WeightTotal,
    double  RecyclingRate
);

/// <summary>Tasa de reciclaje por tipo de residuo (código LER).</summary>
public sealed record RecyclingRateByResidueDto(
    string  LERCode,
    string  ResidueName,
    decimal WeightTotal,
    decimal WeightReused,
    decimal WeightValued,
    double  RecyclingRate,
    /// <summary>Green &gt;70, Orange 40–70, Red &lt;40.</summary>
    string  TrafficLight
);

/// <summary>Evolución mensual de métricas de reciclaje.</summary>
public sealed record MonthlyRecyclingTrendDto(
    string  Period,
    double  RecyclingRate,
    double  ImproperRate,
    decimal WeightValued
);

/// <summary>Comparativa de rendimiento por SCRAP.</summary>
public sealed record TRScrapComparisonDto(
    Guid    ScrapId,
    string  ScrapName,
    int     TotalMoves,
    decimal TotalWeightKg,
    double  RecyclingRate,
    double  ImproperRate,
    int     OpenIncidents
);

/// <summary>Distribución de operaciones de tratamiento R/D.</summary>
public sealed record TreatmentOperationsDistributionDto(
    string  OperationCode,
    string  OperationDescription,
    string  OperationType,
    decimal WeightTotal
);

/// <summary>Incidencia de tratamiento abierta.</summary>
public sealed record TRIncidentDto(
    Guid     IncidentId,
    string?  WasteMoveReference,
    string?  IncidentType,
    string?  Severity,
    DateTime OpenedAt,
    int      DaysOpen,
    string?  PlantName
);

/// <summary>Fila de exportación a XLSX.</summary>
public sealed record TRExportRowDto(
    string?  TreatmentDate,
    string?  PickupDate,
    string?  MunicipalityName,
    string?  ProvinceName,
    string?  ScrapName,
    string?  PlantName,
    string?  CarrierName,
    string?  LERCode,
    string?  ResidueName,
    string?  VehicleType,
    decimal? DistanceKm,
    decimal? DurationMin,
    decimal? CO2Emissions,
    string?  TreatmentOperationCode,
    decimal? WeightTotal,
    decimal? WeightReused,
    decimal? WeightValued,
    decimal? WeightRemove,
    decimal? ImproperWeight,
    double?  RecyclingRate
);

// ── TR-A — SCRAP ─────────────────────────────────────────────────────────────

/// <summary>DTO raíz del Dashboard TR-A — Análisis de Calidad y Revalorización (SCRAP).</summary>
public sealed record TRScrapAnalysisDto(
    IReadOnlyList<TreatmentBalanceDto>          TreatmentBalance,
    IReadOnlyList<RecyclingRateByResidueDto>    RecyclingRateByResidue,
    IReadOnlyList<MonthlyRecyclingTrendDto>     MonthlyTrend,
    IReadOnlyList<TRScrapComparisonDto>         ScrapComparison,
    IReadOnlyList<TreatmentOperationsDistributionDto> OperationsDistribution,
    IReadOnlyList<TRIncidentDto>                OpenIncidents,
    int  Year,
    int? Month
);

// ── TR-B — Ayuntamiento ───────────────────────────────────────────────────────

/// <summary>Card KPI para los dashboards de Tratamiento y Reciclaje.</summary>
public sealed record TRKpiCardDto(
    string  Label,
    double  Value,
    string  Unit,
    double? PreviousValue,
    double? ChangePercent
);

/// <summary>Detalle de cumplimiento por SCRAP (vista ayuntamiento).</summary>
public sealed record TRMunicipalScrapDetailDto(
    Guid    ScrapId,
    string  ScrapName,
    int     TotalMoves,
    decimal TotalWeightKg,
    double  RecyclingRate,
    double  ImproperRate,
    int     OpenIncidents,
    string  RecyclingTrafficLight
);

/// <summary>Alerta de calidad de reciclaje.</summary>
public sealed record TRQualityAlertDto(
    string  AlertType,
    string  Message,
    string  Severity,
    string? ScrapName,
    string? MunicipalityName
);

/// <summary>DTO raíz del Dashboard TR-B — Monitorización de Reciclaje Municipal.</summary>
public sealed record TRMunicipalMonitoringDto(
    IReadOnlyList<TRKpiCardDto>                 KpiCards,
    IReadOnlyList<MonthlyRecyclingTrendDto>     MonthlyTrend,
    IReadOnlyList<TRMunicipalScrapDetailDto>    ScrapDetails,
    IReadOnlyList<TreatmentOperationsDistributionDto> OperationsDistribution,
    IReadOnlyList<TRQualityAlertDto>            QualityAlerts,
    int  Year,
    int? Month
);

// ── TR-C — Coordinador ────────────────────────────────────────────────────────

/// <summary>DTO raíz del Dashboard TR-C — Validación y Datos Multi-SCRAP (Coordinador).</summary>
public sealed record TRCoordinatorValidationDto(
    IReadOnlyList<TRScrapComparisonDto>              ScrapSummaries,
    IReadOnlyList<MonthlyRecyclingTrendDto>          MonthlyTrend,
    IReadOnlyList<TreatmentOperationsDistributionDto> OperationsDistribution,
    IReadOnlyList<TRExportRowDto>                    ExportDataset,
    int  Year,
    int? Month
);

// ── TR-D — Oficina de Asignación ─────────────────────────────────────────────

/// <summary>DTO raíz de la Vista TR-D — Datos Operativos de Tratamiento (Oficina de Asignación).</summary>
public sealed record TRDispatchDataDto(
    IReadOnlyList<TRScrapComparisonDto>              ScrapSummaries,
    TreatmentBalanceDto?                             AggregatedBalance,
    IReadOnlyList<MonthlyRecyclingTrendDto>          MonthlyTrend,
    IReadOnlyList<TreatmentOperationsDistributionDto> OperationsDistribution,
    IReadOnlyList<TRExportRowDto>                    ExportDataset,
    int  Year,
    int? Month
);
