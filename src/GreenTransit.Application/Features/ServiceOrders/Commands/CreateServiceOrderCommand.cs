using MediatR;
using Microsoft.Extensions.Logging;

namespace GreenTransit.Application.Features.ServiceOrders.Commands;

// ── Comando ───────────────────────────────────────────────────────────────────
public sealed record CreateServiceOrderCommand(
    Guid OwnerId,
    string ServiceOrderNumber,
    string Status,
    string Priority
) : IRequest<Guid>;

// ── Handler ───────────────────────────────────────────────────────────────────

/// <summary>
/// Ejemplo de CommandHandler MediatR con ILogger inyectado.
/// El log va a Serilog de forma transparente vía Microsoft.Extensions.Logging.
/// </summary>
public sealed class CreateServiceOrderCommandHandler
    : IRequestHandler<CreateServiceOrderCommand, Guid>
{
    private readonly ILogger<CreateServiceOrderCommandHandler> _logger;

    public CreateServiceOrderCommandHandler(
        ILogger<CreateServiceOrderCommandHandler> logger)
    {
        _logger = logger;
    }

    public async Task<Guid> Handle(
        CreateServiceOrderCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Creando ServiceOrder {@Command}", request);

        // ── Lógica de negocio ─────────────────────────────────────────────────
        var id = Guid.NewGuid();

        _logger.LogInformation(
            "ServiceOrder {Id} creada correctamente para tenant {OwnerId}",
            id, request.OwnerId);

        return await Task.FromResult(id);
    }
}
