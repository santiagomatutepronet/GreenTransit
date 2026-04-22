using GreenTransit.Domain.Interfaces;

namespace GreenTransit.Domain.Entities;

/// <summary>Liquidaciones económicas vinculadas a acuerdos. Tabla: Settlements</summary>
public class Settlement : IAuditableEntity, ITenantEntity
{
    public Guid Id { get; set; }
    public Guid? OwnerId { get; set; }
    public string SettlementNumber { get; set; } = null!;
    public string Status { get; set; } = null!;
    public Guid AgreementId { get; set; }
    public int Year { get; set; }
    public int? Month { get; set; }
    public Guid? IdScrap { get; set; }
    public Guid? IdPublicEntity { get; set; }
    public string Currency { get; set; } = "EUR";
    public decimal BaseAmount { get; set; }
    public decimal AdjustmentsAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? EvidenceRefsJson { get; set; }
    public string? Validator { get; set; }
    public string? ValidationStatus { get; set; }
    public DateTime? ValidatedAt { get; set; }
    public string? ValidationRef { get; set; }
    public string? SourceSystem { get; set; }
    public int Version { get; set; } = 1;
    public string? Hash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int IdUser { get; set; }

    public Agreement Agreement { get; set; } = null!;
    public BusinessEntity? Scrap { get; set; }
    public BusinessEntity? PublicEntity { get; set; }
    public ICollection<SettlementLine> SettlementLines { get; set; } = [];
}
