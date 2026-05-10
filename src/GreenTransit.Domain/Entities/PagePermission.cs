namespace GreenTransit.Domain.Entities;

public class PagePermission
{
    public int ID { get; set; }
    public int IdPageDefinition { get; set; }
    public int IdProfile { get; set; }
    public string AccessLevel { get; set; } = "Read"; // "Read" | "Write" | "ReadWrite"
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? IdUser { get; set; }

    // Navegación
    public PageDefinition PageDefinition { get; set; } = null!;
    public UserProfile Profile { get; set; } = null!;
}
