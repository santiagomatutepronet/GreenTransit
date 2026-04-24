using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repositorio de usuarios que accede a AppDbContext incluyendo el perfil
/// para poder emitir el claim de rol en ClaimsTransformation.
/// </summary>
public sealed class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public Task<AppUser?> FindByLoginAsync(
        string login,
        CancellationToken cancellationToken = default)
        => _context.AppUsers
            .Include(u => u.Profile)
            .IgnoreQueryFilters()          // Users usa int PK — sin filtro de tenant
            .FirstOrDefaultAsync(u => u.Login == login, cancellationToken);
}
