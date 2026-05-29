namespace GreenTransit.Infrastructure.ExternalApis.EcoDataNet.Models;

public class TreatmentPlantItem
{
    public Guid     RemoteId              { get; set; }
    public Guid     OwnerId               { get; set; }
    public string?  WasteMoveReference    { get; set; }
    public string?  TicketScale           { get; set; }
    public DateTime? PlantTreatmentDate   { get; set; }
    public int?     TypeContainer         { get; set; }
    public decimal? PriceContainer        { get; set; }

    public List<TreatmentPlantResidueItem> Residues { get; set; } = [];
}
