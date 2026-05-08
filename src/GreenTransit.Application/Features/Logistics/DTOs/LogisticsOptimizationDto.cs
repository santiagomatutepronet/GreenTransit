namespace GreenTransit.Application.Features.Logistics.DTOs;

// ── DTOs del Dashboard 1 — Panel de Optimización Logística SCRAP ─────────────

/// <summary>DTO raíz del Dashboard 1. Agrupa todos los KPIs en una sola respuesta.</summary>
public sealed record LogisticsOptimizationDto(
    /// <summary>KPIs de eficiencia de rutas del periodo.</summary>
    RouteEfficiencyDto RouteEfficiency,

    /// <summary>Volumen RAEE (kg) agrupado por zona geográfica (ProvinceCode).</summary>
    IReadOnlyList<VolumeByZoneDto> VolumeByZone,

    /// <summary>Puntos de recogida y plantas para el mapa interactivo.</summary>
    IReadOnlyList<LogisticsMapPointDto> MapPoints,

    /// <summary>Zonas DUM activas con su geometría y color de acción.</summary>
    IReadOnlyList<DumZoneLayerDto> DumZones,

    /// <summary>Cumplimiento de ventanas horarias DUM.</summary>
    DumComplianceDto DumCompliance,

    /// <summary>Distribución de llegadas a planta por día de semana y hora (heatmap).</summary>
    IReadOnlyList<PlantArrivalHeatmapDto> PlantArrivalHeatmap,

    /// <summary>Incidencias logísticas abiertas.</summary>
    IReadOnlyList<OpenLogisticsIncidentDto> OpenIncidents,

    /// <summary>Utilización por tipo de vehículo.</summary>
    IReadOnlyList<VehicleUtilizationDto> VehicleUtilization,

    /// <summary>Año y mes del filtro aplicado.</summary>
    int Year,
    int? Month
);

/// <summary>KPIs globales de eficiencia de rutas.</summary>
public sealed record RouteEfficiencyDto(
    decimal AvgDistanceKmPerPickup,
    decimal AvgCO2eKgPerPickup,
    decimal CO2eKgPerTonne,
    decimal TotalCO2eKg,
    decimal TotalDistanceKm,
    int     TotalPickups,
    /// <summary>Variación % de CO₂ respecto al periodo anterior.</summary>
    double? CO2eTrendPercent
);

/// <summary>Volumen RAEE acumulado por provincia.</summary>
public sealed record VolumeByZoneDto(
    string  ProvinceCode,
    string? ProvinceName,
    decimal TotalKg,
    int     PickupCount
);

/// <summary>Punto geográfico para el mapa (punto de recogida o planta).</summary>
public sealed record LogisticsMapPointDto(
    Guid    Id,
    string  Name,
    string  EntityRole,
    double? Latitude,
    double? Longitude,
    string? Address,
    decimal AccumulatedKg,
    int     UpcomingPickups
);

/// <summary>Zona DUM con geometría y semáforo de acción.</summary>
public sealed record DumZoneLayerDto(
    Guid    Id,
    string  ZoneCode,
    string? GeometryJson,
    /// <summary>Block | Restrict | Allow | Notify.</summary>
    string  ActionType,
    /// <summary>Color CSS derivado de ActionType: rojo, naranja, verde, azul.</summary>
    string  Color
);

/// <summary>Distribución de cumplimiento de ventanas DUM.</summary>
public sealed record DumComplianceDto(
    int WithinWindow,
    int OutsideWindow,
    int NoZoneApplicable
);

/// <summary>Conteo de llegadas a planta para el heatmap semanal.</summary>
public sealed record PlantArrivalHeatmapDto(
    /// <summary>0=Domingo … 6=Sábado.</summary>
    int DayOfWeek,
    int Hour,
    int Count
);

/// <summary>Incidencia logística abierta.</summary>
public sealed record OpenLogisticsIncidentDto(
    Guid      Id,
    string?   WasteMoveReference,
    string    Type,
    string    Severity,
    DateTime  OpenedAt,
    int       DaysOpen
);

/// <summary>Utilización por tipo de vehículo.</summary>
public sealed record VehicleUtilizationDto(
    string? VehicleType,
    int     TripCount,
    decimal TotalKg,
    decimal TotalDistanceKm
);
