namespace GreenTransit.Infrastructure.ExternalApis.EcoDataNet.Models;

public class SettlementLineItem
{
    public Guid      RemoteId        { get; set; }
    public Guid?     SettlementId    { get; set; }
    public int?      ProductCategory { get; set; }
    public string?   LerCode         { get; set; }
    public decimal?  WeightKg        { get; set; }
    public decimal?  PricePerKg      { get; set; }
    public decimal?  Amount          { get; set; }
    public string?   EvidenceType    { get; set; }
    public string?   SourceIdsJson   { get; set; }
}
