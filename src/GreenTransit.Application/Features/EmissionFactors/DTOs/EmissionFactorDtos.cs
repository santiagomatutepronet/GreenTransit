namespace GreenTransit.Application.Features.EmissionFactors.DTOs;

/// <summary>DTO de una línea de factor de emisión.</summary>
public sealed record EmissionFactorDto(
    Guid    Id,
    string  VehicleType,
    string  FuelType,
    string? EuroClass,
    string  Unit,
    decimal Value
);

/// <summary>DTO de cabecera de un set de factores.</summary>
public sealed record EmissionFactorSetDto(
    Guid       Id,
    string     FactorSetName,
    string     Version,
    string     Status,
    DateTime   ValidFrom,
    DateTime?  ValidTo,
    string?    Publisher,
    string?    Reference,
    string?    Methodology,
    int        FactorCount,
    DateTime   CreatedAt
);

/// <summary>DTO de detalle completo: cabecera + todas las líneas.</summary>
public sealed record EmissionFactorSetDetailDto(
    Guid                        Id,
    string                      FactorSetName,
    string                      Version,
    string                      Status,
    DateTime                    ValidFrom,
    DateTime?                   ValidTo,
    string?                     Publisher,
    string?                     Reference,
    string?                     Methodology,
    IReadOnlyList<EmissionFactorDto> Factors
);
