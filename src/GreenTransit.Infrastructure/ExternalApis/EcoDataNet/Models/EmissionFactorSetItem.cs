namespace GreenTransit.Infrastructure.ExternalApis.EcoDataNet.Models;

public class EmissionFactorSetItem
{
    public Guid      RemoteId      { get; set; }
    public Guid      OwnerId       { get; set; }
    public string?   FactorSetName { get; set; }
    public string?   Version       { get; set; }
    public string?   Status        { get; set; }
    public DateTime? ValidFrom     { get; set; }
    public DateTime? ValidTo       { get; set; }
    public string?   Publisher     { get; set; }
    public string?   Reference     { get; set; }
    public string?   Methodology   { get; set; }
    public string?   SourceSystem  { get; set; }
    public string?   Hash          { get; set; }
    public DateTime  CreatedAt     { get; set; }
    public DateTime  UpdatedAt     { get; set; }

    public List<EmissionFactorItem> Factors { get; set; } = [];
}
