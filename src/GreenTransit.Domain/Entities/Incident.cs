using GreenTransit.Domain.Interfaces;

namespace GreenTransit.Domain.Entities;

/// <summary>Incidencias operativas. Tabla: Incidents</summary>
public class Incident : IAuditableEntity, ITenantEntity
{
    public Guid Id { get; set; }
    public Guid? OwnerId { get; set; }
    public string Type { get; set; } = null!;
    public string Severity { get; set; } = null!;
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public Guid? ServiceOrderId { get; set; }
    public string? WasteMoveReference { get; set; }
    public string? TicketScale { get; set; }
    public string? ReportedByName { get; set; }
    public string? ReportedByNationalId { get; set; }
    public string? ReportedByCenterCode { get; set; }
    public string? Description { get; set; }
    public string? ResolutionJson { get; set; }
    public string? SourceSystem { get; set; }
    public int Version { get; set; } = 1;
    public string? Hash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int IdUser { get; set; }

    public ServiceOrder? ServiceOrder { get; set; }
}
