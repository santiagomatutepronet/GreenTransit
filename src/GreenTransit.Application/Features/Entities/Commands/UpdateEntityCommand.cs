using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GreenTransit.Application.Features.Entities.Commands;

// ── Comando ───────────────────────────────────────────────────────────────────
public sealed record UpdateEntityCommand(
    Guid    Id,
    string  Name,
    string? NationalId,
    string? CenterCode,
    string  EntityRole,
    string? EntityType,
    string? EconomicActivity,
    string? TypeThirdParty,
    string? InscriptionType,
    string? InscriptionNumber,
    string? CountryCode,
    string? StateCode,
    string? ProvinceCode,
    string? MunicipalityCode,
    string? ZipCode,
    string? Address,
    string? Latitude,
    string? Longitude,
    string? PhoneNumber,
    string? Email,
    string? ContactPerson
) : IRequest;

// ── Handler ───────────────────────────────────────────────────────────────────
public sealed class UpdateEntityCommandHandler : IRequestHandler<UpdateEntityCommand>
{
    private readonly IUnitOfWork           _uow;
    private readonly IApplicationDbContext _context;
    private readonly ILogger<UpdateEntityCommandHandler> _logger;

    public UpdateEntityCommandHandler(
        IUnitOfWork uow,
        IApplicationDbContext context,
        ILogger<UpdateEntityCommandHandler> logger)
    {
        _uow     = uow;
        _context = context;
        _logger  = logger;
    }

    public async Task Handle(UpdateEntityCommand request, CancellationToken ct)
    {
        var entity = await _context.BusinessEntities
            .FirstOrDefaultAsync(e => e.Id == request.Id, ct)
            ?? throw new DomainException($"Entidad {request.Id} no encontrada.");

        // Si cambia el rol y existe usuario vinculado → error de dominio
        if (entity.EntityRole != request.EntityRole)
        {
            var hasLinked = !string.IsNullOrWhiteSpace(entity.Email)
                && await _context.AppUsers
                    .IgnoreQueryFilters()
                    .AnyAsync(u => u.Email == entity.Email || u.Login == entity.Email, ct);

            if (hasLinked)
                throw new DomainException(
                    $"No se puede cambiar el rol de la entidad '{entity.Name}' porque tiene " +
                    "un usuario vinculado. Desvincula o elimina el usuario antes de cambiar el rol.");
        }

        entity.Name              = request.Name;
        entity.NationalId        = request.NationalId;
        entity.CenterCode        = request.CenterCode;
        entity.EntityRole        = request.EntityRole;
        entity.EntityType        = request.EntityType;
        entity.EconomicActivity  = request.EconomicActivity;
        entity.TypeThirdParty    = request.TypeThirdParty;
        entity.InscriptionType   = request.InscriptionType;
        entity.InscriptionNumber = request.InscriptionNumber;
        entity.CountryCode       = request.CountryCode;
        entity.StateCode         = request.StateCode;
        entity.ProvinceCode      = request.ProvinceCode;
        entity.MunicipalityCode  = request.MunicipalityCode;
        entity.ZipCode           = request.ZipCode;
        entity.Address           = request.Address;
        entity.Latitude          = request.Latitude;
        entity.Longitude         = request.Longitude;
        entity.PhoneNumber       = request.PhoneNumber;
        entity.Email             = request.Email;
        entity.ContactPerson     = request.ContactPerson;

        _uow.BusinessEntities.Update(entity);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Entidad {Id} actualizada.", entity.Id);
    }
}
