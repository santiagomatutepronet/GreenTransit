using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Entities.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.Entities.Queries;

// ── Query ─────────────────────────────────────────────────────────────────────
public sealed record GetEntityByIdQuery(Guid Id) : IRequest<EntityDetailDto?>;

// ── Handler ───────────────────────────────────────────────────────────────────
public sealed class GetEntityByIdQueryHandler
    : IRequestHandler<GetEntityByIdQuery, EntityDetailDto?>
{
    private readonly IApplicationDbContext _context;

    public GetEntityByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<EntityDetailDto?> Handle(
        GetEntityByIdQuery request,
        CancellationToken cancellationToken)
    {
        var entity = await _context.BusinessEntities
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);

        if (entity is null) return null;

        // Busca el usuario vinculado por Login = Email de la entidad
        var linkedUser = string.IsNullOrWhiteSpace(entity.Email)
            ? null
            : await _context.AppUsers
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(u => u.Email == entity.Email || u.Login == entity.Email)
                .Select(u => new { u.Id, u.Login })
                .FirstOrDefaultAsync(cancellationToken);

        return new EntityDetailDto(
            entity.Id, entity.Name, entity.NationalId, entity.CenterCode,
            entity.EntityRole, entity.EntityType, entity.EconomicActivity,
            entity.TypeThirdParty, entity.InscriptionType, entity.InscriptionNumber,
            entity.CountryCode, entity.StateCode, entity.ProvinceCode,
            entity.MunicipalityCode, entity.ZipCode, entity.Address,
            entity.Latitude, entity.Longitude,
            entity.PhoneNumber, entity.Email, entity.ContactPerson,
            entity.IsActive, entity.SourceSystem,
            entity.CreatedAt, entity.UpdatedAt, entity.IdUser,
            linkedUser?.Id, linkedUser?.Login
        );
    }
}
