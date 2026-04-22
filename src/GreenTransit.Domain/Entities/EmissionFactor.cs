namespace GreenTransit.Domain.Entities;

/// <summary>Factores de emisión por tipo de vehículo/combustible/Euro class. Tabla: EmissionFactors</summary>
public class EmissionFactor
{
    public Guid Id { get; set; }
    public Guid FactorSetId { get; set; }
    public string VehicleType { get; set; } = null!;
    public string FuelType { get; set; } = null!;
    public string? EuroClass { get; set; }
    public string Unit { get; set; } = null!;
    public decimal Value { get; set; }
    public DateTime CreatedAt { get; set; }

    public EmissionFactorSet FactorSet { get; set; } = null!;
}
