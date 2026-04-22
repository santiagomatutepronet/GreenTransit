namespace GreenTransit.Domain.Entities;

/// <summary>
/// Líneas de tratamiento con 4 fracciones: entrada, reutilizada, valorizada, rechazo.
/// Tabla: TreatmentPlantResidues
/// </summary>
public class TreatmentPlantResidue
{
    public Guid Id { get; set; }
    public Guid IdTreatmentPlant { get; set; }

    // Residuo de entrada
    public Guid? IdResidue { get; set; }
    public string? Category { get; set; }
    public decimal? WeightTotal { get; set; }
    public string? MeasureUnit { get; set; }
    public int? Units { get; set; }
    public decimal? PriceWeight { get; set; }
    public decimal? PriceUnit { get; set; }

    // Fracción reutilizada
    public Guid? IdResidueReused { get; set; }
    public decimal? WeightReused { get; set; }
    public string? MeasureUnitReused { get; set; }
    public int? UnitsReused { get; set; }

    // Fracción valorizada
    public Guid? IdResidueValued { get; set; }
    public decimal? WeightValued { get; set; }
    public string? MeasureUnitValued { get; set; }
    public int? UnitsValued { get; set; }

    // Fracción rechazo
    public Guid? IdResidueRemove { get; set; }
    public decimal? WeightRemove { get; set; }
    public string? MeasureUnitRemove { get; set; }
    public int? UnitsRemove { get; set; }

    public TreatmentPlant TreatmentPlant { get; set; } = null!;
    public Residue? Residue { get; set; }
    public Residue? ResidueReused { get; set; }
    public Residue? ResidueValued { get; set; }
    public Residue? ResidueRemove { get; set; }
}
