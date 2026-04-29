using System.Text.Json;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GreenTransit.Infrastructure.Services;

/// <summary>
/// Verifica si un punto de recogida (lat/lng de una BusinessEntity) cae dentro de
/// alguna Zona DUM activa con reglas de restricción vigentes.
/// Usa el algoritmo ray-casting para la comprobación point-in-polygon (sin deps externas).
/// </summary>
public sealed class DumZoneService : IDumZoneService
{
    private readonly AppDbContext                  _context;
    private readonly ILogger<DumZoneService>       _logger;

    public DumZoneService(AppDbContext context, ILogger<DumZoneService> logger)
    {
        _context = context;
        _logger  = logger;
    }

    public async Task<DumCheckResult> CheckPickupPointAsync(
        Guid              pickupPointId,
        DateTime          plannedDate,
        string?           vehicleType,
        string?           euroClass,
        CancellationToken ct = default)
    {
        // ── 1. Obtener coordenadas del punto de recogida ───────────────────────
        var entity = await _context.BusinessEntities
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == pickupPointId, ct);

        if (entity is null || string.IsNullOrWhiteSpace(entity.Latitude)
                           || string.IsNullOrWhiteSpace(entity.Longitude))
        {
            _logger.LogDebug(
                "DUM: punto {PickupPointId} sin coordenadas — resultado Allow.", pickupPointId);
            return Allow();
        }

        if (!double.TryParse(entity.Latitude,  System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var lat) ||
            !double.TryParse(entity.Longitude, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var lng))
        {
            _logger.LogWarning(
                "DUM: no se pudieron parsear las coordenadas del punto {PickupPointId}.", pickupPointId);
            return Allow();
        }

        // ── 2. Cargar zonas activas con reglas vigentes ───────────────────────
        var zones = await _context.DumZones
            .AsNoTracking()
            .Where(z => z.Status == "Active")
            .Include(z => z.DumRestrictionRules
                .Where(r => r.ValidFrom <= plannedDate
                         && (r.ValidTo == null || r.ValidTo >= plannedDate)))
            .ToListAsync(ct);

        if (zones.Count == 0)
        {
            _logger.LogDebug("DUM: sin zonas activas — resultado Allow.");
            return Allow();
        }

        // ── 3. Evaluar cada zona ──────────────────────────────────────────────
        var matchedResults = new List<(string ActionType, string? Reason, string ZoneCode)>();

        foreach (var zone in zones)
        {
            double[][]? ring = ParseGeoJsonRing(zone.GeometryJson);
            if (ring is null || ring.Length < 3)
            {
                _logger.LogWarning("DUM: zona {ZoneCode} tiene GeometryJson inválido.", zone.ZoneCode);
                continue;
            }

            if (!PointInPolygon(lat, lng, ring))
                continue;

            _logger.LogDebug(
                "DUM: punto ({Lat},{Lng}) dentro de zona {ZoneCode}. Evaluando {RuleCount} reglas.",
                lat, lng, zone.ZoneCode, zone.DumRestrictionRules.Count);

            foreach (var rule in zone.DumRestrictionRules)
            {
                // Evaluar condiciones del JSON si las hay
                if (!EvaluateConditions(rule.ConditionsJson, vehicleType, euroClass))
                    continue;

                matchedResults.Add((rule.ActionType, rule.ActionReason, zone.ZoneCode));
            }
        }

        if (matchedResults.Count == 0)
        {
            _logger.LogDebug("DUM: punto fuera de todas las zonas o sin reglas aplicables — Allow.");
            return Allow();
        }

        // ── 4. Devolver el resultado más restrictivo ───────────────────────────
        var priority = new[] { "Block", "Restrict", "Notify", "Allow" };
        var best     = matchedResults
            .OrderBy(r => Array.IndexOf(priority, r.ActionType))
            .First();

        var zoneCodes = matchedResults.Select(r => r.ZoneCode).Distinct().ToArray();

        _logger.LogDebug(
            "DUM: resultado final {ActionType} para punto {PickupPointId}. Zonas: {Zones}",
            best.ActionType, pickupPointId, string.Join(",", zoneCodes));

        return new DumCheckResult(best.ActionType, best.Reason, zoneCodes);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static DumCheckResult Allow() => new("Allow", null, []);

    /// <summary>
    /// Parsea el primer ring exterior de un GeoJSON Polygon/MultiPolygon.
    /// Devuelve array de [lng, lat] o null si el JSON no es válido.
    /// </summary>
    private static double[][]? ParseGeoJsonRing(string geometryJson)
    {
        try
        {
            using var doc  = JsonDocument.Parse(geometryJson);
            var root       = doc.RootElement;
            var type       = root.GetProperty("type").GetString();
            JsonElement coords;

            if (type == "Polygon")
            {
                // coordinates[0] = exterior ring
                coords = root.GetProperty("coordinates")[0];
            }
            else if (type == "MultiPolygon")
            {
                // coordinates[0][0] = primer polígono, ring exterior
                coords = root.GetProperty("coordinates")[0][0];
            }
            else
            {
                return null;
            }

            var ring = new double[coords.GetArrayLength()][];
            var i    = 0;
            foreach (var point in coords.EnumerateArray())
            {
                var arr   = point.EnumerateArray().ToArray();
                ring[i++] = [arr[0].GetDouble(), arr[1].GetDouble()]; // [lng, lat]
            }
            return ring;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Ray-casting point-in-polygon. Funciona con polígonos convexos y cóncavos.
    /// El array <paramref name="ring"/> contiene puntos [lng, lat].
    /// El punto a testear es (<paramref name="lat"/>, <paramref name="lng"/>).
    /// </summary>
    internal static bool PointInPolygon(double lat, double lng, double[][] ring)
    {
        var inside = false;
        var n      = ring.Length;

        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            // ring[i] = [lng_i, lat_i]
            double xi = ring[i][0], yi = ring[i][1]; // lng, lat
            double xj = ring[j][0], yj = ring[j][1];

            // Ray cast horizontal desde (lng, lat)
            if ((yi > lat) != (yj > lat) &&
                lng < (xj - xi) * (lat - yi) / (yj - yi) + xi)
            {
                inside = !inside;
            }
        }
        return inside;
    }

    /// <summary>
    /// Evalúa las condiciones de la regla (JSON) contra el vehículo.
    /// Si el JSON está vacío o no hay condiciones que restrinjan, devuelve true.
    /// Soporta claves opcionales: "vehicleTypes" (array string) y "minEuroClass" (string).
    /// </summary>
    private static bool EvaluateConditions(string conditionsJson, string? vehicleType, string? euroClass)
    {
        if (string.IsNullOrWhiteSpace(conditionsJson) || conditionsJson.Trim() == "{}")
            return true;

        try
        {
            using var doc  = JsonDocument.Parse(conditionsJson);
            var root       = doc.RootElement;

            // vehicleTypes: ["Camion", "Furgoneta"]
            if (root.TryGetProperty("vehicleTypes", out var vt))
            {
                var allowed = vt.EnumerateArray().Select(e => e.GetString()).ToList();
                if (allowed.Count > 0 && !allowed.Contains(vehicleType, StringComparer.OrdinalIgnoreCase))
                    return false;
            }

            // minEuroClass: "Euro5" → vehículo debe ser >= Euro5
            if (root.TryGetProperty("minEuroClass", out var me))
            {
                var minClass = me.GetString();
                if (!string.IsNullOrEmpty(minClass) && !string.IsNullOrEmpty(euroClass))
                {
                    if (string.Compare(euroClass, minClass, StringComparison.OrdinalIgnoreCase) < 0)
                        return true; // La regla aplica porque el vehículo NO cumple el mínimo
                }
            }

            return true;
        }
        catch
        {
            return true;
        }
    }
}
