namespace GreenTransit.Application.Features.Geography.DTOs;

/// <summary>País para selector.</summary>
public sealed record CountryDto(int Id, string Name, string IsoCode);

/// <summary>Comunidad autónoma / estado para selector.</summary>
public sealed record StateDto(int Id, string Name, string Code);

/// <summary>Provincia para selector.</summary>
public sealed record ProvinceDto(int Id, string Name, string Code);

/// <summary>Municipio para selector.</summary>
public sealed record MunicipalityDto(int Id, string Name, string Code);
