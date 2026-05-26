using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace GreenTransit.Infrastructure.Persistence;

/// <summary>
/// Factory para EF Core tooling (migrations add/update) cuando no hay startup-project disponible.
/// Solo se usa en tiempo de diseño; no afecta al runtime.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        // Connection string de diseño — solo para generar migraciones
        optionsBuilder.UseSqlServer(
            "Server=(localdb)\\mssqllocaldb;Database=GreenTransit_Design;Trusted_Connection=True;",
            sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));

        // ICurrentUserService stub mínimo para satisfacer el constructor
        var services = new ServiceCollection();
        services.AddScoped<GreenTransit.Application.Common.Interfaces.ICurrentUserService,
            DesignTimeCurrentUserService>();
        var sp = services.BuildServiceProvider();
        var currentUser = sp.GetRequiredService<GreenTransit.Application.Common.Interfaces.ICurrentUserService>();

        return new AppDbContext(optionsBuilder.Options, currentUser);
    }
}

/// <summary>Stub de ICurrentUserService para el contexto de diseño (migrations).</summary>
internal class DesignTimeCurrentUserService
    : GreenTransit.Application.Common.Interfaces.ICurrentUserService
{
    public bool IsAuthenticated => false;
    public int IdUser => 0;
    public string Login => string.Empty;
    public string Email => string.Empty;
    public string UserName => string.Empty;
    public Guid OwnerId => Guid.Empty;
    public int ProfileId => 0;
    public string UserProfile => string.Empty;
    public Guid? LinkedEntityId => null;
    public bool IsInProfile(string profileRef) => false;
    public bool IsInAnyProfile(params string[] profileRefs) => false;
    public void SetInteractiveUser(System.Security.Claims.ClaimsPrincipal user) { }
}
