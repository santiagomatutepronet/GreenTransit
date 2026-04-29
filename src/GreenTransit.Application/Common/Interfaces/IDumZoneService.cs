namespace GreenTransit.Application.Common.Interfaces;

/// <summary>
/// Resultado de la comprobación de una coordenada contra las Zonas DUM activas.
/// </summary>
public sealed record DumCheckResult(
    /// <summary>Acción más restrictiva encontrada: Allow | Notify | Restrict | Block</summary>
    string   ActionType,
    /// <summary>Motivo de la acción (si la regla lo indica).</summary>
    string?  Reason,
    /// <summary>Códigos de zona que aplican la restricción.</summary>
    string[] ZoneCodes
);

/// <summary>
/// Verifica si un punto de recogida cae dentro de una Zona DUM con restricciones activas.
/// Implementado en GreenTransit.Infrastructure.Services.DumZoneService.
/// </summary>
public interface IDumZoneService
{
    /// <param name="pickupPointId">Id de la BusinessEntity usada como punto de recogida.</param>
    /// <param name="plannedDate">Fecha de recogida planificada (UTC).</param>
    /// <param name="vehicleType">Tipo de vehículo (ej. "Camion", "Furgoneta").</param>
    /// <param name="euroClass">Clase Euro del vehículo (ej. "Euro6").</param>
    /// <param name="ct">Token de cancelación.</param>
    Task<DumCheckResult> CheckPickupPointAsync(
        Guid              pickupPointId,
        DateTime          plannedDate,
        string?           vehicleType,
        string?           euroClass,
        CancellationToken ct = default);
}
