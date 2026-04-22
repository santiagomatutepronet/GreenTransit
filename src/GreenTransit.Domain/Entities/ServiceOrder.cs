using GreenTransit.Domain.Interfaces;

namespace GreenTransit.Domain.Entities;

/// <summary>Órdenes de servicio de recogida/transporte. Tabla: ServiceOrders</summary>
public class ServiceOrder : IAuditableEntity, ITenantEntity
{
    public Guid Id { get; set; }
    public Guid? OwnerId { get; set; }
    public string ServiceOrderNumber { get; set; } = null!;
    public DateTime IssuedAt { get; set; }
    public Guid? IdIssuedBy { get; set; }
    public string? IssuedByName { get; set; }
    public string? IssuedByNationalId { get; set; }
    public string? IssuedByCenterCode { get; set; }
    public string Status { get; set; } = null!;
    public string Priority { get; set; } = "Normal";
    public string? WasteStream { get; set; }
    public string? SubStream { get; set; }
    public int? ProductUse { get; set; }
    public int? ProductCategory { get; set; }
    public Guid? IdLERCode { get; set; }
    public Guid? IdPickupPoint { get; set; }
    public DateTime? PlannedPickupStart { get; set; }
    public DateTime? PlannedPickupEnd { get; set; }
    public DateTime? PlannedDeliveryStart { get; set; }
    public DateTime? PlannedDeliveryEnd { get; set; }
    public decimal? EstimatedWeight { get; set; }
    public int? MeasureUnit { get; set; }
    public int? Units { get; set; }
    public string? ContainersJson { get; set; }
    public Guid? IdCarrier { get; set; }
    public Guid? IdPlannedPlant { get; set; }
    public string? WasteMoveReference { get; set; }
    public string? TicketScalePlanned { get; set; }
    public DateTime? ActualPickupStart { get; set; }
    public DateTime? ActualPickupEnd { get; set; }
    public DateTime? ActualDeliveryStart { get; set; }
    public DateTime? ActualDeliveryEnd { get; set; }
    public decimal? TransportDistanceKm { get; set; }
    public int? TransportDurationMin { get; set; }
    public string? VehicleRegistration { get; set; }
    public string? VehicleType { get; set; }
    public string? FuelType { get; set; }
    public string? EuroClass { get; set; }
    public string? SourceSystem { get; set; }
    public int Version { get; set; } = 1;
    public string? Hash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int IdUser { get; set; }

    public BusinessEntity? IssuedBy { get; set; }
    public BusinessEntity? PickupPoint { get; set; }
    public BusinessEntity? Carrier { get; set; }
    public BusinessEntity? PlannedPlant { get; set; }
    public LerCode? LerCode { get; set; }
}
