using GreenTransit.Application.Features.EntryCACs.DTOs;
using GreenTransit.Application.Features.EntryPlants.DTOs;
using GreenTransit.Application.Features.TreatmentPlants.DTOs;

namespace GreenTransit.Application.Features.WasteMoves.DTOs;

// ── ServiceOrder snapshot ─────────────────────────────────────────────────────

/// <summary>Resumen de la Orden de Servicio vinculada al traslado.</summary>
public sealed record TimelineServiceOrderDto(
    Guid      Id,
    string    ServiceOrderNumber,
    DateTime  IssuedAt,
    string?   Status,
    string?   Priority,
    string?   IssuedByName,
    string?   IssuedByNationalId,
    string?   IssuedByCenterCode,
    string?   WasteStream,
    string?   LerCodeCode,
    string?   LerCodeDescription,
    bool      LerCodeIsDangerous,
    decimal?  EstimatedWeight,
    string?   VehicleRegistration,
    string?   VehicleType,
    string?   FuelType,
    string?   EuroClass,
    decimal?  TransportDistanceKm
);

// ── WasteMove residue (con datos de transporte y documentos) ──────────────────

/// <summary>Línea de residuo del traslado con todos los datos de transporte y documentos.</summary>
public sealed record TimelineResidueDto(
    Guid     Id,
    Guid?    IdResidue,
    string?  ResidueName,
    bool     IsDangerous,
    bool     IsRAEE,
    decimal? Weight,
    string?  MeasureUnit,
    string?  NTNumber,
    string?  DINumber,
    string?  DIPhase,
    string?  CarrierName,
    string?  VehicleRegistration,
    string?  VehicleType,
    string?  FuelType,
    string?  EuroClass,
    decimal? TransportDistance,
    decimal? TransportCarbonEmissions,
    string?  EmissionFactorVersion
);

// ── SettlementLine snapshot ───────────────────────────────────────────────────

/// <summary>Línea de liquidación vinculada al traslado.</summary>
public sealed record TimelineSettlementLineDto(
    Guid     Id,
    Guid     SettlementId,
    string?  SettlementNumber,
    string?  SettlementStatus,
    int?     ProductCategory,
    string?  LerCodeCode,
    decimal  WeightKg,
    decimal  PricePerKg,
    decimal  Amount,
    string?  EvidenceType
);

// ── Incident snapshot ─────────────────────────────────────────────────────────

/// <summary>Incidencia vinculada al traslado.</summary>
public sealed record TimelineIncidentDto(
    Guid      Id,
    string    Type,
    string    Severity,
    DateTime  OpenedAt,
    DateTime? ClosedAt,
    bool      IsOpen,
    string?   ReportedByName,
    string?   Description
);

// ── Root DTO ──────────────────────────────────────────────────────────────────

/// <summary>Vista 360º completa de un traslado de residuos.</summary>
public sealed record WasteMoveTimelineDto(
    // Cabecera del traslado
    Guid      Id,
    string?   WasteMoveReference,
    string?   CurrentStatus,
    Guid?     OwnerId,
    // Actores
    string?   SourceName,
    string?   SourceLatitude,
    string?   SourceLongitude,
    string?   DestinationName,
    string?   DestinationLatitude,
    string?   DestinationLongitude,
    string?   ScrapName,
    string?   OperatorTransferName,
    // Tiempos
    DateTime? RequestDate,
    DateTime? PlannedPickupStart,
    DateTime? ActualPickupStart,
    DateTime? GatheredDate,
    DateTime? PlantEntryDate,
    // Documentos del traslado
    string?   DocumentId,
    string?   DocumentHash,
    string?   SignatureStatus,
    // SO origen
    TimelineServiceOrderDto?               ServiceOrder,
    // Residuos del traslado (líneas de transporte)
    IReadOnlyList<TimelineResidueDto>      Residues,
    // Pasos del ciclo
    IReadOnlyList<EntryCACDetailDto>       EntryCACs,
    IReadOnlyList<EntryPlantDetailDto>     EntryPlants,
    IReadOnlyList<TreatmentPlantDetailDto> TreatmentPlants,
    // Economía e incidencias
    IReadOnlyList<TimelineSettlementLineDto> SettlementLines,
    IReadOnlyList<TimelineIncidentDto>       Incidents,
    // KPIs agregados
    decimal TotalCO2EmissionsKg,
    decimal TotalWeightIn,
    decimal TotalWeightReused,
    decimal TotalWeightValued,
    decimal TotalWeightRemove,
    // Fechas reales por estado para el stepper
    Dictionary<string, DateTime?> StatusDates
);
