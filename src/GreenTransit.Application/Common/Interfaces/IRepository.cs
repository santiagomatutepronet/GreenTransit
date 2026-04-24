using System.Linq.Expressions;

namespace GreenTransit.Application.Common.Interfaces;

/// <summary>
/// Repositorio genérico de lectura/escritura.
/// Los métodos de consulta aplican filtro automático por OwnerId
/// cuando la entidad implementa ITenantEntity (gestionado en la implementación).
/// </summary>
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
}
