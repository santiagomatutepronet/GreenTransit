namespace GreenTransit.Domain.Entities;

/// <summary>Líneas de liquidación por categoría LER. Tabla: SettlementLines</summary>
public class SettlementLine
{
    public Guid Id { get; set; }
    public Guid SettlementId { get; set; }
    public int? ProductCategory { get; set; }
    public Guid? IdLERCode { get; set; }
    public decimal WeightKg { get; set; }
    public decimal PricePerKg { get; set; }
    public decimal Amount { get; set; }
    public string? EvidenceType { get; set; }
    public string? SourceIdsJson { get; set; }

    public Settlement Settlement { get; set; } = null!;
    public LerCode? LerCode { get; set; }
}
