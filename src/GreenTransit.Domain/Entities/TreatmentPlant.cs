using GreenTransit.Domain.Interfaces;

namespace GreenTransit.Domain.Entities;

/// <summary>Registros de tratamiento en planta. Tabla: TreatmentPlants</summary>
public class TreatmentPlant : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid? OwnerId { get; set; }
    public Guid? IdWasteMove { get; set; }
    public string? WasteMoveReference { get; set; }
    public string? TicketScale { get; set; }
    public DateTime? PlantTreatmentDate { get; set; }
    public string? TypeContainer { get; set; }
    public decimal? PriceContainer { get; set; }
    public DateTime? DateCreateSys { get; set; }
    public DateTime? DateModifiedSys { get; set; }
    public int IdUser { get; set; }
    public Guid? ServiceOrderId { get; set; }
    public Guid? IdTreatmentOperation { get; set; }
    public decimal? ImproperWeight { get; set; }
    public string? QualityMetricsJson { get; set; }
    public Guid? IncidentId { get; set; }
    public string? SourceSystem { get; set; }

    public WasteMove? WasteMove { get; set; }
    public ServiceOrder? ServiceOrder { get; set; }
    public TreatmentOperation? TreatmentOperation { get; set; }
    public Incident? Incident { get; set; }
    public ICollection<TreatmentPlantResidue> TreatmentPlantResidues { get; set; } = [];
}
