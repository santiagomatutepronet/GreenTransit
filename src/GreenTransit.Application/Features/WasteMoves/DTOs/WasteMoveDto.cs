namespace GreenTransit.Application.Features.WasteMoves.DTOs;

/// <summary>DTO para el listado paginado de Traslados.</summary>
public sealed record WasteMoveDto(
    Guid      Id,
    string?   WasteMoveReference,
    string?   ServiceStatus,
    Guid?     IdSource,
    string?   SourceName,
    Guid?     IdDestination,
    string?   DestinationName,
    DateTime? PlannedPickupStart,
    DateTime? RequestDate,
    int       ResidueCount
);

/// <summary>DTO de detalle completo de un Traslado, incluyendo líneas de residuos.</summary>
public sealed record WasteMoveDetailDto(
    Guid      Id,
    string?   WasteMoveReference,
    string?   ServiceStatus,
    Guid?     OwnerId,
    Guid?     IdSource,
    string?   SourceName,
    Guid?     IdDestination,
    string?   DestinationName,
    Guid?     IdScrap,
    string?   ScrapName,
    Guid?     IdScrap2,
    string?   Scrap2Name,
    Guid?     IdOperatorTransfer,
    string?   OperatorTransferName,
    Guid?     ServiceOrderId,
    string?   ServiceOrderNumber,
    // LER code heredado de la SO vinculada
    Guid?     IdLerCode,
    string?   LerCodeCode,
    string?   LerCodeDescription,
    bool      LerCodeIsDangerous,
    DateTime? RequestDate,
    DateTime? PlannedPickupStart,
    DateTime? PlannedPickupEnd,
    DateTime? PlannedDeliveryStart,
    DateTime? PlannedDeliveryEnd,
    DateTime? ActualPickupStart,
    DateTime? ActualPickupEnd,
    DateTime? ActualDeliveryStart,
    DateTime? ActualDeliveryEnd,
    string?   Lot,
    string?   DocumentId,
    string?   SignatureStatus,
    int       Version,
    DateTime? DateCreateSys,
    DateTime? DateModifiedSys,
    int       IdUser,
    IReadOnlyList<WasteMoveResidueDto> Residues
);

/// <summary>DTO de una línea de residuo de un Traslado.</summary>
public sealed record WasteMoveResidueDto(
    Guid      Id,
    Guid      IdWasteMove,
    Guid?     IdResidue,
    string?   ResidueName,
    bool      IsDangerous,
    bool      IsRAEE,
    decimal?  Weight,
    string?   MeasureUnit,
    int?      Units,
    decimal?  UnitPriceKg,
    DateTime? DateDelivery,
    Guid?     IdTreatmentOperationDestiny,
    string?   TreatmentOperationCode,
    string?   TreatmentOperationDescription,
    Guid?     IdCarrier,
    string?   CarrierName,
    string?   NTNumber,
    string?   DINumber,
    string?   DIPhase,
    string?   VehicleType,
    string?   FuelType,
    string?   EuroClass,
    decimal?  TransportDistance,
    decimal?  TransportCarbonEmissions,
    Guid?     EmissionFactorSetId,
    string?   EmissionFactorVersion
);

/// <summary>Input de línea para ConfirmPickupCommand.</summary>
public sealed record ConfirmPickupLineInput(
    Guid    WasteMoveResidueId,
    string? NTNumber,
    string? DINumber,
    string? DIPhase
);

/// <summary>DTO de estimación de emisiones CO₂ para el panel previo a confirmar recogida.</summary>
public sealed record EmissionEstimateDto(
    Guid    WasteMoveResidueId,
    string? ResidueName,
    decimal DistanceKm,
    decimal EmissionFactor,
    decimal EstimatedKgCO2e,
    string  FactorSetVersion
);
