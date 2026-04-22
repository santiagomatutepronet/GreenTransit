using GreenTransit.Domain.Interfaces;

namespace GreenTransit.Domain.Entities;

/// <summary>Consumo energético de plantas. Tabla: PlantEnergies</summary>
public class PlantEnergy : IAuditableEntity, ITenantEntity
{
    public Guid Id { get; set; }
    public Guid? OwnerId { get; set; }
    public string PlantName { get; set; } = null!;
    public string? PlantCenterCode { get; set; }
    public int Year { get; set; }
    public int? Month { get; set; }
    public decimal KwhTotal { get; set; }
    public string? Source { get; set; }
    public string? GridMixRef { get; set; }
    public string? AllocationMethod { get; set; }
    public string? Notes { get; set; }
    public string? SourceSystem { get; set; }
    public int Version { get; set; } = 1;
    public string? Hash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int IdUser { get; set; }
}
