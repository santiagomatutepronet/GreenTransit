using FluentAssertions;
using FluentValidation;
using GreenTransit.Application.Features.ServiceOrders.Commands;
using GreenTransit.Domain.Constants;
using GreenTransit.Domain.Entities;
using GreenTransit.Tests.Helpers;

namespace GreenTransit.Tests.Application.ServiceOrders;

/// <summary>Tests del CreateServiceOrderCommandHandler y DuplicateServiceOrderCommandHandler.</summary>
public sealed class CreateServiceOrderCommandHandlerTests
{
    // ── Helper ────────────────────────────────────────────────────────────────

    private static CreateServiceOrderCommand BuildCommand(
        string number   = "",
        string status   = ServiceOrderStatuses.Pending,
        string priority = ServiceOrderPriorities.Normal,
        Guid?  pickupId = null) => new(
        ServiceOrderNumber:   number,
        IssuedAt:             DateTime.UtcNow,
        IdIssuedBy:           null,
        IssuedByName:         null,
        IssuedByNationalId:   null,
        IssuedByCenterCode:   null,
        Status:               status,
        Priority:             priority,
        WasteStream:          null,
        SubStream:            null,
        ProductUse:           null,
        ProductCategory:      null,
        IdLERCode:             null,
        IdPickupPoint:        pickupId,
        PlannedPickupStart:   null,
        PlannedPickupEnd:     null,
        PlannedDeliveryStart: null,
        PlannedDeliveryEnd:   null,
        EstimatedWeight:      null,
        MeasureUnit:          null,
        Units:                null,
        ContainersJson:       null,
        IdCarrier:            null,
        IdPlannedPlant:       null);

    // ── Creación exitosa ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_CreateWithAutoNumber_GeneratesSONumber()
    {
        var user = new FakeCurrentUserService(FakeCurrentUserService.TenantA);
        await using var ctx = TestDbContextFactory.Create(user);
        var handler = new CreateServiceOrderCommandHandler(ctx, user);

        var id = await handler.Handle(BuildCommand(), CancellationToken.None);

        var so = await ctx.ServiceOrders.FindAsync(id);
        so.Should().NotBeNull();
        so!.ServiceOrderNumber.Should().MatchRegex(@"^SO-\d{4}-\d{5}$");
        so.OwnerId.Should().Be(FakeCurrentUserService.TenantA);
        so.Version.Should().Be(1);
        so.Hash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_CreateWithManualNumber_UsesProvidedNumber()
    {
        var user = new FakeCurrentUserService(FakeCurrentUserService.TenantA);
        await using var ctx = TestDbContextFactory.Create(user);
        var handler = new CreateServiceOrderCommandHandler(ctx, user);

        var id = await handler.Handle(BuildCommand("SO-MANUAL-001"), CancellationToken.None);

        var so = await ctx.ServiceOrders.FindAsync(id);
        so!.ServiceOrderNumber.Should().Be("SO-MANUAL-001");
    }

    [Fact]
    public async Task Handle_AutoNumber_SequenceIncrements()
    {
        var user = new FakeCurrentUserService(FakeCurrentUserService.TenantA);
        await using var ctx = TestDbContextFactory.Create(user);
        var handler = new CreateServiceOrderCommandHandler(ctx, user);

        var id1 = await handler.Handle(BuildCommand(), CancellationToken.None);
        var id2 = await handler.Handle(BuildCommand(), CancellationToken.None);

        var so1 = await ctx.ServiceOrders.FindAsync(id1);
        var so2 = await ctx.ServiceOrders.FindAsync(id2);

        so1!.ServiceOrderNumber.Should().NotBe(so2!.ServiceOrderNumber);
        so2.ServiceOrderNumber.Should().EndWith("00002");
    }

    // ── Validación ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Validator_InvalidStatus_FailsValidation()
    {
        var user = new FakeCurrentUserService(FakeCurrentUserService.TenantA);
        await using var ctx = TestDbContextFactory.Create(user);
        var validator = new CreateServiceOrderCommandValidator(ctx, user);

        var result = await validator.ValidateAsync(BuildCommand(status: "INVALID_STATUS"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Status");
    }

    [Fact]
    public async Task Validator_PickupPointFromDifferentTenant_FailsValidation()
    {
        // Creamos una entidad en TenantB
        await using var ctxB = TestDbContextFactory.Create(new FakeCurrentUserService(FakeCurrentUserService.TenantB));
        var entity = new BusinessEntity
        {
            Id         = Guid.NewGuid(),
            Name       = "Entidad TenantB",
            EntityRole = EntityRoles.Producer,
            IsActive   = true,
            CreatedAt  = DateTime.UtcNow,
            UpdatedAt  = DateTime.UtcNow
        };
        ctxB.BusinessEntities.Add(entity);
        await ctxB.SaveChangesAsync();

        // TenantA intenta usar la entidad de TenantB como pickup point
        var userA = new FakeCurrentUserService(FakeCurrentUserService.TenantA);
        await using var ctxA = TestDbContextFactory.Create(userA);
        var validator = new CreateServiceOrderCommandValidator(ctxA, userA);

        // BusinessEntity no existe en el contexto de TenantA (BD separada in-memory)
        var result = await validator.ValidateAsync(
            BuildCommand(pickupId: entity.Id));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("PickupPoint"));
    }

    [Fact]
    public async Task Validator_PlannedPickupEndBeforeStart_FailsValidation()
    {
        var user = new FakeCurrentUserService(FakeCurrentUserService.TenantA);
        await using var ctx = TestDbContextFactory.Create(user);
        var validator = new CreateServiceOrderCommandValidator(ctx, user);

        var cmd = BuildCommand() with
        {
            PlannedPickupStart = DateTime.UtcNow.AddDays(2),
            PlannedPickupEnd   = DateTime.UtcNow.AddDays(1)   // fin < inicio
        };

        var result = await validator.ValidateAsync(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("PickupEnd"));
    }

    // ── Duplicación ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Duplicate_CreatesNewSOWithPendingStatusAndNoPickupDates()
    {
        var user = new FakeCurrentUserService(FakeCurrentUserService.TenantA);
        await using var ctx = TestDbContextFactory.Create(user);

        // Crear la original
        var createHandler = new CreateServiceOrderCommandHandler(ctx, user);
        var sourceId = await createHandler.Handle(
            BuildCommand("SO-ORIG-001") with { PlannedPickupStart = DateTime.UtcNow },
            CancellationToken.None);

        var dupHandler = new DuplicateServiceOrderCommandHandler(ctx, user);
        var dupId = await dupHandler.Handle(
            new DuplicateServiceOrderCommand(sourceId), CancellationToken.None);

        var dup = await ctx.ServiceOrders.FindAsync(dupId);
        dup.Should().NotBeNull();
        dup!.Id.Should().NotBe(sourceId);
        dup.ServiceOrderNumber.Should().NotBe("SO-ORIG-001");
        dup.Status.Should().Be(ServiceOrderStatuses.Pending);
        dup.PlannedPickupStart.Should().BeNull();
        dup.PlannedPickupEnd.Should().BeNull();
        dup.Version.Should().Be(1);
    }
}
