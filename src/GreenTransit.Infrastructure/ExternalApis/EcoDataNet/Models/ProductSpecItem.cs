namespace GreenTransit.Infrastructure.ExternalApis.EcoDataNet.Models;

public class ProductSpecItem
{
    public Guid      RemoteId                { get; set; }
    public Guid      OwnerId                 { get; set; }
    public string?   ProductRef              { get; set; }
    public int?      ProductUse              { get; set; }
    public int?      ProductCategory         { get; set; }
    public string?   CategoryRef             { get; set; }
    public string?   ProducerName            { get; set; }
    public string?   ProducerNationalId      { get; set; }
    public string?   ProducerRef             { get; set; }
    public string?   CompositionJson         { get; set; }
    public decimal?  WeightPerUnitKg         { get; set; }
    public decimal?  ReparabilityIndex       { get; set; }
    public decimal?  DisassemblyEase         { get; set; }
    public bool?     ContainsHazardous       { get; set; }
    public string?   PotentialLERCodesJson   { get; set; }
    public string?   Notes                   { get; set; }
    public string?   SourceSystem            { get; set; }
    public int       Version                 { get; set; }
    public string?   Hash                    { get; set; }
    public DateTime  CreatedAt               { get; set; }
    public DateTime  UpdatedAt               { get; set; }
}
