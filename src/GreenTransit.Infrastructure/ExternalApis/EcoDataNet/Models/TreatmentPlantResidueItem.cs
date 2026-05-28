namespace GreenTransit.Infrastructure.ExternalApis.EcoDataNet.Models;

public class TreatmentPlantResidueItem
{
    public Guid?    IdTreatmentPlant   { get; set; }
    public string?  ResidueName        { get; set; }
    public string?  LerCode            { get; set; }
    public string?  LerCodeExtended    { get; set; }
    public int?     Category           { get; set; }
    public decimal? WeightTotal        { get; set; }
    public int?     MeasureUnit        { get; set; }
    public int?     Units              { get; set; }
    public decimal? PriceWeight        { get; set; }
    public decimal? PriceUnit          { get; set; }
    public decimal? WeightReused       { get; set; }
    public int?     MeasureUnitReused  { get; set; }
    public int?     UnitsReused        { get; set; }
}
