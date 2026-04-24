using FluentAssertions;
using GreenTransit.Domain.Entities;
using GreenTransit.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Tests.Infrastructure;

/// <summary>
/// Verifica que el HasQueryFilter global de AppDbContext aísla correctamente
/// los datos por OwnerId en entidades que implementan ITenantEntity.
/// </summary>
public sealed class TenantQueryFilterTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Agreement BuildAgreement(Guid ownerId, string number) => new()
    {
        Id              = Guid.NewGuid(),
        OwnerId         = ownerId,
        AgreementNumber = number,
        Status          = "Draft",
        IdScrap         = Guid.NewGuid(),
        IdPublicEntity  = Guid.NewGuid(),
        CreatedAt       = DateTime.UtcNow,
        UpdatedAt       = DateTime.UtcNow,
        IdUser          = 1,
        Version         = 1
    };

    private static WasteMove BuildWasteMove(Guid ownerId, string reference) => new()
    {
        Id                = Guid.NewGuid(),
        OwnerId           = ownerId,
        WasteMoveReference = reference,
        ServiceStatus     = "SOLICITADO",
        RequestDate       = DateTime.UtcNow,
        IdUser            = 1
    };

    // ── Tests: Agreements ─────────────────────────────────────────────────────

    [Fact]
    public async Task Agreements_QueryFilter_ReturnsOnlyCurrentTenantRecords()
    {
        // Arrange
        var userA = new FakeCurrentUserService(FakeCurrentUserService.TenantA);
        await using var ctx = TestDbContextFactory.Create(userA);

        ctx.Agreements.AddRange(
            BuildAgreement(FakeCurrentUserService.TenantA, "AGR-A-001"),
            BuildAgreement(FakeCurrentUserService.TenantA, "AGR-A-002"),
            BuildAgreement(FakeCurrentUserService.TenantB, "AGR-B-001")
        );
        await ctx.SaveChangesAsync();

        // Act
        var result = await ctx.Agreements.ToListAsync();

        // Assert — solo los del TenantA
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(a => a.OwnerId.Should().Be(FakeCurrentUserService.TenantA));
        result.Should().NotContain(a => a.AgreementNumber == "AGR-B-001");
    }

    [Fact]
    public async Task WasteMoves_QueryFilter_ReturnsOnlyCurrentTenantRecords()
    {
        // Arrange
        var userB = new FakeCurrentUserService(FakeCurrentUserService.TenantB);
        await using var ctx = TestDbContextFactory.Create(userB);

        ctx.WasteMoves.AddRange(
            BuildWasteMove(FakeCurrentUserService.TenantA, "WM-A-001"),
            BuildWasteMove(FakeCurrentUserService.TenantB, "WM-B-001"),
            BuildWasteMove(FakeCurrentUserService.TenantB, "WM-B-002")
        );
        await ctx.SaveChangesAsync();

        // Act
        var result = await ctx.WasteMoves.ToListAsync();

        // Assert — solo los del TenantB
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(wm => wm.OwnerId.Should().Be(FakeCurrentUserService.TenantB));
        result.Should().NotContain(wm => wm.WasteMoveReference == "WM-A-001");
    }

    // ── Tests: IgnoreTenantFilter ─────────────────────────────────────────────

    [Fact]
    public async Task IgnoreTenantFilter_AllowsQueryingAllTenants()
    {
        // Arrange — usuario del TenantA, pero con bypass activo
        var userA = new FakeCurrentUserService(FakeCurrentUserService.TenantA);
        await using var ctx = TestDbContextFactory.Create(userA);

        ctx.Agreements.AddRange(
            BuildAgreement(FakeCurrentUserService.TenantA, "AGR-A-001"),
            BuildAgreement(FakeCurrentUserService.TenantB, "AGR-B-001")
        );
        await ctx.SaveChangesAsync();

        // Act
        ctx.IgnoreTenantFilter();
        var result = await ctx.Agreements.ToListAsync();
        ctx.RestoreTenantFilter();

        // Assert — debe ver los dos tenants
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task RestoreTenantFilter_AfterIgnore_ReappliesFilter()
    {
        // Arrange
        var userA = new FakeCurrentUserService(FakeCurrentUserService.TenantA);
        await using var ctx = TestDbContextFactory.Create(userA);

        ctx.Agreements.AddRange(
            BuildAgreement(FakeCurrentUserService.TenantA, "AGR-A-001"),
            BuildAgreement(FakeCurrentUserService.TenantB, "AGR-B-001")
        );
        await ctx.SaveChangesAsync();

        // Act — bypass y luego restaurar
        ctx.IgnoreTenantFilter();
        var allResults = await ctx.Agreements.ToListAsync();
        ctx.RestoreTenantFilter();
        var filteredResults = await ctx.Agreements.ToListAsync();

        // Assert
        allResults.Should().HaveCount(2);
        filteredResults.Should().HaveCount(1);
        filteredResults.Single().OwnerId.Should().Be(FakeCurrentUserService.TenantA);
    }
}
