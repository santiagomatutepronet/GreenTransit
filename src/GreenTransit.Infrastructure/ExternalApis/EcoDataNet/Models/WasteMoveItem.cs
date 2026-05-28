namespace GreenTransit.Infrastructure.ExternalApis.EcoDataNet.Models;

public class WasteMoveItem
{
    public Guid     RemoteId              { get; set; }
    public Guid     OwnerId               { get; set; }
    public DateTime? GatheredDate         { get; set; }
    public DateTime? RequestDate          { get; set; }
    public DateTime? PlantEntryDate       { get; set; }
    public string?  WasteMoveReference    { get; set; }
    public string?  Lot                   { get; set; }
    public Guid?    ServiceOrderId        { get; set; }
    public string?  ServiceStatus         { get; set; }
    public DateTime? PlannedPickupStart   { get; set; }
    public DateTime? PlannedPickupEnd     { get; set; }
    public DateTime? PlannedDeliveryStart { get; set; }
    public DateTime? PlannedDeliveryEnd   { get; set; }
    public DateTime? ActualPickupStart    { get; set; }
    public DateTime? ActualPickupEnd      { get; set; }
    public DateTime? ActualDeliveryStart  { get; set; }
    public DateTime? ActualDeliveryEnd    { get; set; }
    public string?  DocumentId            { get; set; }
    public string?  DocumentHash          { get; set; }
    public string?  SignatureStatus       { get; set; }
    public string?  SourceSystem          { get; set; }
    public int      Version               { get; set; }

    public ThirdPartyRef? Scrap           { get; set; }
    public ThirdPartyRef? Scrap2          { get; set; }
    public ThirdPartyRef? Source          { get; set; }
    public ThirdPartyRef? Destination     { get; set; }
    public ThirdPartyRef? OperatorTransfer { get; set; }

    public List<WasteMoveResidueItem> Residues { get; set; } = [];
}
