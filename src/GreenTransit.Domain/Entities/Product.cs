namespace GreenTransit.Domain.Entities;

/// <summary>Líneas de declaración de producto puesto en mercado. Tabla: Products</summary>
public class Product
{
    public Guid Id { get; set; }
    public Guid IdProductDeclaration { get; set; }
    public Guid? IdResidue { get; set; }
    public string? Reference { get; set; }
    public string? Source { get; set; }
    public decimal? Quantity { get; set; }
    public string? MeasureUnit { get; set; }
    public int? Units { get; set; }
    public decimal? Price { get; set; }

    public ProductDeclaration ProductDeclaration { get; set; } = null!;
    public Residue? Residue { get; set; }
}
