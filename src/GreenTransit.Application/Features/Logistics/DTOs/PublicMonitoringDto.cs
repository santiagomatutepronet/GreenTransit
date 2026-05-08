namespace GreenTransit.Application.Features.Logistics.DTOs;

// ── Dashboard 2 — Panel de Monitorización para Entidades Públicas ─────────────

/// <summary>DTO raíz del Dashboard 2.</summary>
public sealed record PublicMonitoringDto(
    /// <summary>Widget 1 — Servicios prestados por SCRAP en el periodo.</summary>
    List<ScrapServiceSummaryDto>    ScrapServices,
    /// <summary>Widget 2 — Histórico mensual de recogidas (kg) por SCRAP.</summary>
    List<MonthlyPickupSeriesDto>    MonthlyPickupHistory,
    /// <summary>Widget 3 — Liquidaciones de la entidad pública.</summary>
    List<SettlementRowDto>          Settlements,
    /// <summary>Widget 4 — Emisiones CO₂e del periodo vs periodo anterior.</summary>
    EmissionComparisonDto           Emissions,
    /// <summary>Widget 5 — Cumplimiento de objetivos municipales por SCRAP.</summary>
    List<MunicipalTargetDto>        MunicipalTargets,
    int Year,
    int? Month
);

// ── Widget 1 ──────────────────────────────────────────────────────────────────

/// <summary>Fila de la tabla de servicios por SCRAP.</summary>
public sealed record ScrapServiceSummaryDto(
    Guid    IdScrap,
    string  ScrapName,
    int     TotalMoves,
    decimal TotalKg,
    int     PendingMoves,
    int     CompletedMoves,
    int     CancelledMoves
);

// ── Widget 2 ──────────────────────────────────────────────────────────────────

/// <summary>Serie mensual de kg recogidos para un SCRAP.</summary>
public sealed record MonthlyPickupSeriesDto(
    Guid   IdScrap,
    string ScrapName,
    /// <summary>Puntos de la serie: (Año-Mes, kg).</summary>
    List<MonthlyPickupPointDto> Points
);

/// <summary>Punto de la serie mensual.</summary>
public sealed record MonthlyPickupPointDto(
    int     Year,
    int     Month,
    decimal TotalKg
);

// ── Widget 3 ──────────────────────────────────────────────────────────────────

/// <summary>Fila de liquidación visible para la entidad pública.</summary>
public sealed record SettlementRowDto(
    Guid      Id,
    string    SettlementNumber,
    int       Year,
    int?      Month,
    string    Status,
    decimal   TotalAmount,
    string    Currency,
    DateTime? ValidatedAt,
    Guid?     IdScrap,
    string?   ScrapName
);

// ── Widget 4 ──────────────────────────────────────────────────────────────────

/// <summary>Comparativa de emisiones CO₂e: periodo actual vs anterior.</summary>
public sealed record EmissionComparisonDto(
    decimal CurrentCO2eKg,
    decimal PreviousCO2eKg,
    double? TrendPercent,
    decimal CurrentTotalKg,
    decimal CO2ePerTonne
);

// ── Widget 5 ──────────────────────────────────────────────────────────────────

/// <summary>Cumplimiento de cuota municipal por SCRAP.</summary>
public sealed record MunicipalTargetDto(
    Guid    IdScrap,
    string  ScrapName,
    string? AutonomousCommunity,
    decimal TargetKg,
    decimal RealKg,
    double  CompliancePercent
);
