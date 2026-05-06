using GreenTransit.Domain.Interfaces;

namespace GreenTransit.Domain.Entities;

/// <summary>
/// Catálogo maestro de residuos y productos. Tabla: Residues
/// ResidueType: Waste | Product | ProductSpec
/// </summary>
public class Residue : IAuditableEntity
{
    public Guid Id { get; set; }
    /// <summary>Waste | Product | ProductSpec</summary>
    public string ResidueType { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? Reference { get; set; }
    public Guid? IdLERCode { get; set; }
    public bool IsDangerous { get; set; }
    public bool IsRAEE { get; set; }
    public string? DangerousCode { get; set; }
    public string? FlowType { get; set; }
    public string? ProductUse { get; set; }
    public string? ProductCategory { get; set; }
    public decimal? WeightPerUnitKg { get; set; }
    public string? DefaultMeasureUnit { get; set; }
    public int? ReparabilityIndex { get; set; }
    /// <summary>Easy | Medium | Hard</summary>
    public string? DisassemblyEase { get; set; }
    public bool? ContainsHazardous { get; set; }
    public decimal? RecycledContentPercent { get; set; }
    public string? CompositionJson { get; set; }
    public string? PotentialLERCodesJson { get; set; }
    public string? MaterialsJson { get; set; }
    public Guid? IdProducer { get; set; }
    public string? ProducerRef { get; set; }
    public string? SourceSystem { get; set; }
    public bool IsActive { get; set; } = true;
    public int Version { get; set; } = 1;
    public string? Hash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int IdUser { get; set; }

    // FK salientes
    public LerCode? LerCode { get; set; }
    public BusinessEntity? Producer { get; set; }

    // Navegaciones inversas
    public ICollection<WasteMoveResidue> WasteMoveResidues { get; set; } = [];
    public ICollection<EntryPlantResidue> EntryPlantResidues { get; set; } = [];
    public ICollection<EntryCACResidue> EntryCACResidues { get; set; } = [];
    /// <summary>Fracción de entrada en tratamiento.</summary>
    public ICollection<TreatmentPlantResidue> TreatmentPlantResiduesAsInput { get; set; } = [];
    /// <summary>Fracción reutilizada en tratamiento.</summary>
    public ICollection<TreatmentPlantResidue> TreatmentPlantResiduesAsReused { get; set; } = [];
    /// <summary>Fracción valorizada en tratamiento.</summary>
    public ICollection<TreatmentPlantResidue> TreatmentPlantResiduesAsValued { get; set; } = [];
    /// <summary>Fracción de rechazo en tratamiento.</summary>
    public ICollection<TreatmentPlantResidue> TreatmentPlantResiduesAsRemove { get; set; } = [];
    public ICollection<Product> Products { get; set; } = [];
    public ICollection<ProductSpec> ProductSpecs { get; set; } = [];
}
