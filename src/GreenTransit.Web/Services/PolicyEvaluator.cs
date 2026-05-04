using System.Security.Claims;
using GreenTransit.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace GreenTransit.Web.Services;

/// <summary>
/// Implementación de IPolicyEvaluator que delega en IAuthorizationService de ASP.NET Core.
/// Permite que AuthorizationBehavior (en Application) evalúe policies sin depender
/// directamente de Microsoft.AspNetCore.Authorization.
/// </summary>
public sealed class PolicyEvaluator : IPolicyEvaluator
{
    private readonly IAuthorizationService  _authorizationService;
    private readonly IHttpContextAccessor   _httpContextAccessor;

    public PolicyEvaluator(
        IAuthorizationService authorizationService,
        IHttpContextAccessor  httpContextAccessor)
    {
        _authorizationService = authorizationService;
        _httpContextAccessor  = httpContextAccessor;
    }

    /// <inheritdoc/>
    public async Task<bool> AuthorizeAsync(string policyName)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        // Sin HttpContext (p. ej. worker background) → denegar por precaución.
        if (user is null)
            return false;

        var result = await _authorizationService.AuthorizeAsync(user, policyName);
        return result.Succeeded;
    }
}
