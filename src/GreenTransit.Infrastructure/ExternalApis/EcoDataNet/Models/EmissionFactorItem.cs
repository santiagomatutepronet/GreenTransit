namespace GreenTransit.Infrastructure.ExternalApis.EcoDataNet.Models;

public class EmissionFactorItem
{
    public Guid     RemoteId    { get; set; }
    public Guid?    FactorSetId { get; set; }
    public string?  VehicleType { get; set; }
    public string?  FuelType    { get; set; }
    public string?  EuroClass   { get; set; }
    public string?  Unit        { get; set; }
    public decimal? Value       { get; set; }
    public DateTime CreatedAt   { get; set; }
}
