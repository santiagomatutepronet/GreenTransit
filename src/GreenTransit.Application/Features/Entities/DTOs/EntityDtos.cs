namespace GreenTransit.Application.Features.Entities.DTOs;

/// <summary>DTO para el listado paginado de entidades.</summary>
public sealed record EntityDto(
    Guid    Id,
    string  Name,
    string? NationalId,
    string? CenterCode,
    string  EntityRole,
    string? ProvinceCode,
    bool    IsActive,
    string? LinkedUserLogin
);

/// <summary>DTO de detalle con todos los campos de la entidad.</summary>
public sealed record EntityDetailDto(
    Guid    Id,
    string  Name,
    string? NationalId,
    string? CenterCode,
    string  EntityRole,
    string? EntityType,
    string? EconomicActivity,
    string? TypeThirdParty,
    string? InscriptionType,
    string? InscriptionNumber,
    string? CountryCode,
    string? StateCode,
    string? ProvinceCode,
    string? MunicipalityCode,
    string? ZipCode,
    string? Address,
    string? Latitude,
    string? Longitude,
    string? PhoneNumber,
    string? Email,
    string? ContactPerson,
    bool    IsActive,
    string? SourceSystem,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int     IdUser,
    int?    LinkedUserId,
    string? LinkedUserLogin
);
