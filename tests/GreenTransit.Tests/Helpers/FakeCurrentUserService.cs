using System.Security.Claims;
using GreenTransit.Application.Common.Interfaces;

namespace GreenTransit.Tests.Helpers;

/// <summary>
/// Implementación fake de ICurrentUserService para tests.
/// Simula un usuario autenticado con un OwnerId fijo y conocido.
/// </summary>
public sealed class FakeCurrentUserService : ICurrentUserService
{
    /// <summary>OwnerId fijo del tenant de prueba (Tenant A).</summary>
    public static readonly Guid TenantA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    /// <summary>OwnerId de un segundo tenant para tests de aislamiento.</summary>
    public static readonly Guid TenantB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    public FakeCurrentUserService(Guid? ownerId = null, string userProfile = "ADMIN")
    {
        OwnerId     = ownerId ?? TenantA;
        UserProfile = userProfile;
    }

    public bool IsAuthenticated => true;
    public int IdUser           => 1;
    public string Login         => "test@greentransit.dev";
    public string Email         => "test@greentransit.dev";
    public string UserName      => "Test User";
    public Guid OwnerId         { get; }
    public int ProfileId        => 1;
    public string UserProfile   { get; }
    public Guid? LinkedEntityId => null;

    public void SetInteractiveUser(ClaimsPrincipal user) { /* no-op en tests */ }

    public bool IsInProfile(string profileRef) =>
        string.Equals(UserProfile, profileRef, StringComparison.OrdinalIgnoreCase);

    public bool IsInAnyProfile(params string[] profileRefs) =>
        profileRefs.Any(p => string.Equals(UserProfile, p, StringComparison.OrdinalIgnoreCase));
}
