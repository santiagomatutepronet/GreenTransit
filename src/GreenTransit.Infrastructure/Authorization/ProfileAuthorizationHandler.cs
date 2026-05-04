using GreenTransit.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace GreenTransit.Infrastructure.Authorization;

/// <summary>
/// Handler único que evalúa TODAS las policies basadas en ProfileRequirement.
/// La diferencia entre policies es qué perfiles lista cada ProfileRequirement;
/// este handler aplica siempre la misma lógica de evaluación.
///
/// Registrar como: services.AddScoped&lt;IAuthorizationHandler, ProfileAuthorizationHandler&gt;()
/// </summary>
public sealed class ProfileAuthorizationHandler
    : AuthorizationHandler<ProfileRequirement>
{
    private readonly ICurrentUserService _currentUser;

    public ProfileAuthorizationHandler(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    /// <inheritdoc/>
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ProfileRequirement requirement)
    {
        // Si el usuario no está autenticado en el sistema (no existe en Users), no proceder.
        if (!_currentUser.IsAuthenticated)
            return Task.CompletedTask;

        var profile = _currentUser.ProfileReference;

        // Comparación case-insensitive para tolerancia ante valores de BD.
        var isAllowed = requirement.AllowedProfiles.Any(p =>
            string.Equals(p, profile, StringComparison.OrdinalIgnoreCase));

        if (isAllowed)
            context.Succeed(requirement);

        // Si no está en la lista, no llamamos a context.Fail() explícitamente:
        // dejamos que otros handlers puedan evaluar (p. ej. OwnDataAuthorizationHandler
        // que hereda de un OwnDataRequirement separado). ASP.NET falla solo si ningún
        // handler llama Succeed().

        return Task.CompletedTask;
    }
}
