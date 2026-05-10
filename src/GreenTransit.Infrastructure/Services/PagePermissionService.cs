using System.Collections.Immutable;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GreenTransit.Infrastructure.Services;

/// <summary>
/// Evalúa si el perfil del usuario tiene acceso a una ruta según PagePermissions.
///
/// Usa IServiceScopeFactory para crear su propio DbContext en cada operación de BD,
/// evitando compartir el AppDbContext scoped del circuito Blazor con otros componentes
/// (previene InvalidOperationException de concurrencia en EF Core en Blazor Server).
/// PageDefinitions y PagePermissions no tienen filtro de tenant: son seguras de leer
/// desde cualquier scope independientemente del usuario activo.
/// Las consultas se cachean en IMemoryCache durante 5 minutos.
/// </summary>
public sealed class PagePermissionService : IPagePermissionService
{
    private readonly IServiceScopeFactory           _scopeFactory;
    private readonly ICurrentUserService            _currentUser;
    private readonly IMemoryCache                   _cache;
    private readonly ILogger<PagePermissionService> _logger;

    private static readonly TimeSpan CacheDuration      = TimeSpan.FromMinutes(5);
    private const           string   AllDefsCacheKey     = "PagePerms_AllDefs";

    public PagePermissionService(
        IServiceScopeFactory           scopeFactory,
        ICurrentUserService            currentUser,
        IMemoryCache                   cache,
        ILogger<PagePermissionService> logger)
    {
        _scopeFactory = scopeFactory;
        _currentUser  = currentUser;
        _cache        = cache;
        _logger       = logger;
    }

    // ── Interfaz pública ─────────────────────────────────────────────────────

    public async Task<bool> CanAccessRouteAsync(string routeTemplate, CancellationToken ct = default)
    {
        var definition = await GetDefinitionAsync(routeTemplate, ct);
        if (definition is null)
            return true;                              // ruta no gestionada → delegar a [Authorize]

        var profileId = _currentUser.ProfileId;
        if (profileId == 0)
            return false;

        var granted = await GetProfilePermissionsAsync(profileId, ct);

        if (granted.Count > 0)
            return granted.Contains(definition.ID);

        // Ruta gestionada + perfil con 0 entradas → lista blanca estricta = denegado
        _logger.LogDebug(
            "Perfil {ProfileId} sin permisos → denegado para {Route}", profileId, routeTemplate);
        return false;
    }

    public async Task<IReadOnlySet<string>> GetAllowedRoutesAsync(CancellationToken ct = default)
    {
        var profileId = _currentUser.ProfileId;
        if (profileId == 0)
            return ImmutableHashSet<string>.Empty;

        var granted = await GetProfilePermissionsAsync(profileId, ct);
        if (granted.Count == 0)
            return ImmutableHashSet<string>.Empty;

        var allDefs = await GetAllActiveDefinitionsAsync(ct);

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var def in allDefs)
        {
            if (granted.Contains(def.ID))
                result.Add(def.Route);
        }
        return result;
    }

    public async Task InvalidateCacheForProfileAsync(int profileId)
    {
        _cache.Remove(ProfileCacheKey(profileId));
        _cache.Remove(AllDefsCacheKey);
        await Task.CompletedTask;
    }

    // ── Privados ─────────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<PageDefinition>> GetAllActiveDefinitionsAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue(AllDefsCacheKey, out IReadOnlyList<PageDefinition>? hit) && hit is not null)
            return hit;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var list = (IReadOnlyList<PageDefinition>)await ctx.PageDefinitions
            .AsNoTracking()
            .Where(d => d.IsActive)
            .ToListAsync(ct);

        _cache.Set(AllDefsCacheKey, list, CacheDuration);
        return list;
    }

    private async Task<PageDefinition?> GetDefinitionAsync(string routeTemplate, CancellationToken ct)
    {
        var key = $"PageDef_{routeTemplate}";
        if (_cache.TryGetValue(key, out PageDefinition? hit))
            return hit;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var def = await ctx.PageDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Route == routeTemplate && d.IsActive, ct);

        _cache.Set(key, def, CacheDuration);
        return def;
    }

    private async Task<HashSet<int>> GetProfilePermissionsAsync(int profileId, CancellationToken ct)
    {
        var key = ProfileCacheKey(profileId);
        if (_cache.TryGetValue(key, out HashSet<int>? hit) && hit is not null)
            return hit;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var ids = await ctx.PagePermissions
            .AsNoTracking()
            .Where(p => p.IdProfile == profileId)
            .Select(p => p.IdPageDefinition)
            .ToHashSetAsync(ct);

        _cache.Set(key, ids, CacheDuration);
        _logger.LogDebug("Permisos cargados para perfil {ProfileId}: {Count} páginas", profileId, ids.Count);
        return ids;
    }

    private static string ProfileCacheKey(int profileId) => $"PagePerms_Profile_{profileId}";
}
