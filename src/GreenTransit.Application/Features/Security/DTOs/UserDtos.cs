namespace GreenTransit.Application.Features.Security.DTOs;

/// <summary>DTO para listado paginado de usuarios.</summary>
public sealed record UserDto(
    int     Id,
    string  Login,
    string? Email,
    string? CompleteName,
    int     IdProfile,
    string  ProfileReference,
    bool    IsActive,
    Guid?   OwnerId
);

/// <summary>DTO de detalle de usuario con geografía resuelta y entidad vinculada.</summary>
public sealed record UserDetailDto(
    int     Id,
    string  Login,
    string? Email,
    string? CompleteName,
    int     IdProfile,
    string  ProfileReference,
    bool    IsActive,
    Guid?   OwnerId,
    int?    NationalId,
    string? CountryName,
    int?    GeographicalId,
    string? StateName,
    int?    MunicipalityId,
    string? MunicipalityName,
    string? ZipCode,
    string? Address,
    string? PortalEDCProvider,
    string? PortalEDCConsumer,
    DateTime? CreateDate,
    string? LinkedEntityName,
    Guid?   LinkedEntityId
);

/// <summary>DTO ligero para selector de perfiles.</summary>
public sealed record ProfileDto(int Id, string Reference, string? Description);
