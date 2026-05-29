using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GreenTransit.Infrastructure.Persistence;

/// <summary>
/// Inicializa la base de datos en el arranque:
/// - Aplica migraciones EF pendientes.
/// - Ejecuta seed idempotente de datos maestros (Profiles).
/// Seguro para ejecutar en cada arranque: usa INSERT ... WHERE NOT EXISTS.
/// </summary>
public sealed class DbInitializer : IDbInitializer
{
    private readonly AppDbContext          _context;
    private readonly ILogger<DbInitializer> _logger;
    private readonly bool                   _autoMigrate;

    public DbInitializer(AppDbContext context, ILogger<DbInitializer> logger, IConfiguration configuration)
    {
        _context     = context;
        _logger      = logger;
        _autoMigrate = configuration.GetValue<bool>("Database:AutoMigrate", defaultValue: true);
    }

    /// <inheritdoc/>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!_autoMigrate)
        {
            _logger.LogInformation("DbInitializer: migraciones automáticas deshabilitadas (Database:AutoMigrate=false).");
        }
        else
        {
            try
            {
                // Aplica migraciones pendientes (si las hay)
                if ((await _context.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
                {
                    _logger.LogInformation("DbInitializer: aplicando migraciones pendientes…");
                    await _context.Database.MigrateAsync(cancellationToken);
                    _logger.LogInformation("DbInitializer: migraciones completadas.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "DbInitializer: no se pudieron aplicar migraciones (BD puede ser code-first sin migraciones). Continuando con seed.");
            }
        }

        await SeedProfilesAsync(cancellationToken);
        await SeedAdminUserAsync(cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Inserta los 9 perfiles del sistema si no existen (identificados por Reference).
    /// </summary>
    private async Task SeedProfilesAsync(CancellationToken ct)
    {
        // Definición canónica de perfiles del sistema
        var requiredProfiles = new[]
        {
            new { Reference = "ADMIN",           Description = "Administrador del sistema" },
            new { Reference = "SCRAP",           Description = "Sistema Colectivo de Responsabilidad Ampliada" },
            new { Reference = "PRODUCER",        Description = "Productor / Generador de residuos" },
            new { Reference = "CARRIER",         Description = "Transportista" },
            new { Reference = "PLANT_OP",        Description = "Operador de Planta de Tratamiento" },
            new { Reference = "CAC_OP",          Description = "Operador de Centro de Acopio" },
            new { Reference = "PUBLIC_ENT",      Description = "Entidad Pública / Ayuntamiento" },
            new { Reference = "COORDINATOR",     Description = "Coordinador del acuerdo" },
            new { Reference = "DISPATCH_OFFICE", Description = "Oficina de Asignación — Gestor logístico" },
            new { Reference = "REGULATOR",       Description = "Regulador — Autoridad de supervisión normativa" },
            new { Reference = "CERTIFIER",       Description = "Certificador / Auditor — Validación y coherencia" },
        };

        // Leer las referencias que ya existen en la BD
        var existingRefs = await _context.UserProfiles
            .IgnoreQueryFilters()
            .Select(p => p.Reference)
            .ToHashSetAsync(StringComparer.OrdinalIgnoreCase, ct);

        var toInsert = requiredProfiles
            .Where(p => !existingRefs.Contains(p.Reference))
            .ToList();

        if (toInsert.Count == 0)
        {
            _logger.LogDebug("DbInitializer: todos los perfiles ya existen. Seed omitido.");
            return;
        }

        foreach (var p in toInsert)
        {
            _context.UserProfiles.Add(new UserProfile
            {
                Reference   = p.Reference,
                Description = p.Description,
                CreateDate  = DateTime.UtcNow
            });
            _logger.LogInformation("DbInitializer: insertando perfil '{Reference}'.", p.Reference);
        }

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("DbInitializer: seed de Profiles completado ({Count} insertados).",
            toInsert.Count);
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Inserta el usuario administrador inicial si no existe ningún usuario con perfil ADMIN.
    /// Login por defecto: "admin@greentransit.dev" (debe cambiarse tras el primer acceso).
    /// El OwnerId del admin se fija a Guid.Empty para que pueda acceder a todos los tenants
    /// hasta que se le asigne uno manualmente desde la BD o la pantalla de usuarios.
    /// </summary>
    private async Task SeedAdminUserAsync(CancellationToken ct)
    {
        // Buscar el perfil ADMIN (ya debe existir tras SeedProfilesAsync)
        var adminProfile = await _context.UserProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Reference == "ADMIN", ct);

        if (adminProfile is null)
        {
            _logger.LogWarning("DbInitializer: perfil ADMIN no encontrado. Seed de usuario admin omitido.");
            return;
        }

        // Comprobar si ya existe algún usuario administrador con OwnerId correcto
        var adminWithOwner = await _context.AppUsers
            .IgnoreQueryFilters()
            .AnyAsync(u => u.IdProfile == adminProfile.Id && u.OwnerId != null, ct);

        if (adminWithOwner)
        {
            _logger.LogDebug("DbInitializer: usuario ADMIN ya existe con OwnerId asignado. Seed omitido.");
            return;
        }

        // OwnerId demo: el mismo que usa SandboxDataSeeder para que el admin vea los datos sandbox.
        // Se puede cambiar manualmente en BD si el tenant real del admin es otro.
        var demoOwnerId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        // Si ya existe un admin con OwnerId = null, actualizarlo al OwnerId demo
        var existingAdminWithNullOwner = await _context.AppUsers
            .IgnoreQueryFilters()
            .Where(u => u.IdProfile == adminProfile.Id && u.OwnerId == null)
            .ToListAsync(ct);

        if (existingAdminWithNullOwner.Count > 0)
        {
            foreach (var u in existingAdminWithNullOwner)
                u.OwnerId = demoOwnerId;
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation(
                "DbInitializer: {Count} usuario(s) admin con OwnerId=null actualizados a OwnerId={OwnerId}.",
                existingAdminWithNullOwner.Count, demoOwnerId);
            return;
        }

        _context.AppUsers.Add(new AppUser
        {
            Login        = "admin@greentransit.dev",
            CompleteName = "Administrador del sistema",
            Email        = "admin@greentransit.dev",
            IdProfile    = adminProfile.Id,
            OwnerId      = demoOwnerId,
            CreateDate   = DateTime.UtcNow,
        });

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation(
            "DbInitializer: usuario administrador inicial creado (login='admin@greentransit.dev').");
    }
}
