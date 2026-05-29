namespace GreenTransit.Infrastructure.ExternalApis.EcoDataNet.Models;

public class ProductDeclarationItem
{
    public Guid      RemoteId   { get; set; }
    public Guid      OwnerId    { get; set; }
    public string?   Period     { get; set; }
    public int?      Year       { get; set; }
    public int?      Month      { get; set; }
    public string?   Reference  { get; set; }
    public string?   Currency   { get; set; }
    public string?   State      { get; set; }
    public DateTime? DateCreate { get; set; }
    public DateTime? DateEmit   { get; set; }
    public decimal?  Amount     { get; set; }
    public string?   Type       { get; set; }

    public ProducerRef?      Producer { get; set; }
    public List<ProductItem> Products { get; set; } = [];
}
