using GreenTransit.Application.Common.Behaviours;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Authorization;
using GreenTransit.Domain.Entities;
using GreenTransit.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GreenTransit.Application.Features.Security.Commands;

// ── CreateUserCommand ─────────────────────────────────────────────────────────
/// <summary>Crea un nuevo usuario en el tenant del admin autenticado. Solo perfil ADMIN.</summary>
[Authorize(Profiles = ProfileConstants.Admin)]
public sealed record CreateUserCommand(
    string  Login,
    string? Email,
    string? CompleteName,
    int     IdProfile,
    int?    NationalId,
    int?    GeographicalId,
    int?    MunicipalityId,
    string? ZipCode,
    string? Address,
    string? PortalEDCProvider,
    string? PortalEDCConsumer
) : IRequest<int>;

public sealed class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, int>
{
    private readonly IUnitOfWork          _uow;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService  _currentUser;

    public CreateUserCommandHandler(
        IUnitOfWork uow,
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _uow         = uow;
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(CreateUserCommand request, CancellationToken ct)
    {
        var ownerId = _currentUser.OwnerId;

        var user = new AppUser
        {
            Login           = request.Login,
            Email           = request.Email,
            CompleteName    = request.CompleteName,
            IdProfile       = request.IdProfile,
            OwnerId         = ownerId,
            NationalId      = request.NationalId,
            GeographicalId  = request.GeographicalId,
            MunicipalityId  = request.MunicipalityId,
            ZipCode         = request.ZipCode,
            Address         = request.Address,
            PortalEDCProvider = request.PortalEDCProvider,
            PortalEDCConsumer = request.PortalEDCConsumer,
            CreateDate      = DateTime.UtcNow,
            IsActive        = true
        };

        await _uow.AppUsers.AddAsync(user, ct);
        await _uow.SaveChangesAsync(ct);

        return user.Id;
    }
}

// ── UpdateUserCommand ─────────────────────────────────────────────────────────
/// <summary>Actualiza todos los campos del usuario excepto OwnerId. Solo perfil ADMIN.</summary>
[Authorize(Profiles = ProfileConstants.Admin)]
public sealed record UpdateUserCommand(
    int     Id,
    string  Login,
    string? Email,
    string? CompleteName,
    int     IdProfile,
    int?    NationalId,
    int?    GeographicalId,
    int?    MunicipalityId,
    string? ZipCode,
    string? Address,
    string? PortalEDCProvider,
    string? PortalEDCConsumer
) : IRequest;

public sealed class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand>
{
    private readonly IUnitOfWork           _uow;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;
    private readonly ILogger<UpdateUserCommandHandler> _logger;

    public UpdateUserCommandHandler(
        IUnitOfWork uow,
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        ILogger<UpdateUserCommandHandler> logger)
    {
        _uow         = uow;
        _context     = context;
        _currentUser = currentUser;
        _logger      = logger;
    }

    public async Task Handle(UpdateUserCommand request, CancellationToken ct)
    {
        var ownerId = _currentUser.OwnerId;

        var user = await _context.AppUsers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == request.Id &&
                                     (ownerId == Guid.Empty ? u.OwnerId == null : u.OwnerId == ownerId), ct)
            ?? throw new DomainException($"Usuario {request.Id} no encontrado.");

        if (user.IdProfile != request.IdProfile)
        {
            var oldProfile = await _context.UserProfiles
                .AsNoTracking()
                .Where(p => p.Id == user.IdProfile)
                .Select(p => p.Reference)
                .FirstOrDefaultAsync(ct);
            var newProfile = await _context.UserProfiles
                .AsNoTracking()
                .Where(p => p.Id == request.IdProfile)
                .Select(p => p.Reference)
                .FirstOrDefaultAsync(ct);

            _logger.LogWarning(
                "Cambio de perfil en usuario {UserId} ({Login}): {OldProfile} → {NewProfile}",
                user.Id, user.Login, oldProfile, newProfile);
        }

        user.Login           = request.Login;
        user.Email           = request.Email;
        user.CompleteName    = request.CompleteName;
        user.IdProfile       = request.IdProfile;
        user.NationalId      = request.NationalId;
        user.GeographicalId  = request.GeographicalId;
        user.MunicipalityId  = request.MunicipalityId;
        user.ZipCode         = request.ZipCode;
        user.Address         = request.Address;
        user.PortalEDCProvider = request.PortalEDCProvider;
        user.PortalEDCConsumer = request.PortalEDCConsumer;

        _uow.AppUsers.Update(user);
        await _uow.SaveChangesAsync(ct);
    }
}

// ── DeactivateUserCommand ─────────────────────────────────────────────────────
/// <summary>Bloquea el acceso del usuario estableciendo IsActive = false. Solo perfil ADMIN.</summary>
[Authorize(Profiles = ProfileConstants.Admin)]
public sealed record DeactivateUserCommand(int UserId) : IRequest;

public sealed class DeactivateUserCommandHandler : IRequestHandler<DeactivateUserCommand>
{
    private readonly IUnitOfWork           _uow;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;
    private readonly ILogger<DeactivateUserCommandHandler> _logger;

    public DeactivateUserCommandHandler(
        IUnitOfWork uow,
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        ILogger<DeactivateUserCommandHandler> logger)
    {
        _uow         = uow;
        _context     = context;
        _currentUser = currentUser;
        _logger      = logger;
    }

    public async Task Handle(DeactivateUserCommand request, CancellationToken ct)
    {
        var ownerId = _currentUser.OwnerId;

        var user = await _context.AppUsers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == request.UserId &&
                                     (ownerId == Guid.Empty ? u.OwnerId == null : u.OwnerId == ownerId), ct)
            ?? throw new DomainException($"Usuario {request.UserId} no encontrado.");

        user.IsActive = false;
        _uow.AppUsers.Update(user);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Usuario {UserId} ({Login}) desactivado.", user.Id, user.Login);
    }
}

// ── LinkUserToEntityCommand ───────────────────────────────────────────────────
/// <summary>Vincula un usuario a una BusinessEntity (BusinessEntity.IdUser = userId). Solo perfil ADMIN.</summary>
[Authorize(Profiles = ProfileConstants.Admin)]
public sealed record LinkUserToEntityCommand(int UserId, Guid EntityId) : IRequest;

public sealed class LinkUserToEntityCommandHandler : IRequestHandler<LinkUserToEntityCommand>
{
    private readonly IUnitOfWork           _uow;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;
    private readonly ILogger<LinkUserToEntityCommandHandler> _logger;

    public LinkUserToEntityCommandHandler(
        IUnitOfWork uow,
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        ILogger<LinkUserToEntityCommandHandler> logger)
    {
        _uow         = uow;
        _context     = context;
        _currentUser = currentUser;
        _logger      = logger;
    }

    public async Task Handle(LinkUserToEntityCommand request, CancellationToken ct)
    {
        var ownerId = _currentUser.OwnerId;

        var user = await _context.AppUsers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UserId &&
                                     (ownerId == Guid.Empty ? u.OwnerId == null : u.OwnerId == ownerId), ct)
            ?? throw new DomainException($"Usuario {request.UserId} no encontrado.");

        var entity = await _context.BusinessEntities
            .FirstOrDefaultAsync(e => e.Id == request.EntityId, ct)
            ?? throw new DomainException($"Entidad {request.EntityId} no encontrada.");

        entity.IdUser = request.UserId;
        _uow.BusinessEntities.Update(entity);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Usuario {UserId} ({Login}) vinculado a entidad {EntityId} ({EntityName}).",
            user.Id, user.Login, entity.Id, entity.Name);
    }
}
