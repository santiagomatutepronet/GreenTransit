namespace GreenTransit.Infrastructure.ExternalApis.EcoDataNet.Models;

public class TransportInfoItem
{
    public string?  VehicleRegistration        { get; set; }
    public string?  VehicleRegistrationTrailer { get; set; }
    public decimal? TransportDuration          { get; set; }
    public decimal? TransportDistance          { get; set; }
    public decimal? TransportCarbonEmissions   { get; set; }
}
