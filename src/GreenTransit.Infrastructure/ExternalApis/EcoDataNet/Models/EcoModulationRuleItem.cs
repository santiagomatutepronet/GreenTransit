namespace GreenTransit.Infrastructure.ExternalApis.EcoDataNet.Models;

public class EcoModulationRuleItem
{
    public Guid     RemoteId        { get; set; }
    public Guid?    RuleSetId       { get; set; }
    public string?  RuleCode        { get; set; }
    public int?     ProductCategory { get; set; }
    public string?  CriteriaJson    { get; set; }
    public string?  FeeImpactType   { get; set; }
    public decimal? FeeImpactValue  { get; set; }
    public DateTime CreatedAt       { get; set; }
}
