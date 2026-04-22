using GreenTransit.Domain.Interfaces;

namespace GreenTransit.Domain.Entities;

/// <summary>Entradas en centros de acopio. Tabla: EntryCACs</summary>
public class EntryCAC : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid? OwnerId { get; set; }
    public Guid IdWasteMove { get; set; }
    public string? WasteMoveReference { get; set; }
    public DateTime? CACEntryDate { get; set; }
    public string? TypeContainer { get; set; }
    public decimal? PriceContainer { get; set; }
    public DateTime? DateCreateSys { get; set; }
    public DateTime? DateModifiedSys { get; set; }
    public int IdUser { get; set; }
    public string? CollectionMethod { get; set; }

    public WasteMove WasteMove { get; set; } = null!;
    public ICollection<EntryCACResidue> EntryCACResidues { get; set; } = [];
}
