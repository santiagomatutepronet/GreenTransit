using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repositorio de usuarios que accede a AppDbContext incluyendo el perfil
/// para poder emitir los claims internos en ClaimsTransformation.
/// </summary>
public sealed class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    /// Un único SELECT con JOIN a Profiles (Include). No filtra por OwnerId —
    /// la tabla Users usa PK int y no tiene tenant filter global.
    public Task<AppUser?> FindByLoginAsync(
        string login,
        CancellationToken cancellationToken = default)
        => _context.AppUsers
            .Include(u => u.Profile)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Login == login, cancellationToken);

    /// <inheritdoc/>
    public Task<AppUser?> FindByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
        => _context.AppUsers
            .Include(u => u.Profile)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    /// <inheritdoc/>
    /// Busca la primera entidad activa cuyo Email coincide con el email del usuario.
    public Task<Guid?> FindEntityIdByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
        => _context.BusinessEntities
            .IgnoreQueryFilters()
            .Where(e => e.Email == email && e.IsActive)
            .Select(e => (Guid?)e.Id)
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<IEnumerable<string>> GetAllLoginsAsync(
        CancellationToken cancellationToken = default)
    {
        var users = await _context.AppUsers
            .IgnoreQueryFilters()
            .Select(u => new { u.Login, u.Email })
            .Take(10)
            .ToListAsync(cancellationToken);
        return users.Select(u => $"{u.Login} | {u.Email}");
    }
}
