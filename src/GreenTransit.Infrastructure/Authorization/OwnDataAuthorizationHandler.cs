using GreenTransit.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace GreenTransit.Infrastructure.Authorization;

/// <summary>
/// Handler que evalúa OwnDataRequirement: verifica que el usuario tiene perfil permitido
/// y (opcionalmente) que tiene una entidad vinculada en el sistema.
///
/// Este handler NO filtra los datos: el filtrado real (WHERE IdCarrier = @entityId, etc.)
/// lo aplican los query handlers de MediatR consultando ICurrentUserService.LinkedEntityId.
///
/// Registrar como: services.AddScoped&lt;IAuthorizationHandler, OwnDataAuthorizationHandler&gt;()
/// </summary>
public sealed class OwnDataAuthorizationHandler
    : AuthorizationHandler<OwnDataRequirement>
{
    private readonly ICurrentUserService _currentUser;

    public OwnDataAuthorizationHandler(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    /// <inheritdoc/>
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OwnDataRequirement requirement)
    {
        // Si el usuario no está autenticado en el sistema, no proceder.
        if (!_currentUser.IsAuthenticated)
            return Task.CompletedTask;

        var profile = _currentUser.ProfileReference;

        // Verificar que el perfil está en la lista permitida.
        var profileAllowed = requirement.AllowedProfiles.Any(p =>
            string.Equals(p, profile, StringComparison.OrdinalIgnoreCase));

        if (!profileAllowed)
            return Task.CompletedTask;

        // Si el requisito exige entidad vinculada, verificar que exista.
        if (requirement.RequiresEntityLink && _currentUser.LinkedEntityId is null)
        {
            // Perfil operativo sin entidad vinculada — no puede operar sobre datos propios.
            // No llamamos Succeed(); ASP.NET Core denegará si ningún handler tiene éxito.
            return Task.CompletedTask;
        }

        context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
