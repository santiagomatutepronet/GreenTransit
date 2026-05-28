namespace GreenTransit.Infrastructure.ExternalApis.EcoDataNet.Models;

public class AgreementDocumentItem
{
    public Guid      RemoteId           { get; set; }
    public Guid?     AgreementId        { get; set; }
    public string?   DocumentType       { get; set; }
    public string?   DocumentId         { get; set; }
    public string?   DocumentHash       { get; set; }
    public DateTime? SignedAt           { get; set; }
    public string?   SignatureProvider  { get; set; }
}
