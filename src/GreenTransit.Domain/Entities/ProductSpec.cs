using GreenTransit.Domain.Interfaces;

namespace GreenTransit.Domain.Entities;

/// <summary>Fichas técnicas de producto (ecodiseño). Tabla: ProductSpecs</summary>
public class ProductSpec : IAuditableEntity, ITenantEntity
{
    public Guid Id { get; set; }
    public Guid? OwnerId { get; set; }
    public string ProductRef { get; set; } = null!;
    public Guid? IdResidue { get; set; }
    public int? ProductUse { get; set; }
    public int? ProductCategory { get; set; }
    public string? CategoryRef { get; set; }
    public Guid? IdProducer { get; set; }
    public string? ProducerRef { get; set; }
    public string? Notes { get; set; }
    public string? SourceSystem { get; set; }
    public int Version { get; set; } = 1;
    public string? Hash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int IdUser { get; set; }

    public Residue? Residue { get; set; }
    public BusinessEntity? Producer { get; set; }
}
