namespace GreenTransit.Infrastructure.ExternalApis.EcoDataNet.Models;

public class MarketShareItem
{
    public Guid     RemoteId           { get; set; }
    public Guid     OwnerId            { get; set; }
    public string?  Scrap              { get; set; }
    public int?     Category           { get; set; }
    public string?  AutonomousCommunity { get; set; }
    public int?     Year               { get; set; }
    public decimal? Weight             { get; set; }
}
