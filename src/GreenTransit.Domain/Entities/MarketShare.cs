using GreenTransit.Domain.Interfaces;

namespace GreenTransit.Domain.Entities;

/// <summary>Cuotas de mercado por categoría y SCRAP. Tabla: MarketShares</summary>
public class MarketShare : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid? OwnerId { get; set; }
    public Guid? IdScrap { get; set; }
    public string Category { get; set; } = null!;
    public string? AutonomousCommunity { get; set; }
    public int Year { get; set; }
    public decimal Weight { get; set; }
    public int? Period { get; set; }
    public DateOnly? EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public string? FlowType { get; set; }
    public string? SourceSystem { get; set; }
    public int Version { get; set; } = 1;

    public BusinessEntity? Scrap { get; set; }
}
