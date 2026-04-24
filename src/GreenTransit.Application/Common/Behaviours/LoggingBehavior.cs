using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GreenTransit.Application.Common.Behaviours;

/// <summary>
/// Pipeline behavior que loguea entrada, salida y tiempo de ejecución
/// de cada comando/query que atraviesa el pipeline de MediatR.
/// El log va a Serilog de forma transparente vía Microsoft.Extensions.Logging.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        _logger.LogInformation(
            "→ Iniciando {RequestName} {@Request}", requestName, request);

        var sw = Stopwatch.StartNew();

        try
        {
            var response = await next(cancellationToken);
            sw.Stop();

            _logger.LogInformation(
                "← Completado {RequestName} en {ElapsedMs} ms {@Response}",
                requestName, sw.ElapsedMilliseconds, response);

            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();

            _logger.LogError(
                ex,
                "✗ Error en {RequestName} tras {ElapsedMs} ms",
                requestName, sw.ElapsedMilliseconds);

            throw;
        }
    }
}
