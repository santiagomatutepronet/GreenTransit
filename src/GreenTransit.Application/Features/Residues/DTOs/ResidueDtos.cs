namespace GreenTransit.Application.Features.Residues.DTOs;

/// <summary>DTO para el listado de residuos/productos.</summary>
public sealed record ResidueDto(
    Guid    Id,
    string  ResidueType,
    string  Name,
    string? Reference,
    string? LerCode,
    string? LerDescription,
    bool    IsDangerous,
    bool    IsRAEE,
    string? ProducerName,
    bool    IsActive
);

/// <summary>DTO de detalle con todos los campos.</summary>
public sealed record ResidueDetailDto(
    Guid     Id,
    string   ResidueType,
    string   Name,
    string?  Description,
    string?  Reference,
    Guid?    IdLERCode,
    string?  LerCode,
    string?  LerDescription,
    bool     IsDangerous,
    bool     IsRAEE,
    string?  DangerousCode,
    string?  ProductUse,
    string?  ProductCategory,
    decimal? WeightPerUnitKg,
    string?  DefaultMeasureUnit,
    // Ecodiseño (solo ProductSpec)
    int?     ReparabilityIndex,
    string?  DisassemblyEase,
    bool?    ContainsHazardous,
    decimal? RecycledContentPercent,
    string?  CompositionJson,
    string?  PotentialLERCodesJson,
    string?  MaterialsJson,
    // Productor (solo ProductSpec)
    Guid?    IdProducer,
    string?  ProducerName,
    string?  ProducerRef,
    string?  SourceSystem,
    bool     IsActive,
    int      Version,
    string?  Hash,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int      IdUser
);
