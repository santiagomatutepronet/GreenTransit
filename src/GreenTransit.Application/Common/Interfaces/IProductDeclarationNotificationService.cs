namespace GreenTransit.Application.Common.Interfaces;

/// <summary>
/// Servicio de notificaciones del módulo de Declaraciones de Producción.
/// Implementación actual: stub que registra en log.
/// Reemplazable por SignalR o email en fases posteriores.
/// </summary>
public interface IProductDeclarationNotificationService
{
    /// <summary>
    /// Notifica a todos los ADMIN del tenant que se ha emitido una declaración.
    /// Mensaje: "Nueva declaración emitida por {ProducerName} para {Year}/{Period}".
    /// </summary>
    Task NotifyIssuedAsync(
        Guid    declarationId,
        string? producerName,
        int?    year,
        int?    period,
        CancellationToken ct = default);

    /// <summary>
    /// Notifica al PRODUCER vinculado que su declaración ha sido validada.
    /// Mensaje: "Tu declaración {Reference} ha sido validada".
    /// </summary>
    Task NotifyValidatedAsync(
        Guid    declarationId,
        Guid?   idProducer,
        string? reference,
        CancellationToken ct = default);

    /// <summary>
    /// Notifica al PRODUCER vinculado que su declaración ha sido rechazada.
    /// Mensaje: "Tu declaración {Reference} ha sido rechazada. Motivo: {Reason}".
    /// </summary>
    Task NotifyRejectedAsync(
        Guid    declarationId,
        Guid?   idProducer,
        string? reference,
        string  reason,
        CancellationToken ct = default);
}
