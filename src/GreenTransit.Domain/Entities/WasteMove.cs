using GreenTransit.Domain.Interfaces;

namespace GreenTransit.Domain.Entities;

/// <summary>Traslados de residuos origen → destino. Tabla: WasteMoves</summary>
public class WasteMove : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid? OwnerId { get; set; }
    public DateTime? GatheredDate { get; set; }
    public DateTime? RequestDate { get; set; }
    public DateTime? PlantEntryDate { get; set; }
    public Guid? IdScrap { get; set; }
    public Guid? IdScrap2 { get; set; }
    public Guid? IdSource { get; set; }
    public Guid? IdDestination { get; set; }
    public Guid? IdOperatorTransfer { get; set; }
    public string? WasteMoveReference { get; set; }
    public string? Lot { get; set; }
    public DateTime? PlannedPickupStart { get; set; }
    public DateTime? PlannedPickupEnd { get; set; }
    public DateTime? PlannedDeliveryStart { get; set; }
    public DateTime? PlannedDeliveryEnd { get; set; }
    public DateTime? ActualPickupStart { get; set; }
    public DateTime? ActualPickupEnd { get; set; }
    public DateTime? ActualDeliveryStart { get; set; }
    public DateTime? ActualDeliveryEnd { get; set; }
    public string? DocumentId { get; set; }
    public string? DocumentHash { get; set; }
    public string? SignatureStatus { get; set; }
    public DateTime? DateCreateSys { get; set; }
    public DateTime? DateModifiedSys { get; set; }
    public int IdUser { get; set; }
    public Guid? ServiceOrderId { get; set; }
    public string? ServiceStatus { get; set; }
    public string? SourceSystem { get; set; }
    public int Version { get; set; } = 1;

    public BusinessEntity? Scrap { get; set; }
    public BusinessEntity? Scrap2 { get; set; }
    public BusinessEntity? Source { get; set; }
    public BusinessEntity? Destination { get; set; }
    public BusinessEntity? OperatorTransfer { get; set; }
    public ServiceOrder? ServiceOrder { get; set; }
    public ICollection<WasteMoveResidue> WasteMoveResidues { get; set; } = [];
}
