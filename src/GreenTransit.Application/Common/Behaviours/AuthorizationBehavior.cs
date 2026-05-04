using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Exceptions;
using MediatR;

namespace GreenTransit.Application.Common.Behaviours;

/// <summary>
/// Pipeline behavior de MediatR que evalúa la autorización antes de ejecutar el handler.
///
/// Orden en el pipeline:
///   LoggingBehavior → AuthorizationBehavior → ValidationBehavior → Handler
///
/// Funcionamiento:
///   1. Busca atributos [Authorize] en el TRequest via reflexión.
///   2. Si no hay ninguno → pasa directamente al siguiente behavior (sin coste).
///   3. Si los hay, comprueba en orden:
///      a. Que el usuario está autenticado en el sistema (IdUser > 0).
///      b. Si el atributo define Profiles → ICurrentUserService.IsInAnyProfile().
///      c. Si el atributo define Policy → IPolicyEvaluator.AuthorizeAsync().
///   4. Cualquier fallo lanza ForbiddenAccessException (HTTP 403).
///   5. Todos los [Authorize] del request deben cumplirse (AND lógico).
/// </summary>
public sealed class AuthorizationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICurrentUserService _currentUser;
    private readonly IPolicyEvaluator    _policyEvaluator;

    public AuthorizationBehavior(
        ICurrentUserService currentUser,
        IPolicyEvaluator    policyEvaluator)
    {
        _currentUser     = currentUser;
        _policyEvaluator = policyEvaluator;
    }

    public async Task<TResponse> Handle(
        TRequest                          request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken                 cancellationToken)
    {
        // Leer todos los [Authorize] del tipo del request.
        var authorizeAttributes = request
            .GetType()
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .ToArray();

        // Sin atributos → sin restricción; continuar el pipeline.
        if (authorizeAttributes.Length == 0)
            return await next(cancellationToken);

        // Verificar que el usuario existe en la BD (no solo OIDC autenticado).
        if (!_currentUser.IsAuthenticated)
            throw new ForbiddenAccessException(
                "El usuario no está autenticado o no existe en el sistema.");

        // Evaluar cada atributo [Authorize] (AND lógico: todos deben cumplirse).
        foreach (var attr in authorizeAttributes)
        {
            // ── Comprobación por perfiles ─────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(attr.Profiles))
            {
                var allowedProfiles = attr.Profiles
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (!_currentUser.IsInAnyProfile(allowedProfiles))
                    throw new ForbiddenAccessException(
                        $"El perfil '{_currentUser.ProfileReference}' no tiene acceso a esta operación. " +
                        $"Perfiles requeridos: {attr.Profiles}");
            }

            // ── Comprobación por policy ───────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(attr.Policy))
            {
                var authorized = await _policyEvaluator.AuthorizeAsync(attr.Policy);
                if (!authorized)
                    throw new ForbiddenAccessException(
                        $"El usuario no cumple la policy '{attr.Policy}'.");
            }
        }

        return await next(cancellationToken);
    }
}
