using GreenTransit.Domain.Interfaces;

namespace GreenTransit.Domain.Entities;

/// <summary>Reglas de restricción de acceso por zona DUM. Tabla: DUMRestrictionRules</summary>
public class DumRestrictionRule : IAuditableEntity, ITenantEntity
{
    public Guid Id { get; set; }
    public Guid? OwnerId { get; set; }
    public string RuleCode { get; set; } = null!;
    public string Status { get; set; } = "Active";
    public Guid ZoneId { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public string ConditionsJson { get; set; } = null!;
    public string ActionType { get; set; } = null!;
    public string? ActionReason { get; set; }
    public string? SourceSystem { get; set; }
    public int Version { get; set; } = 1;
    public string? Hash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public DumZone Zone { get; set; } = null!;
}
