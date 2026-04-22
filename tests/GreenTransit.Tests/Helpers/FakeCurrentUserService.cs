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

    public FakeCurrentUserService(Guid? ownerId = null)
    {
        OwnerId = ownerId ?? TenantA;
    }

    public bool IsAuthenticated => true;
    public int IdUser => 1;
    public Guid OwnerId { get; }
    public string Email => "test@greentransit.dev";
}
