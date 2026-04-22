using GreenTransit.Domain.Interfaces;

namespace GreenTransit.Domain.Entities;

/// <summary>Entradas de residuos en planta (pesaje). Tabla: EntryPlants</summary>
public class EntryPlant : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid? OwnerId { get; set; }
    public Guid IdWasteMove { get; set; }
    public string? WasteMoveReference { get; set; }
    public string? TicketScale { get; set; }
    public DateTime? PlantEntryDate { get; set; }
    public string? TypeContainer { get; set; }
    public decimal? PriceContainer { get; set; }
    public DateTime? DateCreateSys { get; set; }
    public DateTime? DateModifiedSys { get; set; }
    public int IdUser { get; set; }
    public decimal? GrossWeight { get; set; }
    public decimal? TareWeight { get; set; }
    public decimal? NetWeight { get; set; }
    public string? WeighbridgeId { get; set; }
    public Guid? ServiceOrderId { get; set; }

    public WasteMove WasteMove { get; set; } = null!;
    public ServiceOrder? ServiceOrder { get; set; }
    public ICollection<EntryPlantResidue> EntryPlantResidues { get; set; } = [];
}
