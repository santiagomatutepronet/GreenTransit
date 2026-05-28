namespace GreenTransit.Infrastructure.ExternalApis.EcoDataNet.Models;

public class DumRestrictionRuleItem
{
    public Guid      RemoteId       { get; set; }
    public Guid      OwnerId        { get; set; }
    public string?   RuleCode       { get; set; }
    public string?   Status         { get; set; }
    public Guid?     ZoneId         { get; set; }
    public DateTime? ValidFrom      { get; set; }
    public DateTime? ValidTo        { get; set; }
    public string?   ConditionsJson { get; set; }
    public string?   ActionType     { get; set; }
    public string?   ActionReason   { get; set; }
    public string?   SourceSystem   { get; set; }
    public int       Version        { get; set; }
    public string?   Hash           { get; set; }
    public DateTime  CreatedAt      { get; set; }
    public DateTime  UpdatedAt      { get; set; }
}
