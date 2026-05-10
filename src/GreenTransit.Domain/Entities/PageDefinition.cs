namespace GreenTransit.Domain.Entities;

public class PageDefinition
{
    public int ID { get; set; }
    public string Route { get; set; } = string.Empty;
    public string PageName { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public string? ComponentName { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navegación
    public ICollection<PagePermission> Permissions { get; set; } = new List<PagePermission>();
}
