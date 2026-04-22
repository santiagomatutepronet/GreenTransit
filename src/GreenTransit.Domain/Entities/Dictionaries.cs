namespace GreenTransit.Domain.Entities;

public class DicProductDeclarationCategory
{
    public int Id { get; set; }
    public string Ref { get; set; } = null!;
    public string Description { get; set; } = null!;

    public ICollection<DicProductDeclarationProduct> Products { get; set; } = [];
}

public class DicProductDeclarationPeriod
{
    public int Id { get; set; }
    public string Ref { get; set; } = null!;
    public string Description { get; set; } = null!;
}

public class DicProductDeclarationProduct
{
    public int Id { get; set; }
    public string Ref { get; set; } = null!;
    public string Description { get; set; } = null!;
    public int? CategoryId { get; set; }

    public DicProductDeclarationCategory? Category { get; set; }
}

public class DicProductDeclarationSource
{
    public int Id { get; set; }
    public string Ref { get; set; } = null!;
    public string Description { get; set; } = null!;
}

public class DicProductDeclarationType
{
    public int Id { get; set; }
    public string Ref { get; set; } = null!;
    public string Description { get; set; } = null!;
}

public class DicProductDeclarationUse
{
    public int Id { get; set; }
    public string Ref { get; set; } = null!;
    public string Description { get; set; } = null!;
}

public class DocState
{
    public int Id { get; set; }
    public string IdRef { get; set; } = null!;
    public string? Name { get; set; }
}
