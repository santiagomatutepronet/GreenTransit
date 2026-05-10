namespace GreenTransit.Application.Features.Security.DTOs;

public record PageDefinitionDto
{
    public int Id { get; init; }
    public string Route { get; init; } = string.Empty;
    public string PageName { get; init; } = string.Empty;
    public string ModuleName { get; init; } = string.Empty;
    public string? ComponentName { get; init; }
    public bool IsActive { get; init; }
    public int SortOrder { get; init; }
    public List<PagePermissionDto> Permissions { get; init; } = [];
}

public record PagePermissionDto
{
    public int Id { get; init; }
    public int IdPageDefinition { get; init; }
    public int IdProfile { get; init; }
    public string ProfileReference { get; init; } = string.Empty;
    public string ProfileDescription { get; init; } = string.Empty;
    public string AccessLevel { get; init; } = "Read";
}

public record PagePermissionMatrixDto
{
    public List<ProfileSummaryDto> Profiles { get; init; } = [];
    public List<ModuleGroupDto> Modules { get; init; } = [];
}

public record ProfileSummaryDto
{
    public int Id { get; init; }
    public string Reference { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

public record ModuleGroupDto
{
    public string ModuleName { get; init; } = string.Empty;
    public List<PageWithPermissionsDto> Pages { get; init; } = [];
}

public record PageWithPermissionsDto
{
    public int Id { get; init; }
    public string Route { get; init; } = string.Empty;
    public string PageName { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public int SortOrder { get; init; }
    /// <summary>Diccionario: IdProfile → AccessLevel ("Read" | "Write" | "ReadWrite" | null si sin acceso)</summary>
    public Dictionary<int, string?> PermissionsByProfile { get; init; } = [];
}
