namespace GreenTransit.Application.Features.PlantEnergies.DTOs;

/// <summary>DTO de listado para un registro de consumo energético.</summary>
public sealed record PlantEnergyDto(
    Guid     Id,
    string   PlantName,
    string?  PlantCenterCode,
    int      Year,
    int?     Month,
    decimal  KwhTotal,
    string?  Source,
    string?  GridMixRef,
    string?  AllocationMethod,
    string?  Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

/// <summary>Resumen anual de consumo energético de una planta.</summary>
public sealed record PlantEnergySummaryDto(
    string          PlantCenterCode,
    int             Year,
    /// <summary>Array de 12 valores (índice 0 = enero … 11 = diciembre). null si no hay dato.</summary>
    decimal?[]      MonthlyKwh,
    decimal         TotalKwhYear,
    decimal         TotalCO2eKg,
    decimal         GridEmissionFactor
);
