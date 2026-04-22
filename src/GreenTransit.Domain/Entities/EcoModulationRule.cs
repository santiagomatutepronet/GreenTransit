namespace GreenTransit.Domain.Entities;

/// <summary>Reglas individuales de ecomodulación. Tabla: EcoModulationRules</summary>
public class EcoModulationRule
{
    public Guid Id { get; set; }
    public Guid RuleSetId { get; set; }
    public string RuleCode { get; set; } = null!;
    public int? ProductCategory { get; set; }
    public string CriteriaJson { get; set; } = null!;
    public string FeeImpactType { get; set; } = null!;
    public decimal FeeImpactValue { get; set; }
    public DateTime CreatedAt { get; set; }

    public EcoModulationRuleSet RuleSet { get; set; } = null!;
}
