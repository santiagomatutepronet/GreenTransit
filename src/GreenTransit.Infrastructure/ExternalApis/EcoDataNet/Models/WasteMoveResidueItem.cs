namespace GreenTransit.Infrastructure.ExternalApis.EcoDataNet.Models;

public class WasteMoveResidueItem
{
    public Guid?    IdWasteMove      { get; set; }
    public string?  LerCode          { get; set; }
    public string?  LerCodeExtended  { get; set; }
    public bool?    Dangerous        { get; set; }
    public bool?    Raee             { get; set; }
    public int?     ProductUse       { get; set; }
    public int?     ProductCategory  { get; set; }
    public string?  Description      { get; set; }
    public string?  ResidueName      { get; set; }
    public decimal? Weight           { get; set; }
    public int?     MeasureUnit      { get; set; }
    public int?     Units            { get; set; }
    public decimal? UnitPriceKg      { get; set; }
    public string?  NtNumber         { get; set; }
    public string?  DiNumber         { get; set; }
    public string?  DiPhase          { get; set; }
    public string?  DangerousCode    { get; set; }
    public ThirdPartyRef?    Carrier       { get; set; }
    public TransportInfoItem? TransportInfo { get; set; }
}
