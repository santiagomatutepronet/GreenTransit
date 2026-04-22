using GreenTransit.Domain.Interfaces;

namespace GreenTransit.Domain.Entities;

/// <summary>Conjuntos de reglas de ecomodulación de tarifas. Tabla: EcoModulationRuleSets</summary>
public class EcoModulationRuleSet : IAuditableEntity, ITenantEntity
{
    public Guid Id { get; set; }
    public Guid? OwnerId { get; set; }
    public string RuleSetName { get; set; } = null!;
    public string Version { get; set; } = null!;
    public string Status { get; set; } = "Active";
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public string? PublisherName { get; set; }
    public string? PublisherNationalId { get; set; }
    public string? PublisherCenterCode { get; set; }
    public string? SourceSystem { get; set; }
    public string? Hash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int IdUser { get; set; }

    public ICollection<EcoModulationRule> EcoModulationRules { get; set; } = [];
}
