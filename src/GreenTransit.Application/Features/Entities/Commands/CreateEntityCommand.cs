using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Constants;
using GreenTransit.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GreenTransit.Application.Features.Entities.Commands;

// ── Comando ───────────────────────────────────────────────────────────────────
public sealed record CreateEntityCommand(
    // Identificación
    string  Name,
    string? NationalId,
    string? CenterCode,
    string  EntityRole,
    string? EntityType,
    string? EconomicActivity,
    // Clasificación normativa
    string? TypeThirdParty,
    string? InscriptionType,
    string? InscriptionNumber,
    // Localización
    string? CountryCode,
    string? StateCode,
    string? ProvinceCode,
    string? MunicipalityCode,
    string? ZipCode,
    string? Address,
    string? Latitude,
    string? Longitude,
    // Contacto
    string? PhoneNumber,
    string? Email,
    string? ContactPerson,
    // Acceso al sistema (provisión automática de usuario)
    string? SuggestedLogin = null
) : IRequest<Guid>;

// ── Handler ───────────────────────────────────────────────────────────────────
public sealed class CreateEntityCommandHandler
    : IRequestHandler<CreateEntityCommand, Guid>
{
    private readonly IUnitOfWork           _uow;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;
    private readonly ILogger<CreateEntityCommandHandler> _logger;

    public CreateEntityCommandHandler(
        IUnitOfWork uow,
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        ILogger<CreateEntityCommandHandler> logger)
    {
        _uow         = uow;
        _context     = context;
        _currentUser = currentUser;
        _logger      = logger;
    }

    public async Task<Guid> Handle(CreateEntityCommand request, CancellationToken ct)
    {
        // ── Crear la entidad ──────────────────────────────────────────────────
        var entity = new BusinessEntity
        {
            Id                = Guid.NewGuid(),
            Name              = request.Name,
            NationalId        = request.NationalId,
            CenterCode        = request.CenterCode,
            EntityRole        = request.EntityRole,
            EntityType        = request.EntityType,
            EconomicActivity  = request.EconomicActivity,
            TypeThirdParty    = request.TypeThirdParty,
            InscriptionType   = request.InscriptionType,
            InscriptionNumber = request.InscriptionNumber,
            CountryCode       = request.CountryCode,
            StateCode         = request.StateCode,
            ProvinceCode      = request.ProvinceCode,
            MunicipalityCode  = request.MunicipalityCode,
            ZipCode           = request.ZipCode,
            Address           = request.Address,
            Latitude          = request.Latitude,
            Longitude         = request.Longitude,
            PhoneNumber       = request.PhoneNumber,
            Email             = request.Email,
            ContactPerson     = request.ContactPerson,
            IsActive          = true,
            IdUser            = _currentUser.IdUser
        };

        await _uow.BusinessEntities.AddAsync(entity, ct);

        // ── Provisión automática de usuario ───────────────────────────────────
        var profileRef = EntityRoles.GetAutoUserProfile(request.EntityRole);
        if (profileRef is not null && !string.IsNullOrWhiteSpace(request.Email))
        {
            var profile = await _context.UserProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Reference == profileRef, ct);

            if (profile is not null)
            {
                var login = !string.IsNullOrWhiteSpace(request.SuggestedLogin)
                    ? request.SuggestedLogin
                    : request.Email;

                var appUser = new AppUser
                {
                    Login       = login,
                    CompleteName= request.Name,
                    Email       = request.Email,
                    IdProfile   = profile.Id,
                    OwnerId     = _currentUser.OwnerId == Guid.Empty ? null : _currentUser.OwnerId,
                    CreateDate  = DateTime.UtcNow
                };

                await _uow.AppUsers.AddAsync(appUser, ct);
                _logger.LogInformation(
                    "Usuario {Login} (perfil {Profile}) provisionado para entidad {EntityId}",
                    login, profileRef, entity.Id);
            }
        }

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Entidad {Name} ({Role}) creada con Id {Id}",
            entity.Name, entity.EntityRole, entity.Id);

        return entity.Id;
    }
}
