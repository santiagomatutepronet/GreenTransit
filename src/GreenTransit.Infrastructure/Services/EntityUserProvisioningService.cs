using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Constants;
using GreenTransit.Domain.Entities;
using GreenTransit.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GreenTransit.Infrastructure.Services;

/// <summary>
/// Implementación de IEntityUserProvisioningService.
/// Crea un registro Users para la entidad si su EntityRole tiene perfil mapeado.
/// Resuelve las FKs geográficas desde los códigos de la entidad.
/// </summary>
public sealed class EntityUserProvisioningService : IEntityUserProvisioningService
{
    private readonly IApplicationDbContext                  _context;
    private readonly ICurrentUserService                    _currentUser;
    private readonly ILogger<EntityUserProvisioningService> _logger;

    public EntityUserProvisioningService(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        ILogger<EntityUserProvisioningService> logger)
    {
        _context     = context;
        _currentUser = currentUser;
        _logger      = logger;
    }

    /// <inheritdoc/>
    public async Task<int?> ProvisionUserForEntityAsync(
        BusinessEntity entity,
        string? suggestedLogin = null,
        CancellationToken ct = default)
    {
        // ── 1. Verificar si el rol genera usuario ─────────────────────────────
        var profileRef = EntityRoles.GetAutoUserProfile(entity.EntityRole);
        if (profileRef is null)
        {
            _logger.LogDebug(
                "ProvisionUser: rol {Role} no genera usuario. Entidad {Name} omitida.",
                entity.EntityRole, entity.Name);
            return null;
        }

        // ── 2. Calcular login ─────────────────────────────────────────────────
        var login = !string.IsNullOrWhiteSpace(suggestedLogin)
            ? suggestedLogin
            : !string.IsNullOrWhiteSpace(entity.Email)
                ? entity.Email
                : entity.NationalId;

        if (string.IsNullOrWhiteSpace(login))
            throw new DomainException(
                $"No se puede crear usuario para la entidad '{entity.Name}' " +
                $"(rol {entity.EntityRole}): el Email y el NationalId están vacíos. " +
                "Al menos uno de los dos es obligatorio para generar el Login del usuario.");

        // ── 3. Comprobar duplicado ────────────────────────────────────────────
        var existingUser = await _context.AppUsers
            .IgnoreQueryFilters()
            .Where(u => u.Login == login)
            .Select(u => u.Id)
            .FirstOrDefaultAsync(ct);

        if (existingUser != 0)
        {
            _logger.LogInformation(
                "ProvisionUser: usuario con Login={Login} ya existe (ID={Id}). No se crea duplicado.",
                login, existingUser);
            return existingUser;
        }

        // ── 4. Buscar perfil ──────────────────────────────────────────────────
        var profileId = await _context.UserProfiles
            .IgnoreQueryFilters()
            .Where(p => p.Reference == profileRef)
            .Select(p => p.Id)
            .FirstOrDefaultAsync(ct);

        if (profileId == 0)
            throw new DomainException(
                $"No se encontró el perfil '{profileRef}' en la tabla Profiles. " +
                "Verifique que el seed de perfiles esté ejecutado.");

        // ── 5. Resolver FKs geográficas (null-safe, sin fallar si no existe) ──
        int? countryId      = null;
        int? territoryId    = null;
        int? municipalityId = null;

        if (!string.IsNullOrWhiteSpace(entity.CountryCode))
        {
            countryId = await _context.Countries
                .IgnoreQueryFilters()
                .Where(c => c.Code == entity.CountryCode)
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync(ct);
        }

        if (!string.IsNullOrWhiteSpace(entity.StateCode))
        {
            territoryId = await _context.TerritoryStates
                .IgnoreQueryFilters()
                .Where(s => s.Code == entity.StateCode)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync(ct);
        }

        if (!string.IsNullOrWhiteSpace(entity.MunicipalityCode))
        {
            municipalityId = await _context.Municipalities
                .IgnoreQueryFilters()
                .Where(m => m.Code == entity.MunicipalityCode)
                .Select(m => (int?)m.Id)
                .FirstOrDefaultAsync(ct);
        }

        // ── 6. Crear el registro Users ────────────────────────────────────────
        var user = new AppUser
        {
            Login           = login,
            Email           = entity.Email,
            CompleteName    = entity.Name,
            IdProfile       = profileId,
            OwnerId         = _currentUser.OwnerId == Guid.Empty ? null : _currentUser.OwnerId,
            NationalId      = countryId,
            GeographicalId  = territoryId,
            MunicipalityId  = municipalityId,
            CreateDate      = DateTime.UtcNow
        };

        _context.AppUsers.Add(user);

        _logger.LogInformation(
            "ProvisionUser: usuario {Login} (perfil {Profile}) provisionado para entidad {EntityId}. Pendiente de SaveChanges.",
            login, profileRef, entity.Id);

        // El SaveChanges lo ejecuta el handler que llama a este servicio (misma transacción)
        return null; // El ID real quedará asignado por EF tras SaveChanges
    }
}
