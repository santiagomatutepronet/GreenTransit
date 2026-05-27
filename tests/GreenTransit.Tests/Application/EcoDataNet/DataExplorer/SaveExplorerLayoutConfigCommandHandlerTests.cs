using FluentAssertions;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.EcoDataNet.Commands;
using GreenTransit.Application.Features.EcoDataNet.DTOs.DataExplorer;
using GreenTransit.Tests.Helpers;

namespace GreenTransit.Tests.Application.EcoDataNet.DataExplorer;

/// <summary>
/// Tests del handler SaveExplorerLayoutConfigCommandHandler: upsert de configuración de layout.
/// </summary>
public sealed class SaveExplorerLayoutConfigCommandHandlerTests
{
    private static SaveExplorerLayoutConfigCommand BuildCommand(
        string assetId = "urn:asset:1",
        string provider = "urn:provider:1") => new()
    {
        AssetId              = assetId,
        ProviderParticipantId = provider,
        DatasetName          = "Test Dataset",
        SchemaHash           = "abc123",
        Overrides            =
        [
            new WidgetLayoutOverride { WidgetId = "kpi_value", CustomTitle = "Mi KPI" }
        ]
    };

    // ── Inserción de nuevo registro ───────────────────────────────────────

    [Fact]
    public async Task Handle_NewConfig_CreatesRecord()
    {
        var user    = new FakeCurrentUserService();
        var db      = TestDbContextFactory.Create(user);
        var handler = new SaveExplorerLayoutConfigCommandHandler(db, user);

        var id = await handler.Handle(BuildCommand(), CancellationToken.None);

        id.Should().BeGreaterThan(0);
        ((IApplicationDbContext)db).ExplorerLayoutConfigs.Should().HaveCount(1);
    }

    // ── Upsert: actualiza registro existente ──────────────────────────────

    [Fact]
    public async Task Handle_ExistingConfig_UpdatesInPlace()
    {
        var user    = new FakeCurrentUserService();
        var db      = TestDbContextFactory.Create(user);
        var handler = new SaveExplorerLayoutConfigCommandHandler(db, user);

        var cmd1 = BuildCommand();
        await handler.Handle(cmd1, CancellationToken.None);

        var cmd2 = new SaveExplorerLayoutConfigCommand
        {
            AssetId              = "urn:asset:1",
            ProviderParticipantId = "urn:provider:1",
            DatasetName          = "Updated",
            SchemaHash           = "new_hash",
            Overrides            = []
        };
        await handler.Handle(cmd2, CancellationToken.None);

        var ctx = (IApplicationDbContext)db;
        ctx.ExplorerLayoutConfigs.Should().HaveCount(1);
        var saved = ctx.ExplorerLayoutConfigs.Single();
        saved.SchemaHash.Should().Be("new_hash");
        saved.DatasetName.Should().Be("Updated");
    }

    // ── Aislamiento por tenant ────────────────────────────────────────────

    [Fact]
    public async Task Handle_DifferentTenants_CreateSeparateRecords()
    {
        var userA   = new FakeCurrentUserService(FakeCurrentUserService.TenantA);
        var userB   = new FakeCurrentUserService(FakeCurrentUserService.TenantB);

        // Ambos usan la misma BD InMemory compartida
        var db      = TestDbContextFactory.Create(userA);
        var handlerA = new SaveExplorerLayoutConfigCommandHandler(db, userA);
        var handlerB = new SaveExplorerLayoutConfigCommandHandler(db, userB);

        await handlerA.Handle(BuildCommand(), CancellationToken.None);
        await handlerB.Handle(BuildCommand(), CancellationToken.None);

        ((IApplicationDbContext)db).ExplorerLayoutConfigs.Should().HaveCount(2);
    }

    // ── El JSON serializado contiene los overrides ────────────────────────

    [Fact]
    public async Task Handle_Overrides_SerializedToJson()
    {
        var user    = new FakeCurrentUserService();
        var db      = TestDbContextFactory.Create(user);
        var handler = new SaveExplorerLayoutConfigCommandHandler(db, user);

        await handler.Handle(BuildCommand(), CancellationToken.None);

        var saved = ((IApplicationDbContext)db).ExplorerLayoutConfigs.Single();
        saved.LayoutConfigJson.Should().Contain("kpi_value");
        saved.LayoutConfigJson.Should().Contain("Mi KPI");
    }
}
