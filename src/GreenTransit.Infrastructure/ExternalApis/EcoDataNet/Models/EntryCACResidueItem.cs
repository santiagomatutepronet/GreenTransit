namespace GreenTransit.Infrastructure.ExternalApis.EcoDataNet.Models;

public class EntryCACResidueItem
{
    public Guid?    IdEntryCAC       { get; set; }
    public string?  ResidueName      { get; set; }
    public string?  LerCode          { get; set; }
    public string?  LerCodeExtended  { get; set; }
    public decimal? Weight           { get; set; }
    public int?     MeasureUnit      { get; set; }
    public int?     Units            { get; set; }
    public decimal? PriceWeight      { get; set; }
    public decimal? PriceUnit        { get; set; }
    public bool?    Dangerous        { get; set; }
    public bool?    Raee             { get; set; }
    public int?     ProductCategory  { get; set; }
}
