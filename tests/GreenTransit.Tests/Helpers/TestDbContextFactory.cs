using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GreenTransit.Tests.Helpers;

/// <summary>
/// Crea instancias de AppDbContext usando una base de datos InMemory aislada.
/// Cada llamada genera un nombre de BD único para garantizar aislamiento entre tests.
/// </summary>
public static class TestDbContextFactory
{
    /// <summary>
    /// Crea un AppDbContext InMemory con el servicio de usuario fake inyectado.
    /// La BD se nombra con un GUID único para aislar cada test.
    /// </summary>
    public static AppDbContext Create(ICurrentUserService currentUserService)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            // Suprime la advertencia de que InMemory no soporta transacciones
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var context = new AppDbContext(options, currentUserService);
        context.Database.EnsureCreated();
        return context;
    }

    /// <summary>
    /// Crea un AppDbContext InMemory usando el FakeCurrentUserService por defecto.
    /// </summary>
    public static AppDbContext CreateDefault()
        => Create(new FakeCurrentUserService());
}
