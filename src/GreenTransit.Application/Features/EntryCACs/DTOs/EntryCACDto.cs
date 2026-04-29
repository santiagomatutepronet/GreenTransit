namespace GreenTransit.Application.Features.EntryCACs.DTOs;

/// <summary>DTO para el listado paginado de Entradas en CAC.</summary>
public sealed record EntryCACDto(
    Guid      Id,
    Guid      IdWasteMove,
    string?   WasteMoveReference,
    DateTime? CACEntryDate,
    string?   TypeContainer,
    decimal?  PriceContainer,
    string?   CollectionMethod,
    int       ResidueLineCount
);

/// <summary>DTO de detalle completo de una Entrada en CAC.</summary>
public sealed record EntryCACDetailDto(
    Guid      Id,
    Guid      IdWasteMove,
    string?   WasteMoveReference,
    Guid?     OwnerId,
    DateTime? CACEntryDate,
    string?   TypeContainer,
    decimal?  PriceContainer,
    string?   CollectionMethod,
    int       IdUser,
    DateTime? DateCreateSys,
    DateTime? DateModifiedSys,
    IReadOnlyList<EntryCACResidueDto> Residues
);

/// <summary>DTO de una línea de residuo de una Entrada en CAC.</summary>
public sealed record EntryCACResidueDto(
    Guid     Id,
    Guid     IdEntryCAC,
    Guid?    IdResidue,
    string?  ResidueName,
    decimal? Weight,
    string?  MeasureUnit,
    int?     Units,
    decimal? PriceWeight,
    decimal? PriceUnit
);
