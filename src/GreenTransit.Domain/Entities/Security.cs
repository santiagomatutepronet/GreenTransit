using System.ComponentModel.DataAnnotations.Schema;
using GreenTransit.Domain.Interfaces;

namespace GreenTransit.Domain.Entities;

/// <summary>Perfiles de autorización. Tabla: Profiles</summary>
[Table("Profiles")]
public class UserProfile
{
    public int Id { get; set; }
    public string Reference { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime? CreateDate { get; set; }

    // Navegación inversa
    public ICollection<AppUser> Users { get; set; } = [];
}

/// <summary>Usuarios del sistema. Tabla: Users</summary>
[Table("Users")]
public class AppUser : ITenantEntity
{
    public int Id { get; set; }
    public string Login { get; set; } = null!;
    public string? CompleteName { get; set; }
    public string? Email { get; set; }
    public DateTime? CreateDate { get; set; }
    public int IdProfile { get; set; }
    /// <summary>FK → Country.Id</summary>
    public int? NationalId { get; set; }
    /// <summary>FK → TerritoryState.Id</summary>
    public int? GeographicalId { get; set; }
    public string? ZipCode { get; set; }
    /// <summary>FK → Municipality.Id</summary>
    public int? MunicipalityId { get; set; }
    public string? Address { get; set; }
    public Guid? OwnerId { get; set; }
    public string? PortalEDCProvider { get; set; }
    public string? PortalEDCConsumer { get; set; }

    // FK salientes
    public UserProfile Profile { get; set; } = null!;
    public Country? Country { get; set; }
    public TerritoryState? TerritoryState { get; set; }
    public Municipality? Municipality { get; set; }
    // Relación 1:1 (UQ_UserSharePointCredentials_UserID)
    public UserSharePointCredential? UserSharePointCredentials { get; set; }
}

/// <summary>Credenciales SharePoint por usuario. Tabla: UserSharePointCredentials</summary>
[Table("UserSharePointCredentials")]
public class UserSharePointCredential
{
    public int Id { get; set; }
    /// <summary>FK → Users.Id (único, relación 1:1)</summary>
    public int UserId { get; set; }
    public string TenantId { get; set; } = null!;
    public string ClientId { get; set; } = null!;
    public string ClientSecret { get; set; } = null!;
    public bool IsActive { get; set; } = true;

    // FK saliente
    public AppUser User { get; set; } = null!;
}
