namespace GreenTransit.Application.Features.EntryPlants.DTOs;

/// <summary>DTO para el listado paginado de Entradas en Planta.</summary>
public sealed record EntryPlantDto(
    Guid      Id,
    Guid      IdWasteMove,
    string?   WasteMoveReference,
    string?   TicketScale,
    string?   WeighbridgeId,
    DateTime? PlantEntryDate,
    decimal?  GrossWeight,
    decimal?  TareWeight,
    decimal?  NetWeight,
    string?   TypeContainer,
    int       ResidueLineCount
);

/// <summary>DTO de detalle completo de una Entrada en Planta.</summary>
public sealed record EntryPlantDetailDto(
    Guid      Id,
    Guid      IdWasteMove,
    string?   WasteMoveReference,
    string?   WasteMoveServiceStatus,
    string?   SourceName,
    Guid?     OwnerId,
    string?   TicketScale,
    string?   WeighbridgeId,
    DateTime? PlantEntryDate,
    decimal?  GrossWeight,
    decimal?  TareWeight,
    decimal?  NetWeight,
    string?   TypeContainer,
    decimal?  PriceContainer,
    Guid?     ServiceOrderId,
    int       IdUser,
    DateTime? DateCreateSys,
    DateTime? DateModifiedSys,
    bool      HasWeightDiscrepancy,
    IReadOnlyList<EntryPlantResidueDto> Residues
);

/// <summary>DTO de una línea de residuo de una Entrada en Planta.</summary>
public sealed record EntryPlantResidueDto(
    Guid     Id,
    Guid     IdEntryPlant,
    Guid?    IdResidue,
    string?  ResidueName,
    decimal? Weight,
    string?  MeasureUnit,
    int?     Units,
    decimal? PriceWeight,
    decimal? PriceUnit
);
