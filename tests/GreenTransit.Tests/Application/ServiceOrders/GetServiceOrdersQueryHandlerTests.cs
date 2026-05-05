using FluentAssertions;
using GreenTransit.Application.Features.ServiceOrders.Queries;
using GreenTransit.Domain.Constants;
using GreenTransit.Domain.Entities;
using GreenTransit.Tests.Helpers;

namespace GreenTransit.Tests.Application.ServiceOrders;

/// <summary>Tests de GetServiceOrdersQueryHandler — paginación, filtros y aislamiento multi-tenant.</summary>
public sealed class GetServiceOrdersQueryHandlerTests
{
    // ── Helper ────────────────────────────────────────────────────────────────

    private static ServiceOrder BuildServiceOrder(
        Guid ownerId, string number,
        string status   = ServiceOrderStatuses.Pending,
        string priority = ServiceOrderPriorities.Normal,
        DateTime? plannedPickup = null) => new()
    {
        Id                 = Guid.NewGuid(),
        OwnerId            = ownerId,
        ServiceOrderNumber = number,
        Status             = status,
        Priority           = priority,
        IssuedAt           = DateTime.UtcNow,
        PlannedPickupStart = plannedPickup,
        Version            = 1,
        IdUser             = 1,
        CreatedAt          = DateTime.UtcNow,
        UpdatedAt          = DateTime.UtcNow
    };

    // ── Tests de aislamiento multi-tenant ─────────────────────────────────────

    [Fact]
    public async Task Handle_WhenMultipleTenantsExist_ReturnsOnlyCurrentTenantOrders()
    {
        var user = new FakeCurrentUserService(FakeCurrentUserService.TenantA);
        await using var ctx = TestDbContextFactory.Create(user);

        ctx.ServiceOrders.AddRange(
            BuildServiceOrder(FakeCurrentUserService.TenantA, "SO-A-001"),
            BuildServiceOrder(FakeCurrentUserService.TenantA, "SO-A-002"),
            BuildServiceOrder(FakeCurrentUserService.TenantB, "SO-B-001"));
        await ctx.SaveChangesAsync();

        var result = await new GetServiceOrdersQueryHandler(ctx, user)
            .Handle(new GetServiceOrdersQuery(), CancellationToken.None);

        result.Items.Should().HaveCount(2);
        result.Items.Should().AllSatisfy(so => so.ServiceOrderNumber.Should().StartWith("SO-A-"));
    }

    [Fact]
    public async Task Handle_WhenNoOrders_ReturnsEmptyList()
    {
        var user = new FakeCurrentUserService();
        await using var ctx = TestDbContextFactory.Create(user);

        var result = await new GetServiceOrdersQueryHandler(ctx, user)
            .Handle(new GetServiceOrdersQuery(), CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_TenantBCannotSeeTenantAOrders()
    {
        var userA = new FakeCurrentUserService(FakeCurrentUserService.TenantA);
        await using var ctxA = TestDbContextFactory.Create(userA);
        ctxA.ServiceOrders.Add(BuildServiceOrder(FakeCurrentUserService.TenantA, "SO-A-001"));
        await ctxA.SaveChangesAsync();

        var userB = new FakeCurrentUserService(FakeCurrentUserService.TenantB);
        await using var ctxB = TestDbContextFactory.Create(userB);

        var result = await new GetServiceOrdersQueryHandler(ctxB, userB)
            .Handle(new GetServiceOrdersQuery(), CancellationToken.None);

        result.Items.Should().BeEmpty();
    }

    // ── Tests de filtros ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_FilterByStatus_ReturnsMatchingOrders()
    {
        var user = new FakeCurrentUserService();
        await using var ctx = TestDbContextFactory.Create(user);

        ctx.ServiceOrders.AddRange(
            BuildServiceOrder(FakeCurrentUserService.TenantA, "SO-001", ServiceOrderStatuses.Pending),
            BuildServiceOrder(FakeCurrentUserService.TenantA, "SO-002", ServiceOrderStatuses.Scheduled),
            BuildServiceOrder(FakeCurrentUserService.TenantA, "SO-003", ServiceOrderStatuses.Cancelled));
        await ctx.SaveChangesAsync();

        var result = await new GetServiceOrdersQueryHandler(ctx, user)
            .Handle(new GetServiceOrdersQuery(Status: ServiceOrderStatuses.Pending), CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.Items.Single().ServiceOrderNumber.Should().Be("SO-001");
    }

    [Fact]
    public async Task Handle_FilterBySearchTerm_ReturnsMatchingOrders()
    {
        var user = new FakeCurrentUserService();
        await using var ctx = TestDbContextFactory.Create(user);

        ctx.ServiceOrders.AddRange(
            BuildServiceOrder(FakeCurrentUserService.TenantA, "SO-2024-00001"),
            BuildServiceOrder(FakeCurrentUserService.TenantA, "SO-2024-00002"),
            BuildServiceOrder(FakeCurrentUserService.TenantA, "SO-2025-00001"));
        await ctx.SaveChangesAsync();

        var result = await new GetServiceOrdersQueryHandler(ctx, user)
            .Handle(new GetServiceOrdersQuery(SearchTerm: "2025"), CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.Items.Single().ServiceOrderNumber.Should().Be("SO-2025-00001");
    }

    [Fact]
    public async Task Handle_FilterByPlannedPickupDateRange_ReturnsOrdersInRange()
    {
        var user = new FakeCurrentUserService();
        await using var ctx = TestDbContextFactory.Create(user);
        var today     = DateTime.UtcNow.Date;

        ctx.ServiceOrders.AddRange(
            BuildServiceOrder(FakeCurrentUserService.TenantA, "SO-PAST",   plannedPickup: today.AddDays(-10)),
            BuildServiceOrder(FakeCurrentUserService.TenantA, "SO-TODAY",  plannedPickup: today),
            BuildServiceOrder(FakeCurrentUserService.TenantA, "SO-FUTURE", plannedPickup: today.AddDays(10)));
        await ctx.SaveChangesAsync();

        var result = await new GetServiceOrdersQueryHandler(ctx, user).Handle(
            new GetServiceOrdersQuery(
                PlannedPickupFrom: today.AddDays(-1),
                PlannedPickupTo:   today.AddDays(1)),
            CancellationToken.None);

        result.Items.Should().HaveCount(1);
        result.Items.Single().ServiceOrderNumber.Should().Be("SO-TODAY");
    }

    // ── Tests de paginación ───────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Pagination_ReturnsCorrectPage()
    {
        var user = new FakeCurrentUserService();
        await using var ctx = TestDbContextFactory.Create(user);

        ctx.ServiceOrders.AddRange(
            Enumerable.Range(1, 25).Select(i =>
                BuildServiceOrder(FakeCurrentUserService.TenantA, $"SO-{i:000}")));
        await ctx.SaveChangesAsync();

        var page1 = await new GetServiceOrdersQueryHandler(ctx, user)
            .Handle(new GetServiceOrdersQuery(PageNumber: 1, PageSize: 10), CancellationToken.None);

        var page3 = await new GetServiceOrdersQueryHandler(ctx, user)
            .Handle(new GetServiceOrdersQuery(PageNumber: 3, PageSize: 10), CancellationToken.None);

        page1.Items.Should().HaveCount(10);
        page1.TotalCount.Should().Be(25);
        page1.TotalPages.Should().Be(3);
        page3.Items.Should().HaveCount(5);
    }
}

