namespace GreenTransit.Infrastructure.ExternalApis.EcoDataNet.Models;

public class ProductItem
{
    public Guid     Id                    { get; set; }
    public Guid?    IdProductDeclaration  { get; set; }
    public string?  Description           { get; set; }
    public string?  Reference             { get; set; }
    public string?  Source                { get; set; }
    public decimal? Quantity              { get; set; }
    public decimal? Price                 { get; set; }
    public string?  MeasureUnit           { get; set; }
    public int?     Units                 { get; set; }
    public string?  ProductUse            { get; set; }
    public string?  ProductCategory       { get; set; }
    public decimal? WeightPerUnitKg       { get; set; }
    public decimal? ReparabilityIndex     { get; set; }
    public decimal? RecycledContentPercent { get; set; }
    public string?  MaterialsJson         { get; set; }
}
