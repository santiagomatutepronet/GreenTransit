using GreenTransit.Application.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GreenTransit.Application.Common.Behaviours;

/// <summary>
/// Pipeline behavior que centraliza el manejo de excepciones de dominio conocidas.
/// Se registra primero en el pipeline para envolver todo lo demás.
///
/// Responsabilidades:
/// - Deja pasar <see cref="FluentValidation.ValidationException"/> y
///   <see cref="ForbiddenAccessException"/> sin reenvolver (ya tienen formato estándar).
/// - Loguea <see cref="KeyNotFoundException"/> como Warning (recurso no encontrado → 404).
/// - Loguea <see cref="InvalidOperationException"/> como Warning (regla de negocio → 422).
/// - Cualquier otra excepción no controlada sube como Error.
///
/// Orden final del pipeline:
///   ExceptionHandling → Logging → Authorization → Validation → Transaction → Handler
/// </summary>
public sealed class ExceptionHandlingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<ExceptionHandlingBehavior<TRequest, TResponse>> _logger;

    public ExceptionHandlingBehavior(
        ILogger<ExceptionHandlingBehavior<TRequest, TResponse>> logger)
        => _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        try
        {
            return await next(cancellationToken);
        }
        catch (FluentValidation.ValidationException)
        {
            // Ya tiene formato estándar de errores; el middleware de Blazor la mapea.
            throw;
        }
        catch (ForbiddenAccessException)
        {
            // Manejada por el middleware de autorización; no reenvolver.
            throw;
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(
                ex,
                "Recurso no encontrado al procesar {RequestName}: {Message}",
                typeof(TRequest).Name, ex.Message);
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                ex,
                "Operación inválida en {RequestName}: {Message}",
                typeof(TRequest).Name, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error no controlado en {RequestName}",
                typeof(TRequest).Name);
            throw;
        }
    }
}
