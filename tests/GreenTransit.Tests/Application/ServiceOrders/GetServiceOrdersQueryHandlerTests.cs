using FluentAssertions;
using GreenTransit.Application.Features.ServiceOrders.Queries;
using GreenTransit.Domain.Entities;
using GreenTransit.Tests.Helpers;

namespace GreenTransit.Tests.Application.ServiceOrders;

/// <summary>
/// Tests del GetServiceOrdersQueryHandler.
/// Verifican el comportamiento del filtro multi-tenant a través del
/// HasQueryFilter configurado en AppDbContext.
/// </summary>
public sealed class GetServiceOrdersQueryHandlerTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ServiceOrder BuildServiceOrder(Guid ownerId, string number) => new()
    {
        Id                  = Guid.NewGuid(),
        OwnerId             = ownerId,
        ServiceOrderNumber  = number,
        Status              = "Pending",
        Priority            = "Normal",
        IssuedAt            = DateTime.UtcNow,
        IdUser              = 1,
        Version             = 1,
        CreatedAt           = DateTime.UtcNow,
        UpdatedAt           = DateTime.UtcNow
    };

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenMultipleTenantsExist_ReturnsOnlyCurrentTenantOrders()
    {
        // Arrange
        var userService = new FakeCurrentUserService(FakeCurrentUserService.TenantA);
        await using var ctx = TestDbContextFactory.Create(userService);

        // Sembramos órdenes de dos tenants distintos
        ctx.ServiceOrders.AddRange(
            BuildServiceOrder(FakeCurrentUserService.TenantA, "SO-A-001"),
            BuildServiceOrder(FakeCurrentUserService.TenantA, "SO-A-002"),
            BuildServiceOrder(FakeCurrentUserService.TenantB, "SO-B-001")  // otro tenant
        );
        await ctx.SaveChangesAsync();

        var handler = new GetServiceOrdersQueryHandler(ctx);

        // Act
        var result = await handler.Handle(new GetServiceOrdersQuery(), CancellationToken.None);

        // Assert — solo las del TenantA deben aparecer
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(so =>
            so.OwnerId.Should().Be(FakeCurrentUserService.TenantA));
        result.Should().NotContain(so => so.ServiceOrderNumber == "SO-B-001");
    }

    [Fact]
    public async Task Handle_WhenNoOrders_ReturnsEmptyList()
    {
        // Arrange
        await using var ctx = TestDbContextFactory.CreateDefault();
        var handler = new GetServiceOrdersQueryHandler(ctx);

        // Act
        var result = await handler.Handle(new GetServiceOrdersQuery(), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenTenantHasOrders_OtherTenantSeesNothing()
    {
        // Arrange — TenantB ve sus propias órdenes, no las de TenantA
        var tenantAUser = new FakeCurrentUserService(FakeCurrentUserService.TenantA);
        var tenantBUser = new FakeCurrentUserService(FakeCurrentUserService.TenantB);

        // Usamos la misma BD para probar aislamiento entre tenants
        var dbName = Guid.NewGuid().ToString();

        await using var ctxA = TestDbContextFactory.Create(tenantAUser);
        ctxA.ServiceOrders.Add(BuildServiceOrder(FakeCurrentUserService.TenantA, "SO-A-001"));
        await ctxA.SaveChangesAsync();

        // TenantB usa su propio contexto (filtro distinto)
        await using var ctxB = TestDbContextFactory.Create(tenantBUser);
        var handlerB = new GetServiceOrdersQueryHandler(ctxB);

        // Act
        var result = await handlerB.Handle(new GetServiceOrdersQuery(), CancellationToken.None);

        // Assert — TenantB no puede ver las órdenes de TenantA
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsOrdersSortedByIssuedAtDescending()
    {
        // Arrange
        var userService = new FakeCurrentUserService(FakeCurrentUserService.TenantA);
        await using var ctx = TestDbContextFactory.Create(userService);

        var earlier = BuildServiceOrder(FakeCurrentUserService.TenantA, "SO-EARLY");
        var later   = BuildServiceOrder(FakeCurrentUserService.TenantA, "SO-LATE");

        earlier.IssuedAt = DateTime.UtcNow.AddDays(-1);
        later.IssuedAt   = DateTime.UtcNow;

        ctx.ServiceOrders.AddRange(earlier, later);
        await ctx.SaveChangesAsync();

        var handler = new GetServiceOrdersQueryHandler(ctx);

        // Act
        var result = await handler.Handle(new GetServiceOrdersQuery(), CancellationToken.None);

        // Assert — la más reciente debe ir primera
        result.Should().HaveCount(2);
        result[0].ServiceOrderNumber.Should().Be("SO-LATE");
        result[1].ServiceOrderNumber.Should().Be("SO-EARLY");
    }
}
