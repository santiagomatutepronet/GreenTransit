namespace GreenTransit.Infrastructure.ExternalApis.EcoDataNet.Models;

public class IncidentItem
{
    public Guid      RemoteId               { get; set; }
    public Guid      OwnerId                { get; set; }
    public string?   Type                   { get; set; }
    public string?   Severity               { get; set; }
    public DateTime? OpenedAt               { get; set; }
    public DateTime? ClosedAt               { get; set; }
    public Guid?     ServiceOrderId         { get; set; }
    public string?   WasteMoveReference     { get; set; }
    public string?   TicketScale            { get; set; }
    public string?   ReportedByName         { get; set; }
    public string?   ReportedByNationalId   { get; set; }
    public string?   ReportedByCenterCode   { get; set; }
    public string?   Description            { get; set; }
    public string?   ResolutionJson         { get; set; }
    public string?   SourceSystem           { get; set; }
    public int       Version                { get; set; }
    public string?   Hash                   { get; set; }
    public DateTime  CreatedAt              { get; set; }
    public DateTime  UpdatedAt              { get; set; }
}
