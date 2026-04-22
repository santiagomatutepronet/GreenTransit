using GreenTransit.Domain.Interfaces;

namespace GreenTransit.Domain.Entities;

/// <summary>Declaraciones periódicas de producto puesto en mercado. Tabla: ProductDeclaration</summary>
public class ProductDeclaration : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid? OwnerId { get; set; }
    public int? Period { get; set; }
    public int? Year { get; set; }
    public int? Month { get; set; }
    public string? Currency { get; set; }
    public string? State { get; set; }
    public DateTime? DateCreate { get; set; }
    public DateTime? DateEmit { get; set; }
    public string? Reference { get; set; }
    public Guid? IdProducer { get; set; }
    public decimal? Amount { get; set; }
    public string? Type { get; set; }
    public DateTime? DateCreateSys { get; set; }
    public DateTime? DateModifiedSys { get; set; }
    public int IdUser { get; set; }

    public BusinessEntity? Producer { get; set; }
    public ICollection<Product> Products { get; set; } = [];
}
