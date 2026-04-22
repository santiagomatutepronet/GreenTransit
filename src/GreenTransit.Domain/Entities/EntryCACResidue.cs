namespace GreenTransit.Domain.Entities;

/// <summary>Líneas de residuos por entrada en CAC. Tabla: EntryCACResidues</summary>
public class EntryCACResidue
{
    public Guid Id { get; set; }
    public Guid IdEntryCAC { get; set; }
    public Guid? IdResidue { get; set; }
    public decimal? Weight { get; set; }
    public string? MeasureUnit { get; set; }
    public int? Units { get; set; }
    public decimal? PriceWeight { get; set; }
    public decimal? PriceUnit { get; set; }

    public EntryCAC EntryCAC { get; set; } = null!;
    public Residue? Residue { get; set; }
}
