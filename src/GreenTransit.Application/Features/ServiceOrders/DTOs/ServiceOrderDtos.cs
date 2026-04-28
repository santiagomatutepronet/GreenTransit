namespace GreenTransit.Application.Features.ServiceOrders.DTOs;

/// <summary>DTO para el listado paginado de órdenes de servicio.</summary>
public sealed record ServiceOrderDto(
    Guid      Id,
    string    ServiceOrderNumber,
    string    Status,
    string    Priority,
    DateTime  IssuedAt,
    DateTime? PlannedPickupStart,
    Guid?     IdPickupPoint,
    string?   PickupPointName,
    string?   WasteStream,
    decimal?  EstimatedWeight,
    int?      MeasureUnit,
    string?   WasteMoveReference,
    Guid?     IdLERCode,
    string?   LerCodeCode,
    string?   LerCodeDescription
);

/// <summary>DTO de una línea de residuo de una Orden de Servicio.</summary>
public sealed record ServiceOrderResidueDto(
    Guid     Id,
    Guid     IdServiceOrder,
    int      SortOrder,
    Guid?    IdLERCode,
    string?  LerCodeCode,
    string?  LerCodeDescription,
    bool     LerCodeIsDangerous,
    int?     ProductUse,
    int?     ProductCategory,
    decimal? EstimatedWeight,
    int?     MeasureUnit,
    int?     Units
);

/// <summary>DTO de detalle con todos los campos del mapa sección 3.1.</summary>
public sealed record ServiceOrderDetailDto(
    Guid      Id,
    string    ServiceOrderNumber,
    DateTime  IssuedAt,
    Guid?     IdIssuedBy,
    string?   IssuedByName,
    string?   IssuedByNationalId,
    string?   IssuedByCenterCode,
    string    Status,
    string    Priority,
    string?   WasteStream,
    string?   SubStream,
    Guid?     IdPickupPoint,
    string?   PickupPointName,
    DateTime? PlannedPickupStart,
    DateTime? PlannedPickupEnd,
    DateTime? PlannedDeliveryStart,
    DateTime? PlannedDeliveryEnd,
    string?   ContainersJson,
    Guid?     IdCarrier,
    string?   CarrierName,
    Guid?     IdPlannedPlant,
    string?   PlannedPlantName,
    string?   WasteMoveReference,
    string?   TicketScalePlanned,
    int       Version,
    DateTime  CreatedAt,
    DateTime  UpdatedAt,
    int       IdUser,
    IReadOnlyList<ServiceOrderResidueDto> Residues
);
