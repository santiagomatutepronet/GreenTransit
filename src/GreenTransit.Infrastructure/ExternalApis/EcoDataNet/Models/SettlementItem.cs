namespace GreenTransit.Infrastructure.ExternalApis.EcoDataNet.Models;

public class SettlementItem
{
    public Guid      RemoteId            { get; set; }
    public Guid      OwnerId             { get; set; }
    public string?   SettlementNumber    { get; set; }
    public string?   Status              { get; set; }
    public Guid?     AgreementId         { get; set; }
    public int?      Year                { get; set; }
    public int?      Month               { get; set; }
    public string?   ScrapName           { get; set; }
    public string?   ScrapNationalId     { get; set; }
    public string?   PublicEntityName    { get; set; }
    public string?   PublicEntityNationalId { get; set; }
    public string?   Currency            { get; set; }
    public decimal?  BaseAmount          { get; set; }
    public decimal?  AdjustmentsAmount   { get; set; }
    public decimal?  TaxAmount           { get; set; }
    public decimal?  TotalAmount         { get; set; }
    public string?   EvidenceRefsJson    { get; set; }
    public string?   Validator           { get; set; }
    public string?   ValidationStatus    { get; set; }
    public DateTime? ValidatedAt         { get; set; }
    public string?   ValidationRef       { get; set; }
    public string?   SourceSystem        { get; set; }
    public int       Version             { get; set; }
    public string?   Hash                { get; set; }
    public DateTime  CreatedAt           { get; set; }
    public DateTime  UpdatedAt           { get; set; }

    public List<SettlementLineItem> Lines { get; set; } = [];
}
