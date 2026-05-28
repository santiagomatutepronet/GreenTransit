namespace GreenTransit.Infrastructure.ExternalApis.EcoDataNet.Models;

public class EntryCACItem
{
    public Guid     RemoteId           { get; set; }
    public Guid     OwnerId            { get; set; }
    public string?  WasteMoveReference { get; set; }
    public DateTime? CacEntryDate      { get; set; }
    public int?     TypeContainer      { get; set; }
    public decimal? PriceContainer     { get; set; }
    public string?  CollectionMethod   { get; set; }

    public List<EntryCACResidueItem> Residues { get; set; } = [];
}
