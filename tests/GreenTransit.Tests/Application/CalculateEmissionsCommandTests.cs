using GreenTransit.Application.Features.Emissions.Commands;
using GreenTransit.Domain.Entities;
using GreenTransit.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GreenTransit.Tests.Application;

/// <summary>
/// Tests unitarios para CalculateEmissionsCommandHandler.
/// Cubre: factor encontrado, factor no encontrado y set activo inexistente.
/// </summary>
public sealed class CalculateEmissionsCommandTests
{
    // ── Helpers de datos ──────────────────────────────────────────────────────

    private static EmissionFactorSet MakeActiveSet(string version = "v2024") => new()
    {
        Id            = Guid.NewGuid(),
        OwnerId       = FakeCurrentUserService.TenantA,
        FactorSetName = "Test Set",
        Version       = version,
        Status        = "Active",
        ValidFrom     = DateTime.UtcNow.AddDays(-1),
        ValidTo       = null,
        CreatedAt     = DateTime.UtcNow,
        UpdatedAt     = DateTime.UtcNow,
        IdUser        = 1
    };

    private static EmissionFactor MakeFactor(Guid setId,
        string vehicleType = "Camion",
        string fuelType    = "Diesel",
        string euroClass   = "Euro6",
        decimal value      = 0.1m) => new()
    {
        Id          = Guid.NewGuid(),
        FactorSetId = setId,
        VehicleType = vehicleType,
        FuelType    = fuelType,
        EuroClass   = euroClass,
        Unit        = "kgCO2e/km",
        Value       = value,
        CreatedAt   = DateTime.UtcNow
    };

    private static WasteMove MakeWasteMove(Guid ownerId) => new()
    {
        Id            = Guid.NewGuid(),
        OwnerId       = ownerId,
        ServiceStatus = "RECOGIDO",
        DateCreateSys = DateTime.UtcNow,
        IdUser        = 1,
        Version       = 1
    };

    private static WasteMoveResidue MakeLine(
        Guid wasteMoveId,
        decimal distanceKm,
        string vehicleType = "Camion",
        string fuelType    = "Diesel",
        string euroClass   = "Euro6") => new()
    {
        Id                            = Guid.NewGuid(),
        IdWasteMove                   = wasteMoveId,
        TransportInfo_TransportDistance = distanceKm,
        VehicleType                   = vehicleType,
        FuelType                      = fuelType,
        EuroClass                     = euroClass
    };

    // ── Tests ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// CASO 1: Factor encontrado → emisiones calculadas y persistidas correctamente.
    /// </summary>
    [Fact]
    public async Task Handle_FactorFound_CalculatesAndPersistsEmissions()
    {
        // Arrange
        using var db = TestDbContextFactory.CreateDefault();

        var set    = MakeActiveSet("v2024");
        var factor = MakeFactor(set.Id, value: 0.25m);
        var wm     = MakeWasteMove(FakeCurrentUserService.TenantA);
        var line   = MakeLine(wm.Id, distanceKm: 100m);

        await db.EmissionFactorSets.AddAsync(set);
        await db.EmissionFactors.AddAsync(factor);
        await db.WasteMoves.AddAsync(wm);
        await db.WasteMoveResidues.AddAsync(line);
        await db.SaveChangesAsync();

        var handler = new CalculateEmissionsCommandHandler(
            db, NullLogger<CalculateEmissionsCommandHandler>.Instance);

        // Act
        await handler.Handle(new CalculateEmissionsCommand(wm.Id), CancellationToken.None);

        // Assert
        var updated = await db.WasteMoveResidues.FindAsync(line.Id);
        Assert.NotNull(updated);
        Assert.Equal(25m, updated!.TransportInfo_TransportCarbonEmissions);  // 100 km × 0.25
        Assert.Equal(set.Id,      updated.EmissionFactorSetId);
        Assert.Equal("v2024",     updated.EmissionFactorVersion);
    }

    /// <summary>
    /// CASO 2: Factor no encontrado para la combinación → línea omitida, sin excepción.
    /// </summary>
    [Fact]
    public async Task Handle_FactorNotFound_SkipsLineWithoutException()
    {
        // Arrange
        using var db = TestDbContextFactory.CreateDefault();

        var set    = MakeActiveSet();
        // Factor con vehículo diferente al de la línea
        var factor = MakeFactor(set.Id, vehicleType: "Furgoneta");
        var wm     = MakeWasteMove(FakeCurrentUserService.TenantA);
        var line   = MakeLine(wm.Id, distanceKm: 50m, vehicleType: "Camion");

        await db.EmissionFactorSets.AddAsync(set);
        await db.EmissionFactors.AddAsync(factor);
        await db.WasteMoves.AddAsync(wm);
        await db.WasteMoveResidues.AddAsync(line);
        await db.SaveChangesAsync();

        var handler = new CalculateEmissionsCommandHandler(
            db, NullLogger<CalculateEmissionsCommandHandler>.Instance);

        // Act — no debe lanzar excepción
        var ex = await Record.ExceptionAsync(() =>
            handler.Handle(new CalculateEmissionsCommand(wm.Id), CancellationToken.None));

        // Assert
        Assert.Null(ex);

        var notUpdated = await db.WasteMoveResidues.FindAsync(line.Id);
        Assert.Null(notUpdated!.TransportInfo_TransportCarbonEmissions);
        Assert.Null(notUpdated.EmissionFactorSetId);
    }

    /// <summary>
    /// CASO 3: No existe EmissionFactorSet activo → sin cálculo, sin excepción.
    /// </summary>
    [Fact]
    public async Task Handle_NoActiveSet_SkipsAllLinesWithoutException()
    {
        // Arrange
        using var db = TestDbContextFactory.CreateDefault();

        var wm   = MakeWasteMove(FakeCurrentUserService.TenantA);
        var line = MakeLine(wm.Id, distanceKm: 80m);

        await db.WasteMoves.AddAsync(wm);
        await db.WasteMoveResidues.AddAsync(line);
        await db.SaveChangesAsync();

        var handler = new CalculateEmissionsCommandHandler(
            db, NullLogger<CalculateEmissionsCommandHandler>.Instance);

        // Act
        var ex = await Record.ExceptionAsync(() =>
            handler.Handle(new CalculateEmissionsCommand(wm.Id), CancellationToken.None));

        // Assert
        Assert.Null(ex);

        var notUpdated = await db.WasteMoveResidues.FindAsync(line.Id);
        Assert.Null(notUpdated!.TransportInfo_TransportCarbonEmissions);
    }

    /// <summary>
    /// CASO EXTRA: EmissionFactorSet con Status != Active → no se usa.
    /// </summary>
    [Fact]
    public async Task Handle_InactiveSet_NotUsed()
    {
        // Arrange
        using var db = TestDbContextFactory.CreateDefault();

        var inactiveSet = MakeActiveSet();
        inactiveSet.Status = "Inactive";
        var wm   = MakeWasteMove(FakeCurrentUserService.TenantA);
        var line = MakeLine(wm.Id, distanceKm: 60m);

        await db.EmissionFactorSets.AddAsync(inactiveSet);
        await db.WasteMoves.AddAsync(wm);
        await db.WasteMoveResidues.AddAsync(line);
        await db.SaveChangesAsync();

        var handler = new CalculateEmissionsCommandHandler(
            db, NullLogger<CalculateEmissionsCommandHandler>.Instance);

        // Act
        await handler.Handle(new CalculateEmissionsCommand(wm.Id), CancellationToken.None);

        // Assert — sin set activo, la línea no se toca
        var notUpdated = await db.WasteMoveResidues.FindAsync(line.Id);
        Assert.Null(notUpdated!.TransportInfo_TransportCarbonEmissions);
    }
}
