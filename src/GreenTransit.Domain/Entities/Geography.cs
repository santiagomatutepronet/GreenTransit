namespace GreenTransit.Domain.Entities;

public class Country
{
    public int Id { get; set; }
    public string Ref { get; set; } = null!;
    public string Code { get; set; } = null!;
    public int? IsoNum { get; set; }
    public string? CodeIso3 { get; set; }
    public bool MunicipalityDataLinkedRequired { get; set; }
    public bool MunicipalityDataRequired { get; set; }
    public bool UE { get; set; }

    public ICollection<TerritoryState> States { get; set; } = [];
}

public class TerritoryState
{
    public int Id { get; set; }
    public int IdCountry { get; set; }
    public string Ref { get; set; } = null!;
    public string Code { get; set; } = null!;
    public string? Name { get; set; }

    public Country Country { get; set; } = null!;
    public ICollection<Province> Provinces { get; set; } = [];
}

public class Province
{
    public int Id { get; set; }
    public int IdState { get; set; }
    public string Ref { get; set; } = null!;
    public string Code { get; set; } = null!;
    public string? Name { get; set; }

    public TerritoryState State { get; set; } = null!;
    public ICollection<Municipality> Municipalities { get; set; } = [];
}

public class Municipality
{
    public int Id { get; set; }
    public int IdProvince { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? CodeControlNumber { get; set; }

    public Province Province { get; set; } = null!;
    public ICollection<MunicipalityPopulation> Populations { get; set; } = [];
    public ICollection<MunicipalityZipCode> ZipCodes { get; set; } = [];
}

public class MunicipalityPopulation
{
    public int Id { get; set; }
    public int IdMunicipality { get; set; }
    public int? TotalPopulation { get; set; }
    public int? MalePopulation { get; set; }
    public int? FemalePopulation { get; set; }

    public Municipality Municipality { get; set; } = null!;
}

public class MunicipalityZipCode
{
    public int Id { get; set; }
    public int IdMunicipality { get; set; }
    public string ZipCode { get; set; } = null!;
    public string? Description { get; set; }

    public Municipality Municipality { get; set; } = null!;
}
