using System.Linq.Expressions;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación genérica de IRepository&lt;T&gt; sobre EF Core.
/// Aplica filtro automático por OwnerId cuando T implementa ITenantEntity.
/// </summary>
public sealed class EfRepository<T> : IRepository<T> where T : class
{
    private readonly AppDbContext _context;
    private readonly DbSet<T> _set;
    private readonly Guid? _ownerId;

    public EfRepository(AppDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _set = context.Set<T>();
        _ownerId = typeof(ITenantEntity).IsAssignableFrom(typeof(T))
            ? currentUserService.OwnerId
            : null;
    }

    /// <inheritdoc/>
    public async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await ApplyTenantFilter(_set).FirstOrDefaultAsync(e => EF.Property<Guid>(e, "Id") == id, ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
        => await ApplyTenantFilter(_set).ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<T>> FindAsync(
        Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => await ApplyTenantFilter(_set).Where(predicate).ToListAsync(ct);

    /// <inheritdoc/>
    public async Task AddAsync(T entity, CancellationToken ct = default)
        => await _set.AddAsync(entity, ct);

    /// <inheritdoc/>
    public void Update(T entity)
        => _set.Update(entity);

    /// <inheritdoc/>
    public void Remove(T entity)
        => _set.Remove(entity);

    /// <inheritdoc/>
    public async Task<bool> ExistsAsync(
        Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => await ApplyTenantFilter(_set).AnyAsync(predicate, ct);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IQueryable<T> ApplyTenantFilter(IQueryable<T> query)
    {
        if (_ownerId is null || _ownerId == Guid.Empty)
            return query;

        return query.Where(e => EF.Property<Guid?>(e, "OwnerId") == _ownerId);
    }
}
