namespace GreenTransit.Infrastructure.ExternalApis.EcoDataNet.Models;

public class DumZoneItem
{
    public Guid     RemoteId      { get; set; }
    public Guid     OwnerId       { get; set; }
    public string?  ZoneCode      { get; set; }
    public string?  Name          { get; set; }
    public string?  Description   { get; set; }
    public string?  Status        { get; set; }
    public string?  GeometryJson  { get; set; }
    public string?  SourceSystem  { get; set; }
    public int      Version       { get; set; }
    public string?  Hash          { get; set; }
    public DateTime CreatedAt     { get; set; }
    public DateTime UpdatedAt     { get; set; }

    public List<DumRestrictionRuleItem> Rules { get; set; } = [];
}
