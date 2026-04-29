using GreenTransit.Domain.Entities;
using GreenTransit.Infrastructure.Services;
using GreenTransit.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GreenTransit.Tests.Infrastructure;

/// <summary>
/// Tests unitarios del servicio DumZoneService.
/// Usa AppDbContext InMemory para aislar la infraestructura.
/// </summary>
public sealed class DumZoneServiceTests
{
    // Coordenadas del punto de recogida: (lat=40.4, lng=-3.7) — centro de Madrid
    private const double PickupLat = 40.4;
    private const double PickupLng = -3.7;

    // Polígono que envuelve el punto (lat=40.4, lng=-3.7)
    private const string PolygonContaining = """
        {
          "type": "Polygon",
          "coordinates": [[
            [-3.8, 40.3], [-3.6, 40.3], [-3.6, 40.5],
            [-3.8, 40.5], [-3.8, 40.3]
          ]]
        }
        """;

    // Polígono que NO envuelve el punto
    private const string PolygonOutside = """
        {
          "type": "Polygon",
          "coordinates": [[
            [-4.0, 41.0], [-3.9, 41.0], [-3.9, 41.1],
            [-4.0, 41.1], [-4.0, 41.0]
          ]]
        }
        """;

    // Polígono cóncavo en forma de L que sí contiene el punto
    private const string PolygonConcave = """
        {
          "type": "Polygon",
          "coordinates": [[
            [-3.8, 40.3], [-3.6, 40.3], [-3.6, 40.45],
            [-3.65, 40.45], [-3.65, 40.5], [-3.8, 40.5],
            [-3.8, 40.3]
          ]]
        }
        """;

    private static BusinessEntity MakePickupEntity(double lat, double lng) => new()
    {
        Id        = Guid.NewGuid(),
        Name      = "Punto Test",
        EntityRole = "Producer",
        IsActive  = true,
        Latitude  = lat.ToString(System.Globalization.CultureInfo.InvariantCulture),
        Longitude = lng.ToString(System.Globalization.CultureInfo.InvariantCulture),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static DumZone MakeZone(string zoneCode, string geometryJson) => new()
    {
        Id           = Guid.NewGuid(),
        ZoneCode     = zoneCode,
        Name         = $"Zona {zoneCode}",
        Status       = "Active",
        GeometryJson = geometryJson,
        CreatedAt    = DateTime.UtcNow,
        UpdatedAt    = DateTime.UtcNow
    };

    private static DumRestrictionRule MakeRule(
        Guid zoneId, string actionType, DateTime? validFrom = null, DateTime? validTo = null,
        string? reason = null, string conditionsJson = "{}") => new()
    {
        Id             = Guid.NewGuid(),
        ZoneId         = zoneId,
        RuleCode       = $"RULE-{Guid.NewGuid():N}"[..12],
        Status         = "Active",
        ActionType     = actionType,
        ActionReason   = reason,
        ConditionsJson = conditionsJson,
        ValidFrom      = validFrom ?? DateTime.UtcNow.AddDays(-1),
        ValidTo        = validTo,
        CreatedAt      = DateTime.UtcNow,
        UpdatedAt      = DateTime.UtcNow
    };

    // ── Caso 1: sin zonas → Allow ─────────────────────────────────────────────

    [Fact]
    public async Task NoZones_Returns_Allow()
    {
        var ctx     = TestDbContextFactory.CreateDefault();
        var service = new DumZoneService(ctx, NullLogger<DumZoneService>.Instance);
        var pickup  = MakePickupEntity(PickupLat, PickupLng);
        ctx.BusinessEntities.Add(pickup);
        await ctx.SaveChangesAsync();

        var result = await service.CheckPickupPointAsync(
            pickup.Id, DateTime.UtcNow, "Camion", "Euro6");

        Assert.Equal("Allow", result.ActionType);
        Assert.Empty(result.ZoneCodes);
    }

    // ── Caso 2: punto dentro de zona con regla Block → Block ─────────────────

    [Fact]
    public async Task PointInsideZone_BlockRule_Returns_Block()
    {
        var ctx     = TestDbContextFactory.CreateDefault();
        var service = new DumZoneService(ctx, NullLogger<DumZoneService>.Instance);

        var pickup = MakePickupEntity(PickupLat, PickupLng);
        var zone   = MakeZone("Z01", PolygonContaining);
        var rule   = MakeRule(zone.Id, "Block", reason: "Acceso restringido DUM");
        zone.DumRestrictionRules.Add(rule);

        ctx.BusinessEntities.Add(pickup);
        ctx.DumZones.Add(zone);
        await ctx.SaveChangesAsync();

        var result = await service.CheckPickupPointAsync(
            pickup.Id, DateTime.UtcNow, "Camion", "Euro6");

        Assert.Equal("Block", result.ActionType);
        Assert.Equal("Acceso restringido DUM", result.Reason);
        Assert.Contains("Z01", result.ZoneCodes);
    }

    // ── Caso 3: punto dentro de zona con regla Notify → Notify ───────────────

    [Fact]
    public async Task PointInsideZone_NotifyRule_Returns_Notify()
    {
        var ctx     = TestDbContextFactory.CreateDefault();
        var service = new DumZoneService(ctx, NullLogger<DumZoneService>.Instance);

        var pickup = MakePickupEntity(PickupLat, PickupLng);
        var zone   = MakeZone("Z02", PolygonContaining);
        var rule   = MakeRule(zone.Id, "Notify", reason: "Zona de vigilancia");
        zone.DumRestrictionRules.Add(rule);

        ctx.BusinessEntities.Add(pickup);
        ctx.DumZones.Add(zone);
        await ctx.SaveChangesAsync();

        var result = await service.CheckPickupPointAsync(
            pickup.Id, DateTime.UtcNow, null, null);

        Assert.Equal("Notify", result.ActionType);
        Assert.Contains("Z02", result.ZoneCodes);
    }

    // ── Caso 4: punto fuera de todas las zonas → Allow ────────────────────────

    [Fact]
    public async Task PointOutsideAllZones_Returns_Allow()
    {
        var ctx     = TestDbContextFactory.CreateDefault();
        var service = new DumZoneService(ctx, NullLogger<DumZoneService>.Instance);

        var pickup = MakePickupEntity(PickupLat, PickupLng);
        var zone   = MakeZone("Z03", PolygonOutside); // polígono lejos del punto
        var rule   = MakeRule(zone.Id, "Block");
        zone.DumRestrictionRules.Add(rule);

        ctx.BusinessEntities.Add(pickup);
        ctx.DumZones.Add(zone);
        await ctx.SaveChangesAsync();

        var result = await service.CheckPickupPointAsync(
            pickup.Id, DateTime.UtcNow, "Camion", "Euro6");

        Assert.Equal("Allow", result.ActionType);
    }

    // ── Caso 5: regla expirada (ValidTo < plannedDate) → Allow ────────────────

    [Fact]
    public async Task ExpiredRule_Returns_Allow()
    {
        var ctx     = TestDbContextFactory.CreateDefault();
        var service = new DumZoneService(ctx, NullLogger<DumZoneService>.Instance);

        var pickup      = MakePickupEntity(PickupLat, PickupLng);
        var zone        = MakeZone("Z04", PolygonContaining);
        // La regla venció ayer
        var expiredRule = MakeRule(zone.Id, "Block",
            validFrom: DateTime.UtcNow.AddDays(-30),
            validTo:   DateTime.UtcNow.AddDays(-1));
        zone.DumRestrictionRules.Add(expiredRule);

        ctx.BusinessEntities.Add(pickup);
        ctx.DumZones.Add(zone);
        await ctx.SaveChangesAsync();

        var result = await service.CheckPickupPointAsync(
            pickup.Id, DateTime.UtcNow, "Camion", "Euro6");

        Assert.Equal("Allow", result.ActionType);
    }

    // ── Caso 6: polígono cóncavo — punto dentro → Block ───────────────────────

    [Fact]
    public async Task ConcavePolygon_PointInside_Returns_Block()
    {
        var ctx     = TestDbContextFactory.CreateDefault();
        var service = new DumZoneService(ctx, NullLogger<DumZoneService>.Instance);

        var pickup = MakePickupEntity(PickupLat, PickupLng); // (40.4, -3.7) — dentro del L
        var zone   = MakeZone("Z05", PolygonConcave);
        var rule   = MakeRule(zone.Id, "Block", reason: "Zona cóncava restringida");
        zone.DumRestrictionRules.Add(rule);

        ctx.BusinessEntities.Add(pickup);
        ctx.DumZones.Add(zone);
        await ctx.SaveChangesAsync();

        var result = await service.CheckPickupPointAsync(
            pickup.Id, DateTime.UtcNow, "Camion", "Euro5");

        Assert.Equal("Block", result.ActionType);
    }

    // ── Caso 7: resultado más restrictivo cuando hay varias reglas ─────────────

    [Fact]
    public async Task MultipleRules_Returns_MostRestrictive()
    {
        var ctx     = TestDbContextFactory.CreateDefault();
        var service = new DumZoneService(ctx, NullLogger<DumZoneService>.Instance);

        var pickup = MakePickupEntity(PickupLat, PickupLng);
        var zone   = MakeZone("Z06", PolygonContaining);
        zone.DumRestrictionRules.Add(MakeRule(zone.Id, "Notify",   reason: "Aviso general"));
        zone.DumRestrictionRules.Add(MakeRule(zone.Id, "Restrict", reason: "Restricción horaria"));
        zone.DumRestrictionRules.Add(MakeRule(zone.Id, "Block",    reason: "Bloqueo total"));

        ctx.BusinessEntities.Add(pickup);
        ctx.DumZones.Add(zone);
        await ctx.SaveChangesAsync();

        var result = await service.CheckPickupPointAsync(
            pickup.Id, DateTime.UtcNow, "Camion", "Euro6");

        Assert.Equal("Block", result.ActionType);
    }

    // ── Tests del algoritmo ray-casting (unitarios puros) ─────────────────────

    [Theory]
    [InlineData(40.4,  -3.7,   true)]   // dentro del cuadrado
    [InlineData(41.0,  -3.7,   false)]  // fuera por arriba
    [InlineData(40.4,  -4.5,   false)]  // fuera por la izquierda
    [InlineData(40.3,  -3.7,   false)]  // en el borde inferior (fuera)
    public void RayCasting_SquarePolygon(double lat, double lng, bool expected)
    {
        // Cuadrado: lng [-3.8,-3.6] lat [40.3, 40.5]
        double[][] ring =
        [
            [-3.8, 40.3], [-3.6, 40.3], [-3.6, 40.5],
            [-3.8, 40.5], [-3.8, 40.3]
        ];

        var result = DumZoneService.PointInPolygon(lat, lng, ring);
        Assert.Equal(expected, result);
    }
}
