namespace GreenTransit.Application.Features.Reporting.DTOs;

/// <summary>KPIs regulatorios calculados para un periodo (año / trimestre opcional).</summary>
public sealed record RegulatoryKpisDto(
    int    Year,
    int?   Quarter,

    // ── KPIs principales ─────────────────────────────────────────────────────
    double  RecyclingRatePercent,
    double  ReusePreparationPercent,
    double  CO2IntensityKgPerTon,
    decimal TotalWeightKg,
    int     TotalTransportsCount,

    // ── Objetivo normativo aplicable (de RegulatoryTargets o appsettings) ────
    double  MinRecyclingPercent,
    double  MinReusePercent,

    // ── Desglose por categoría de residuo ─────────────────────────────────────
    IReadOnlyList<CategoryKpiDto> ByCategory,

    // ── Cumplimiento de cuotas de mercado ─────────────────────────────────────
    IReadOnlyList<MarketShareComplianceKpiDto> MarketShareComplianceList,

    // ── Histórico trimestral (siempre 4 trimestres del año seleccionado) ──────
    IReadOnlyList<QuarterlyKpiDto> ByQuarter
);

/// <summary>KPIs de un trimestre concreto.</summary>
public sealed record QuarterlyKpiDto(
    int     Quarter,
    double  RecyclingRatePercent,
    double  ReusePreparationPercent,
    double  CO2IntensityKgPerTon,
    decimal TotalWeightKg,
    int     TotalTransportsCount
);

/// <summary>KPIs desglosados por categoría de residuo.</summary>
public sealed record CategoryKpiDto(
    string  Category,
    decimal TotalWeightKg,
    double  RecyclingRatePercent,
    double  ReusePreparationPercent
);

/// <summary>Cumplimiento real vs objetivo de una cuota de mercado.</summary>
public sealed record MarketShareComplianceKpiDto(
    string   Category,
    string?  AutonomousCommunity,
    decimal  TargetKg,
    decimal  ActualKg,
    double   CompliancePercent
);

/// <summary>Resultado de la exportación XLSX: contenido en memoria.</summary>
public sealed record ExportKpisResultDto(
    byte[]  Content,
    string  ContentType,
    string  FileName
);
