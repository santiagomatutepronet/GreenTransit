namespace GreenTransit.Application.Features.Reporting.CarbonFootprint.DTOs;

// ── CO2-A: Panel de Control General ──────────────────────────────────────────

/// <summary>DTO raíz del dashboard CO2-A — Panel de Control General de Huella de Carbono.</summary>
public sealed record CarbonOverviewDto(
    // Periodo
    DateTime DateFrom,
    DateTime DateTo,
    // KPIs
    decimal TotalCO2eTonnes,
    decimal CO2ePerTonneKg,
    double  VariationVsPreviousPct,
    decimal TotalDistanceKm,
    int     TotalServices,
    int     ServicesWithoutFactor,
    // Gráfico 1: evolución mensual (año actual vs año anterior)
    IReadOnlyList<MonthlyEmissionSeriesDto>  MonthlyEvolution,
    // Gráfico 2: distribución por fuente (combustible)
    IReadOnlyList<EmissionByFuelDto>         ByFuelType,
    // Gráfico 3: top 10 operaciones más emisoras
    IReadOnlyList<TopEmissionOperationDto>   Top10Operations
);

public sealed record MonthlyEmissionSeriesDto(
    int     Year,
    int     Month,
    decimal CO2eTonnes
);

public sealed record EmissionByFuelDto(
    string  FuelType,
    decimal CO2eTonnes,
    double  Pct
);

public sealed record TopEmissionOperationDto(
    string  WasteMoveReference,
    string? OriginName,
    string? DestinationName,
    decimal DistanceKm,
    string? VehicleType,
    string? FuelType,
    decimal WeightKg,
    decimal CO2eKg
);

// ── CO2-B: Emisiones por Transporte ──────────────────────────────────────────

/// <summary>DTO raíz del dashboard CO2-B — Emisiones por Transporte.</summary>
public sealed record CarbonTransportDto(
    DateTime DateFrom,
    DateTime DateTo,
    // KPIs
    decimal TransportCO2eTonnes,
    decimal IntensityGCO2ePerKm,
    decimal IntensityGCO2ePerTonneKm,
    double  LowEmissionKmPct,
    // Gráfico 1: barras apiladas por combustible/mes
    IReadOnlyList<MonthlyEmissionByFuelDto>     MonthlyByFuel,
    // Gráfico 2: comparativa por gestor
    IReadOnlyList<EmissionByScrapDto>           ByScrap,
    // Gráfico 3: scatter km vs emisiones
    IReadOnlyList<OperationScatterDto>          ScatterData,
    // Tabla detallada
    IReadOnlyList<TransportOperationDetailDto>  OperationDetails
);

public sealed record MonthlyEmissionByFuelDto(
    int     Year,
    int     Month,
    string  FuelType,
    decimal CO2eTonnes
);

public sealed record EmissionByScrapDto(
    Guid    ScrapId,
    string  ScrapName,
    decimal CO2eTonnes,
    decimal TotalKm,
    decimal WeightKg,
    decimal CO2ePerTonne
);

public sealed record OperationScatterDto(
    string  WasteMoveReference,
    decimal DistanceKm,
    decimal CO2eKg,
    string? VehicleType
);

public sealed record TransportOperationDetailDto(
    string   WasteMoveReference,
    DateTime? Date,
    string?  OriginMunicipality,
    string?  OriginProvince,
    string?  DestinationMunicipality,
    string?  DestinationProvince,
    decimal  DistanceKm,
    string?  VehicleType,
    string?  FuelType,
    decimal  WeightKg,
    decimal? CO2eKg,
    bool     MissingFactor
);

// ── CO2-C: Eficiencia Energética de Instalaciones ────────────────────────────

/// <summary>DTO raíz del dashboard CO2-C — Eficiencia Energética de Instalaciones.</summary>
public sealed record CarbonInstallationsDto(
    DateTime DateFrom,
    DateTime DateTo,
    // KPIs
    decimal InstallationsCO2eTonnes,
    double  ValorizationRatePct,
    decimal AvoidedEmissionsTonnes,
    decimal TotalTreatmentWeightKg,
    // Gráfico 1: emisiones por instalación
    IReadOnlyList<EmissionByPlantDto>           ByPlant,
    // Gráfico 2: evolución mensual por tipo de tratamiento
    IReadOnlyList<MonthlyTreatmentVolumeDto>    MonthlyByTreatmentType,
    // Tabla ranking de instalaciones
    IReadOnlyList<PlantEfficiencyDto>           PlantRanking,
    // Perfil radar de la instalación seleccionada
    PlantRadarProfileDto?                       SelectedPlantProfile,
    decimal                                     AvgCO2ePerTonneAllPlants
);

public sealed record EmissionByPlantDto(
    Guid    PlantId,
    string  PlantName,
    string? Municipality,
    decimal RecoveryCO2e,
    decimal DisposalCO2e
);

public sealed record MonthlyTreatmentVolumeDto(
    int     Year,
    int     Month,
    string  TreatmentType,   // "Recovery" | "Disposal"
    decimal WeightKg
);

public sealed record PlantEfficiencyDto(
    Guid    PlantId,
    string  PlantName,
    string? Municipality,
    string? Province,
    decimal TreatmentWeightKg,
    decimal CO2eKg,
    decimal CO2ePerTonneKg,
    double  ValorizationPct,
    /// <summary>Above | Below respecto a la media.</summary>
    string  EfficiencyStatus
);

public sealed record PlantRadarProfileDto(
    Guid    PlantId,
    string  PlantName,
    double  ValorizationRate,
    double  EmissionIntensity,
    double  TreatmentVolume,
    double  WasteDiversity,
    double  AvoidedEmissionsRatio
);

// ── CO2-D: Comparativa y Tendencias ──────────────────────────────────────────

/// <summary>DTO raíz del dashboard CO2-D — Comparativa y Tendencias.</summary>
public sealed record CarbonTrendsDto(
    // KPIs
    double  AccumulatedReductionPct,
    string? BestScrapName,
    decimal BestScrapCO2ePerTonne,
    string? BestProvinceName,
    double  BestProvinceReductionPct,
    // Gráfico 1: evolución por gestor
    IReadOnlyList<EmissionSeriesByScrapDto>     ByScrapEvolution,
    // Gráfico 2: heatmap provincia × mes
    IReadOnlyList<ProvinceMonthHeatmapCellDto>  ProvinceHeatmap,
    // Gráfico 3: barras agrupadas trimestre actual vs anterior
    IReadOnlyList<QuarterlyComparisonDto>       QuarterlyComparison,
    // Gráfico 4: waterfall de variación
    IReadOnlyList<WaterfallItemDto>             WaterfallItems,
    // Tabla por provincia
    IReadOnlyList<ProvinceEmissionSummaryDto>   ProvinceSummary
);

public sealed record EmissionSeriesByScrapDto(
    Guid    ScrapId,
    string  ScrapName,
    int     Year,
    int     Month,
    decimal CO2eTonnes
);

public sealed record ProvinceMonthHeatmapCellDto(
    string  ProvinceName,
    int     Month,
    decimal CO2eTonnes
);

public sealed record QuarterlyComparisonDto(
    int     Quarter,
    decimal CurrentCO2eTonnes,
    decimal PreviousCO2eTonnes
);

public sealed record WaterfallItemDto(
    string  Label,
    decimal Value,
    /// <summary>positive | negative | total</summary>
    string  Type
);

public sealed record ProvinceEmissionSummaryDto(
    string  ProvinceName,
    decimal CO2eTonnes,
    decimal CO2ePerTonne,
    decimal TotalKm,
    int     ServiceCount,
    double  ValorizationPct,
    double  VariationVsPreviousPct
);

// ── HC-C: Huella Energética de Plantas ───────────────────────────────────────

/// <summary>DTO raíz del dashboard HC-C — Huella Energética de Plantas (Scope 2).</summary>
public sealed record CarbonPlantEnergyDto(
    DateTime DateFrom,
    DateTime DateTo,
    // KPIs
    decimal TotalKwhPeriod,
    decimal Scope2CO2eTonnes,
    decimal Scope2CO2ePerTonneKg,
    double  VariationVsPreviousPct,
    // Series
    IReadOnlyList<PlantEnergyComparisonDto>     PlantComparison,
    IReadOnlyList<MonthlyPlantEnergyDto>        MonthlyEvolution,
    IReadOnlyList<EnergySourceBreakdownDto>     BySource,
    // Tabla
    IReadOnlyList<PlantEnergyDetailDto>         Details
);

public sealed record PlantEnergyComparisonDto(
    string  PlantName,
    decimal KwhTotal,
    decimal Scope2CO2eKg,
    decimal TreatmentWeightKg,
    decimal CO2ePerTonneKg
);

public sealed record MonthlyPlantEnergyDto(
    int     Year,
    int     Month,
    string  PlantName,
    decimal KwhTotal,
    decimal TreatmentWeightKg
);

public sealed record EnergySourceBreakdownDto(
    string  Source,
    decimal KwhTotal,
    double  Pct
);

public sealed record PlantEnergyDetailDto(
    string  PlantName,
    string? PlantCenterCode,
    int     Year,
    int     Month,
    decimal KwhTotal,
    string? Source,
    string? GridMixRef,
    string? AllocationMethod,
    decimal TreatmentWeightKg,
    decimal Scope2CO2eKg,
    decimal CO2ePerTonneKg
);

// ── HC-D: Reporte de Huella para Productores ─────────────────────────────────

/// <summary>DTO raíz del dashboard HC-D — Reporte de Huella de Carbono para Productores.</summary>
public sealed record CarbonProducerReportDto(
    DateTime DateFrom,
    DateTime DateTo,
    // KPIs
    decimal TotalCO2eKg,
    decimal TotalTonnesManaged,
    decimal IntensityCO2ePerTonne,
    int     TotalServices,
    double  VariationVsPreviousPct,
    // Series
    IReadOnlyList<MonthlyProducerEmissionDto>   MonthlyEvolution,
    IReadOnlyList<EmissionByLerCodeDto>         ByLerCode,
    IReadOnlyList<EmissionByDestinationDto>     ByDestination,
    // Gauge: intensidad productor vs media ecosistema
    decimal EcosystemAvgCO2ePerTonne,
    // Tabla detalle
    IReadOnlyList<ProducerOperationDetailDto>   Details
);

public sealed record MonthlyProducerEmissionDto(
    int     Year,
    int     Month,
    decimal CO2eKg,
    decimal IntensityCO2ePerTonne
);

public sealed record EmissionByLerCodeDto(
    string  LerCode,
    string  LerDescription,
    decimal CO2eKg,
    decimal WeightKg,
    decimal IntensityCO2ePerTonne,
    int     ServiceCount
);

public sealed record EmissionByDestinationDto(
    string  PlantName,
    decimal AvgDistanceKm,
    decimal CO2eKg,
    decimal IntensityCO2ePerTonne,
    int     ServiceCount
);

public sealed record ProducerOperationDetailDto(
    string   WasteMoveReference,
    DateTime? Date,
    string?  LerCode,
    string?  LerDescription,
    string?  DestinationName,
    decimal  DistanceKm,
    string?  VehicleType,
    string?  FuelType,
    decimal  WeightKg,
    decimal? CO2eKg,
    bool     MissingFactor
);

// ── HC-E: Vista de Emisiones para Entidades Públicas ─────────────────────────

/// <summary>DTO raíz del dashboard HC-E — Panel de Emisiones para Entidades Públicas.</summary>
public sealed record CarbonPublicViewDto(
    DateTime DateFrom,
    DateTime DateTo,
    string   MunicipalityName,
    // KPIs
    decimal TotalCO2eKg,
    decimal TotalTonnesManaged,
    decimal IntensityCO2ePerTonne,
    int     TotalServices,
    double  VariationVsPreviousPct,
    // Series
    IReadOnlyList<MonthlyPublicEmissionDto>     MonthlyEvolution,
    IReadOnlyList<EmissionByScrapPublicDto>     ByScrap,
    IReadOnlyList<MonthlyFuelStackDto>          MonthlyByFuel,
    // Alertas/notificaciones
    IReadOnlyList<CarbonAlertDto>               Alerts
);

public sealed record MonthlyPublicEmissionDto(
    int     Year,
    int     Month,
    decimal CO2eKg,
    decimal IntensityCO2ePerTonne
);

public sealed record EmissionByScrapPublicDto(
    Guid    ScrapId,
    string  ScrapName,
    decimal CO2eKg,
    decimal WeightKg,
    decimal IntensityCO2ePerTonne,
    int     ServiceCount,
    decimal AvgDistanceKm
);

public sealed record MonthlyFuelStackDto(
    int     Year,
    int     Month,
    string  FuelType,
    decimal CO2eKg
);

public sealed record CarbonAlertDto(
    string  Level,     // "warning" | "danger"
    string  Message
);
