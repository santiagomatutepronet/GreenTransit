namespace GreenTransit.Domain.Entities;

/// <summary>Líneas de residuos por entrada en planta. Tabla: EntryPlantResidues</summary>
public class EntryPlantResidue
{
    public Guid Id { get; set; }
    public Guid IdEntryPlant { get; set; }
    public Guid? IdResidue { get; set; }
    public decimal? Weight { get; set; }
    public string? MeasureUnit { get; set; }
    public int? Units { get; set; }
    public decimal? PriceWeight { get; set; }
    public decimal? PriceUnit { get; set; }

    public EntryPlant EntryPlant { get; set; } = null!;
    public Residue? Residue { get; set; }
}
