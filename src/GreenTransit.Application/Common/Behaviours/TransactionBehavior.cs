using GreenTransit.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GreenTransit.Application.Common.Behaviours;

/// <summary>
/// Pipeline behavior que envuelve en una transacción de BD los commands que implementan
/// <see cref="ITransactional"/>. Se registra al final del pipeline, justo antes del handler,
/// para que Logging, Authorization y Validation ya hayan pasado.
///
/// Orden final del pipeline:
///   ExceptionHandling → Logging → Authorization → Validation → Transaction → Handler
///
/// Uso en un command:
///   public sealed record GenerateSettlementCommand(...) : IRequest&lt;Guid&gt;, ITransactional;
/// </summary>
public sealed class TransactionBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;

    public TransactionBehavior(
        IApplicationDbContext context,
        ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    {
        _context = context;
        _logger  = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Solo actúa sobre requests que implementen ITransactional
        if (request is not ITransactional)
            return await next(cancellationToken);

        var requestName = typeof(TRequest).Name;
        _logger.LogDebug("Iniciando transacción para {RequestName}", requestName);

        await _context.BeginTransactionAsync(cancellationToken);
        try
        {
            var response = await next(cancellationToken);
            await _context.CommitTransactionAsync(cancellationToken);
            _logger.LogDebug("Transacción confirmada para {RequestName}", requestName);
            return response;
        }
        catch
        {
            await _context.RollbackTransactionAsync(cancellationToken);
            _logger.LogWarning("Transacción revertida para {RequestName}", requestName);
            throw;
        }
    }
}
