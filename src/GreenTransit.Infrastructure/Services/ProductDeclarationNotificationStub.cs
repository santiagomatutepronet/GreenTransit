using GreenTransit.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace GreenTransit.Infrastructure.Services;

/// <summary>
/// Implementación stub de IProductDeclarationNotificationService.
/// Registra los eventos en log (ILogger). 
/// Reemplazar por SignalR o email cuando el módulo de notificaciones esté disponible.
/// </summary>
public sealed class ProductDeclarationNotificationStub
    : IProductDeclarationNotificationService
{
    private readonly ILogger<ProductDeclarationNotificationStub> _logger;

    public ProductDeclarationNotificationStub(
        ILogger<ProductDeclarationNotificationStub> logger)
        => _logger = logger;

    public Task NotifyIssuedAsync(
        Guid    declarationId,
        string? producerName,
        int?    year,
        int?    period,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[NOTIF] Nueva declaración emitida · Id={Id} · Productor={Producer} · {Year}/{Period}",
            declarationId, producerName ?? "—", year, period);
        return Task.CompletedTask;
    }

    public Task NotifyValidatedAsync(
        Guid    declarationId,
        Guid?   idProducer,
        string? reference,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[NOTIF] Declaración validada · Id={Id} · Productor={Producer} · Ref={Ref}",
            declarationId, idProducer, reference ?? "—");
        return Task.CompletedTask;
    }

    public Task NotifyRejectedAsync(
        Guid    declarationId,
        Guid?   idProducer,
        string? reference,
        string  reason,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[NOTIF] Declaración rechazada · Id={Id} · Productor={Producer} · Ref={Ref} · Motivo={Reason}",
            declarationId, idProducer, reference ?? "—", reason);
        return Task.CompletedTask;
    }
}
