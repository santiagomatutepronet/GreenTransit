namespace GreenTransit.Application.Common.Interfaces;

/// <summary>
/// Evalúa si el usuario actual tiene acceso a una ruta concreta
/// según la tabla PagePermissions.
/// Solo se aplica a rutas que existan en PageDefinitions con IsActive = true.
/// Rutas no registradas en PageDefinitions no son evaluadas por este servicio.
/// </summary>
public interface IPagePermissionService
{
    /// <summary>
    /// Devuelve true si el usuario puede acceder a la ruta.
    /// Devuelve true si la ruta NO está en PageDefinitions (sin gestión dinámica).
    /// Devuelve false si la ruta ESTÁ en PageDefinitions y el perfil no tiene ninguna entrada
    /// en PagePermissions (lista blanca estricta: sin concesión explícita = denegado).
    /// Devuelve false si la ruta ESTÁ en PageDefinitions y el perfil tiene entradas
    /// pero esta ruta concreta no está entre ellas.
    /// </summary>
    Task<bool> CanAccessRouteAsync(string routeTemplate, CancellationToken ct = default);

    /// <summary>
    /// Carga en una sola operación (2 queries + caché) el conjunto completo de rutas
    /// a las que el usuario tiene acceso. Usar en el NavMenu en lugar de llamar
    /// CanAccessRouteAsync individualmente para cada enlace, evitando N queries
    /// concurrentes sobre el mismo DbContext scoped.
    /// </summary>
    Task<IReadOnlySet<string>> GetAllowedRoutesAsync(CancellationToken ct = default);

    /// <summary>
    /// Devuelve true si el usuario tiene permiso de escritura sobre la ruta
    /// (AccessLevel == "Write" | "ReadWrite").
    /// Devuelve false si la ruta no está gestionada o el nivel es solo "Read".
    /// </summary>
    Task<bool> CanWriteRouteAsync(string routeTemplate, CancellationToken ct = default);

    /// <summary>Invalida la caché de permisos para un perfil concreto (tras guardar cambios).</summary>
    Task InvalidateCacheForProfileAsync(int profileId);
}
