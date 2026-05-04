namespace GreenTransit.Application.Common.Behaviours;

/// <summary>
/// Marca un command o query de MediatR como protegido por autorización.
/// El AuthorizationBehavior evalúa este atributo antes de ejecutar el handler.
///
/// Se puede aplicar varias veces en el mismo request (AllowMultiple = true):
/// todos los atributos deben superarse (AND lógico entre atributos).
///
/// Uso:
/// [Authorize(Profiles = "ADMIN,DISPATCH_OFFICE")]
/// public record CreateWasteMoveCommand : IRequest&lt;Guid&gt; { ... }
///
/// [Authorize(Policy = PolicyConstants.CanManageWasteMoves)]
/// public record CreateWasteMoveCommand : IRequest&lt;Guid&gt; { ... }
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class AuthorizeAttribute : Attribute
{
    /// <summary>
    /// Perfiles permitidos separados por coma (valores de ProfileConstants).
    /// Ejemplo: "ADMIN,DISPATCH_OFFICE"
    /// Si se rellena, el behavior comprueba ICurrentUserService.IsInAnyProfile().
    /// </summary>
    public string Profiles { get; init; } = "";

    /// <summary>
    /// Nombre de una policy de ASP.NET Core Authorization (valores de PolicyConstants).
    /// Si se rellena, el behavior invoca IAuthorizationService.AuthorizeAsync().
    /// </summary>
    public string Policy { get; init; } = "";
}
