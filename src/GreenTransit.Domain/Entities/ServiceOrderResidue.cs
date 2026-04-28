namespace GreenTransit.Domain.Entities;

/// <summary>Línea de residuo de una Orden de Servicio. Tabla: ServiceOrderResidues</summary>
public class ServiceOrderResidue
{
    public Guid     Id             { get; set; }
    public Guid     IdServiceOrder { get; set; }
    public int      SortOrder      { get; set; }
    public Guid?    IdLERCode      { get; set; }
    public int?     ProductUse     { get; set; }
    public int?     ProductCategory { get; set; }
    public decimal? EstimatedWeight { get; set; }
    public int?     MeasureUnit    { get; set; }
    public int?     Units          { get; set; }

    // ── Navegación ────────────────────────────────────────────────────────────
    public ServiceOrder? ServiceOrder { get; set; }
    public LerCode?      LerCode      { get; set; }
}
