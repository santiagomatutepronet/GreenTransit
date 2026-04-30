using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.DumZones.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GreenTransit.Application.Features.DumZones.Queries;

// ── Query ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Simula la comprobación de restricciones DUM para un punto (lat/lng),
/// una fecha/hora y unas características de vehículo.
/// No requiere que el punto sea una entidad del maestro.
/// </summary>
public sealed record SimulateDumCheckQuery(
    decimal  Latitude,
    decimal  Longitude,
    DateTime Date,
    string?  VehicleType,
    string?  EuroClass
) : IRequest<DumSimulationResultDto>;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class SimulateDumCheckQueryHandler
    : IRequestHandler<SimulateDumCheckQuery, DumSimulationResultDto>
{
    private readonly IApplicationDbContext _context;

    public SimulateDumCheckQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<DumSimulationResultDto> Handle(
        SimulateDumCheckQuery request, CancellationToken ct)
    {
        var lat = (double)request.Latitude;
        var lng = (double)request.Longitude;

        var zones = await _context.DumZones
            .AsNoTracking()
            .Where(z => z.Status == "Active")
            .Include(z => z.DumRestrictionRules
                .Where(r => r.ValidFrom <= request.Date
                         && (r.ValidTo == null || r.ValidTo >= request.Date)))
            .ToListAsync(ct);

        var applied = new List<ActiveRuleApplied>();

        foreach (var zone in zones)
        {
            var ring = ParseGeoJsonRing(zone.GeometryJson);
            if (ring is null || ring.Length < 3) continue;
            if (!PointInPolygon(lat, lng, ring)) continue;

            foreach (var rule in zone.DumRestrictionRules)
            {
                if (!MatchesVehicleConditions(rule.ConditionsJson, request.VehicleType, request.EuroClass))
                    continue;

                applied.Add(new ActiveRuleApplied(
                    zone.ZoneCode,
                    rule.RuleCode,
                    rule.ActionType,
                    rule.ActionReason,
                    rule.ConditionsJson
                ));
            }
        }

        if (applied.Count == 0)
        {
            return new DumSimulationResultDto("Allow", null, [], []);
        }

        // Acción más restrictiva
        var worst = applied
            .OrderByDescending(a => ActionOrder(a.ActionType))
            .First();

        var zoneCodes = applied.Select(a => a.ZoneCode).Distinct().ToArray();

        return new DumSimulationResultDto(
            worst.ActionType,
            worst.ActionReason,
            zoneCodes,
            applied.AsReadOnly()
        );
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static int ActionOrder(string a) => a switch
    {
        "Block"    => 4,
        "Restrict" => 3,
        "Notify"   => 2,
        _          => 1
    };

    /// <summary>
    /// Extrae el primer anillo exterior del GeoJSON (Polygon o MultiPolygon).
    /// Devuelve un array de [lng, lat].
    /// </summary>
    private static double[][]? ParseGeoJsonRing(string geometryJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(geometryJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeEl)) return null;
            var type = typeEl.GetString();

            JsonElement coordinates;
            if (!root.TryGetProperty("coordinates", out coordinates)) return null;

            JsonElement ring;
            if (type == "Polygon")
            {
                ring = coordinates[0];
            }
            else if (type == "MultiPolygon")
            {
                ring = coordinates[0][0];
            }
            else return null;

            var result = new double[ring.GetArrayLength()][];
            int i = 0;
            foreach (var pt in ring.EnumerateArray())
            {
                result[i++] = [pt[0].GetDouble(), pt[1].GetDouble()];
            }
            return result;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Algoritmo ray-casting para point-in-polygon.</summary>
    private static bool PointInPolygon(double lat, double lng, double[][] ring)
    {
        bool inside = false;
        int n = ring.Length;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            double xi = ring[i][0], yi = ring[i][1]; // lng, lat
            double xj = ring[j][0], yj = ring[j][1];

            bool intersect = ((yi > lat) != (yj > lat))
                          && (lng < (xj - xi) * (lat - yi) / (yj - yi) + xi);
            if (intersect) inside = !inside;
        }
        return inside;
    }

    /// <summary>
    /// Comprueba si el vehículo cumple las condiciones de la regla.
    /// Si ConditionsJson no especifica restricciones de vehículo, aplica a todos.
    /// </summary>
    private static bool MatchesVehicleConditions(
        string conditionsJson, string? vehicleType, string? euroClass)
    {
        try
        {
            using var doc = JsonDocument.Parse(conditionsJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("vehicleTypes", out var vtEl))
            {
                var allowed = vtEl.EnumerateArray()
                    .Select(e => e.GetString())
                    .ToArray();
                if (allowed.Length > 0
                    && !string.IsNullOrWhiteSpace(vehicleType)
                    && !allowed.Contains(vehicleType, StringComparer.OrdinalIgnoreCase))
                    return false;
            }

            if (root.TryGetProperty("minEuroClass", out var ecEl))
            {
                var minEuro = ecEl.GetString();
                if (!string.IsNullOrWhiteSpace(minEuro)
                    && !string.IsNullOrWhiteSpace(euroClass)
                    && string.Compare(euroClass, minEuro, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            }

            return true;
        }
        catch
        {
            return true; // Si no se puede parsear, la regla aplica
        }
    }
}
