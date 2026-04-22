namespace GreenTransit.Domain.Entities;

/// <summary>Documentos firmados vinculados a acuerdos. Tabla: AgreementDocuments</summary>
public class AgreementDocument
{
    public Guid Id { get; set; }
    public Guid AgreementId { get; set; }
    public string DocumentType { get; set; } = null!;
    public string? DocumentId { get; set; }
    public string? DocumentHash { get; set; }
    public DateTime? SignedAt { get; set; }
    public string? SignatureProvider { get; set; }

    public Agreement Agreement { get; set; } = null!;
}
