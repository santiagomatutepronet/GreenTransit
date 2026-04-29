namespace GreenTransit.Application.Features.TreatmentPlants.DTOs;

/// <summary>DTO para el listado paginado de tratamientos en planta.</summary>
public sealed record TreatmentPlantDto(
    Guid      Id,
    Guid?     IdWasteMove,
    string?   WasteMoveReference,
    string?   TicketScale,
    DateTime? PlantTreatmentDate,
    string?   TreatmentOperationCode,
    string?   TreatmentOperationDescription,
    string?   TreatmentOperationType,
    int       ResidueLineCount,
    bool      HasIncident
);

/// <summary>DTO de detalle completo de un tratamiento en planta.</summary>
public sealed record TreatmentPlantDetailDto(
    Guid      Id,
    Guid?     IdWasteMove,
    string?   WasteMoveReference,
    string?   WasteMoveServiceStatus,
    Guid?     OwnerId,
    string?   TicketScale,
    DateTime? PlantTreatmentDate,
    Guid?     IdTreatmentOperation,
    string?   TreatmentOperationCode,
    string?   TreatmentOperationDescription,
    string?   TreatmentOperationType,
    bool      IsRecycling,
    bool      IsEnergyRecovery,
    bool      IsPreparationForReuse,
    decimal?  ImproperWeight,
    string?   QualityMetricsJson,
    string?   TypeContainer,
    decimal?  PriceContainer,
    Guid?     ServiceOrderId,
    Guid?     IncidentId,
    int       IdUser,
    DateTime? DateCreateSys,
    DateTime? DateModifiedSys,
    // KPIs calculados
    decimal   TotalWeightIn,
    decimal   TotalWeightReused,
    decimal   TotalWeightValued,
    decimal   TotalWeightRemove,
    decimal   RecyclingRate,
    decimal   ValorizationRate,
    decimal   RejectionRate,
    IReadOnlyList<TreatmentPlantResidueDto> Residues
);

/// <summary>DTO de una línea de tratamiento con las tres fracciones.</summary>
public sealed record TreatmentPlantResidueDto(
    Guid     Id,
    Guid     IdTreatmentPlant,
    // Entrada
    Guid?    IdResidue,
    string?  ResidueName,
    string?  Category,
    decimal? WeightTotal,
    string?  MeasureUnit,
    int?     Units,
    decimal? PriceWeight,
    decimal? PriceUnit,
    // Fracción reutilizada
    Guid?    IdResidueReused,
    string?  ResidueReusedName,
    decimal? WeightReused,
    string?  MeasureUnitReused,
    int?     UnitsReused,
    // Fracción valorizada
    Guid?    IdResidueValued,
    string?  ResidueValuedName,
    decimal? WeightValued,
    string?  MeasureUnitValued,
    int?     UnitsValued,
    // Fracción rechazo
    Guid?    IdResidueRemove,
    string?  ResidueRemoveName,
    decimal? WeightRemove,
    string?  MeasureUnitRemove,
    int?     UnitsRemove,
    // Balance calculado
    decimal  SumFractions,
    decimal  BalanceDiff,
    bool     BalanceOk
);
