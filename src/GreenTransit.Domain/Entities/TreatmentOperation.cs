using GreenTransit.Domain.Interfaces;

namespace GreenTransit.Domain.Entities;

/// <summary>
/// Operaciones de tratamiento R1-R13 (valorización) y D1-D15 (eliminación). Tabla: TreatmentOperations
/// </summary>
public class TreatmentOperation : IAuditableEntity
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    /// <summary>Recovery | Disposal</summary>
    public string OperationType { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string? ShortDescription { get; set; }
    public bool IsRecycling { get; set; }
    public bool IsEnergyRecovery { get; set; }
    public bool IsPreparationForReuse { get; set; }
    public int? SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navegaciones inversas
    /// <summary>Traslados donde esta operación es la prevista en destino (WasteMoveResidues.IdTreatmentOperationDestiny).</summary>
    public ICollection<WasteMoveResidue> WasteMoveResidues { get; set; } = [];
    /// <summary>Tratamientos en planta donde se aplicó esta operación (TreatmentPlants.IdTreatmentOperation).</summary>
    public ICollection<TreatmentPlant> TreatmentPlants { get; set; } = [];
}
