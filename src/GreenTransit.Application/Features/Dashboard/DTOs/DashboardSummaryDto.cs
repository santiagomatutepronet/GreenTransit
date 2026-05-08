namespace GreenTransit.Application.Features.Dashboard.DTOs;

/// <summary>DTO principal del dashboard operativo. Agrupa todos los KPIs en una sola respuesta.</summary>
public sealed record DashboardSummaryDto(
    /// <summary>Conteo de WasteMoves agrupados por ServiceStatus.</summary>
    Dictionary<string, int> WasteMovesByStatus,

    /// <summary>Suma de kg recogidos (estados RECOGIDO o posteriores) en el mes en curso.</summary>
    decimal KgCollectedThisMonth,

    /// <summary>Suma de kg tratados (TreatmentPlantResidues.WeightTotal) en el mes en curso.</summary>
    decimal KgTreatedThisMonth,

    /// <summary>Tasa de reciclaje: Σ WeightValued (IsRecycling=true) / Σ WeightTotal × 100.</summary>
    double RecyclingRatePercent,

    /// <summary>Tasa de valorización energética: Σ WeightValued (IsEnergyRecovery=true) / Σ WeightTotal × 100.</summary>
    double EnergyRecoveryPercent,

    /// <summary>Tasa de preparación para reutilización: Σ WeightReused (IsPreparationForReuse=true) / Σ WeightTotal × 100.</summary>
    double ReusePercent,

    /// <summary>Huella CO₂ acumulada en el mes en curso (kgCO₂e).</summary>
    decimal TotalCO2ThisMonth,

    /// <summary>Huella CO₂ del mes anterior (para mostrar tendencia).</summary>
    decimal CO2PreviousMonth,

    /// <summary>Incidencias abiertas (ClosedAt IS NULL) agrupadas por Severity.</summary>
    Dictionary<string, int> OpenIncidentsBySeverity,

    /// <summary>Cumplimiento de cuotas de MarketShares del año en curso por categoría.</summary>
    IReadOnlyList<MarketShareComplianceDto> MarketShareCompliance,

    /// <summary>Próximas 5 órdenes de servicio con PlannedPickupStart en los próximos 7 días.</summary>
    IReadOnlyList<UpcomingPickupDto> UpcomingPickups,

    /// <summary>Histórico de kg recogidos vs tratados de los últimos 6 meses.</summary>
    IReadOnlyList<MonthlyKgDto> MonthlyKgLast6Months,

    /// <summary>Puntos geográficos de entidades activas del OwnerId (para mapa Leaflet).</summary>
    IReadOnlyList<EntityMapPointDto> EntityMapPoints,

    /// <summary>Zonas DUM activas del OwnerId como GeoJSON (para mapa Leaflet).</summary>
    IReadOnlyList<DumZoneMapDto> DumZones
);

/// <summary>Cumplimiento real vs objetivo para una categoría de residuo.</summary>
public sealed record MarketShareComplianceDto(
    string    Category,
    string?   AutonomousCommunity,
    decimal   TargetKg,
    decimal   ActualKg,
    double    CompliancePercent,
    Guid?     IdScrap    = null,
    string?   ScrapName  = null
);

/// <summary>Orden de servicio próxima a ejecutar.</summary>
public sealed record UpcomingPickupDto(
    Guid      Id,
    string    ServiceOrderNumber,
    string?   PickupPointName,
    DateTime? PlannedPickupStart,
    string    Priority
);

/// <summary>Kg recogidos y tratados para un mes concreto.</summary>
public sealed record MonthlyKgDto(
    int     Year,
    int     Month,
    decimal KgCollected,
    decimal KgTreated
);

/// <summary>Punto geográfico de una entidad activa para representar en el mapa.</summary>
public sealed record EntityMapPointDto(
    string Name,
    string EntityRole,
    double Lat,
    double Lng
);

/// <summary>Zona DUM activa con su GeoJSON para el mapa del dashboard.</summary>
public sealed record DumZoneMapDto(
    string ZoneCode,
    string Name,
    string GeometryJson
);
