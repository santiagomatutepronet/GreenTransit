using System.ComponentModel.DataAnnotations.Schema;
using GreenTransit.Domain.Interfaces;

namespace GreenTransit.Domain.Entities;

/// <summary>Catálogo de códigos LER (Lista Europea de Residuos). Tabla: LERCodes</summary>
[Table("LERCodes")]
public class LerCode : IAuditableEntity
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string? CodeExtended { get; set; }
    public string Description { get; set; } = null!;
    public string? Chapter { get; set; }
    public string? ChapterDescription { get; set; }
    public string? SubChapter { get; set; }
    public string? SubChapterDescription { get; set; }
    public bool IsDangerous { get; set; }
    public bool IsRAEE { get; set; }
    public string? DefaultProductCategory { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navegaciones inversas
    public ICollection<Residue> Residues { get; set; } = [];
    public ICollection<ServiceOrder> ServiceOrders { get; set; } = [];
    public ICollection<SettlementLine> SettlementLines { get; set; } = [];
}
