using FluentAssertions;
using FluentValidation;
using GreenTransit.Application.Features.TreatmentPlants.Commands;
using GreenTransit.Domain.Constants;
using GreenTransit.Domain.Entities;
using GreenTransit.Infrastructure.Persistence;
using GreenTransit.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace GreenTransit.Tests.Application.TreatmentPlants;

/// <summary>Tests del CreateTreatmentPlantCommandHandler.</summary>
public sealed class CreateTreatmentPlantCommandHandlerTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static CreateTreatmentPlantCommand BuildCommand(
        Guid    wasteMoveId,
        Guid    operationId,
        CreateTreatmentPlantLineInput[]? lines = null) => new(
        WasteMoveId:         wasteMoveId,
        PlantTreatmentDate:  DateTime.UtcNow,
        IdTreatmentOperation: operationId,
        TicketScale:         "TIC-TEST-001",
        ServiceOrderId:      null,
        ImproperWeight:      null,
        QualityMetricsJson:  null,
        TypeContainer:       null,
        PriceContainer:      null,
        Lines:               lines ?? [BuildLine(Guid.NewGuid(), 100m, 60m, 30m, 10m)]);

    private static CreateTreatmentPlantLineInput BuildLine(
        Guid    idResidue,
        decimal total,
        decimal reused,
        decimal valued,
        decimal remove) => new(
        IdResidue:        idResidue,
        Category:         null,
        WeightTotal:      total,
        MeasureUnit:      "kg",
        Units:            null,
        PriceWeight:      null,
        PriceUnit:        null,
        IdResidueReused:  null,
        WeightReused:     reused,
        MeasureUnitReused: null,
        UnitsReused:      null,
        IdResidueValued:  null,
        WeightValued:     valued,
        MeasureUnitValued: null,
        UnitsValued:      null,
        IdResidueRemove:  null,
        WeightRemove:     remove,
        MeasureUnitRemove: null,
        UnitsRemove:      null);

    private static async Task<(AppDbContext ctx, WasteMove wm, Guid opId)> SetupEnPlantaAsync(
        FakeCurrentUserService user)
    {
        var ctx = TestDbContextFactory.Create(user);
        var opId = Guid.NewGuid();

        var op = new TreatmentOperation
        {
            Id            = opId,
            Code          = "R3",
            OperationType = "Recovery",
            Description   = "Reciclado de materiales orgánicos",
            IsActive      = true,
            CreatedAt     = DateTime.UtcNow,
            UpdatedAt     = DateTime.UtcNow,
        };
        ctx.TreatmentOperations.Add(op);

        var wm = new WasteMove
        {
            Id                 = Guid.NewGuid(),
            OwnerId            = user.OwnerId,
            WasteMoveReference = "WM-TEST-001",
            ServiceStatus      = WasteMoveStatuses.EnPlanta,
            DateCreateSys      = DateTime.UtcNow,
        };
        ctx.WasteMoves.Add(wm);

        // EntryPlant previa (requerida)
        ctx.EntryPlants.Add(new EntryPlant
        {
            Id                 = Guid.NewGuid(),
            IdWasteMove        = wm.Id,
            OwnerId            = user.OwnerId,
            WasteMoveReference = wm.WasteMoveReference,
            TicketScale        = "TIC-PREV-001",
            GrossWeight        = 110m,
            TareWeight         = 10m,
            NetWeight          = 100m,
            DateCreateSys      = DateTime.UtcNow,
            IdUser             = 1,
        });

        await ctx.SaveChangesAsync();
        return (ctx, wm, opId);
    }

    // ── Caso 1: balance correcto → CLASIFICADO, sin Incident ─────────────────

    [Fact]
    public async Task Handle_BalanceOk_ChangesStatusToClasificadoAndNoIncident()
    {
        var user = new FakeCurrentUserService(FakeCurrentUserService.TenantA);
        var (ctx, wm, opId) = await SetupEnPlantaAsync(user);
        var handler = new CreateTreatmentPlantCommandHandler(
            ctx, user, NullLogger<CreateTreatmentPlantCommandHandler>.Instance);

        // Balance: 100 = 60 + 30 + 10 → perfecto
        var result = await handler.Handle(BuildCommand(wm.Id, opId), CancellationToken.None);

        result.HasBalanceErrors.Should().BeFalse();
        result.BalanceErrorMessages.Should().BeEmpty();

        var updated = await ctx.WasteMoves.FindAsync(wm.Id);
        updated!.ServiceStatus.Should().Be(WasteMoveStatuses.Clasificado);

        var incidents = ctx.Incidents
            .Where(i => i.WasteMoveReference == wm.WasteMoveReference
                     && i.Type == "MassBalanceError")
            .ToList();
        incidents.Should().BeEmpty();
    }

    // ── Caso 2: balance fuera de tolerancia → sin cambio de estado, Incident High ──

    [Fact]
    public async Task Handle_BalanceOutOfTolerance_CreatesHighIncidentAndDoesNotChangeStatus()
    {
        var user = new FakeCurrentUserService(FakeCurrentUserService.TenantA);
        var (ctx, wm, opId) = await SetupEnPlantaAsync(user);
        var handler = new CreateTreatmentPlantCommandHandler(
            ctx, user, NullLogger<CreateTreatmentPlantCommandHandler>.Instance);

        // Balance: 100 ≠ 40 + 20 + 10 = 70  → diferencia 30 kg > 1%
        var lines = new[] { BuildLine(Guid.NewGuid(), 100m, 40m, 20m, 10m) };
        var command = BuildCommand(wm.Id, opId, lines);

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*descuadre*");

        // Estado NO cambia
        var updated = await ctx.WasteMoves.FindAsync(wm.Id);
        updated!.ServiceStatus.Should().Be(WasteMoveStatuses.EnPlanta);

        // Incident creado con Severity=High
        var incident = ctx.Incidents
            .FirstOrDefault(i => i.WasteMoveReference == wm.WasteMoveReference
                              && i.Type == "MassBalanceError");
        incident.Should().NotBeNull();
        incident!.Severity.Should().Be("High");
    }

    // ── Caso 3: línea con WeightTotal = 0 → ValidationException ─────────────

    [Fact]
    public async Task Handle_LineWeightTotalZero_ThrowsValidationException()
    {
        var user = new FakeCurrentUserService(FakeCurrentUserService.TenantA);
        var (ctx, wm, opId) = await SetupEnPlantaAsync(user);
        var validator = new CreateTreatmentPlantCommandValidator();

        var lines = new[] { BuildLine(Guid.NewGuid(), 0m, 0m, 0m, 0m) };
        var command = BuildCommand(wm.Id, opId, lines);

        var validationResult = await validator.ValidateAsync(command);

        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should()
            .Contain(e => e.PropertyName.Contains("WeightTotal"));
    }

    // ── Caso 4: WasteMove sin estado EN_PLANTA → InvalidOperationException ───

    [Fact]
    public async Task Handle_WasteMoveNotEnPlanta_ThrowsInvalidOperationException()
    {
        var user = new FakeCurrentUserService(FakeCurrentUserService.TenantA);
        await using var ctx = TestDbContextFactory.Create(user);

        var opId = Guid.NewGuid();
        var wm = new WasteMove
        {
            Id                 = Guid.NewGuid(),
            OwnerId            = user.OwnerId,
            WasteMoveReference = "WM-RECOGIDO-001",
            ServiceStatus      = WasteMoveStatuses.Recogido,
            DateCreateSys      = DateTime.UtcNow,
        };
        ctx.WasteMoves.Add(wm);
        await ctx.SaveChangesAsync();

        var handler = new CreateTreatmentPlantCommandHandler(
            ctx, user, NullLogger<CreateTreatmentPlantCommandHandler>.Instance);

        var act = async () => await handler.Handle(BuildCommand(wm.Id, opId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{WasteMoveStatuses.Recogido}*");
    }

    // ── Caso 5: WasteMove EN_PLANTA pero sin EntryPlant previa ───────────────

    [Fact]
    public async Task Handle_NoEntryPlant_ThrowsInvalidOperationException()
    {
        var user = new FakeCurrentUserService(FakeCurrentUserService.TenantA);
        await using var ctx = TestDbContextFactory.Create(user);

        var opId = Guid.NewGuid();
        var wm = new WasteMove
        {
            Id                 = Guid.NewGuid(),
            OwnerId            = user.OwnerId,
            WasteMoveReference = "WM-NOPLANT-001",
            ServiceStatus      = WasteMoveStatuses.EnPlanta,
            DateCreateSys      = DateTime.UtcNow,
        };
        ctx.WasteMoves.Add(wm);
        await ctx.SaveChangesAsync();

        var handler = new CreateTreatmentPlantCommandHandler(
            ctx, user, NullLogger<CreateTreatmentPlantCommandHandler>.Instance);

        var act = async () => await handler.Handle(BuildCommand(wm.Id, opId), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*entrada en planta*");
    }

    // ── Caso 6: aislamiento multi-tenant ─────────────────────────────────────

    [Fact]
    public async Task Handle_WasteMoveFromDifferentTenant_ThrowsKeyNotFoundException()
    {
        var userA = new FakeCurrentUserService(FakeCurrentUserService.TenantA);
        var userB = new FakeCurrentUserService(FakeCurrentUserService.TenantB);

        // WasteMove pertenece a TenantA
        await using var ctxA = TestDbContextFactory.Create(userA);
        var opId = Guid.NewGuid();
        var wm = new WasteMove
        {
            Id                 = Guid.NewGuid(),
            OwnerId            = userA.OwnerId,
            WasteMoveReference = "WM-TENANTA-001",
            ServiceStatus      = WasteMoveStatuses.EnPlanta,
            DateCreateSys      = DateTime.UtcNow,
        };
        ctxA.WasteMoves.Add(wm);
        await ctxA.SaveChangesAsync();

        // Handler usando contexto de TenantB — el filtro de tenant oculta el WasteMove
        await using var ctxB = TestDbContextFactory.Create(userB);
        var handler = new CreateTreatmentPlantCommandHandler(
            ctxB, userB, NullLogger<CreateTreatmentPlantCommandHandler>.Instance);

        var act = async () => await handler.Handle(BuildCommand(wm.Id, opId), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
