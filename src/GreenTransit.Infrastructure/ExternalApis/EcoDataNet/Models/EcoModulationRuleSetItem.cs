namespace GreenTransit.Infrastructure.ExternalApis.EcoDataNet.Models;

public class EcoModulationRuleSetItem
{
    public Guid      RemoteId              { get; set; }
    public Guid      OwnerId               { get; set; }
    public string?   RuleSetName           { get; set; }
    public string?   Version               { get; set; }
    public string?   Status                { get; set; }
    public DateTime? ValidFrom             { get; set; }
    public DateTime? ValidTo               { get; set; }
    public string?   PublisherName         { get; set; }
    public string?   PublisherNationalId   { get; set; }
    public string?   PublisherCenterCode   { get; set; }
    public string?   SourceSystem          { get; set; }
    public string?   Hash                  { get; set; }
    public DateTime  CreatedAt             { get; set; }
    public DateTime  UpdatedAt             { get; set; }

    public List<EcoModulationRuleItem> Rules { get; set; } = [];
}
