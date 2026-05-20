using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Residues.DTOs;
using MediatR;

namespace GreenTransit.Application.Features.Residues.Queries;

public sealed record GetResidueByIdQuery(Guid Id) : IRequest<ResidueDetailDto?>;

public sealed class GetResidueByIdQueryHandler
    : IRequestHandler<GetResidueByIdQuery, ResidueDetailDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetResidueByIdQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<ResidueDetailDto?> Handle(
        GetResidueByIdQuery request,
        CancellationToken cancellationToken)
    {
        // ── Filtro de tenant temporal ──────────────────────────────────────────
        // Residue no implementa ITenantEntity: el HasQueryFilter global no aplica.
        // Se filtra por IdProducer como proxy de tenant para evitar acceso directo
        // a residuos de otros tenants por Id.
        // TODO: reemplazar cuando se añada OwnerId a Residue + migración + backfill.
        var ownerId = _currentUser.IsAuthenticated ? _currentUser.OwnerId : Guid.Empty;

        return await _context.Residues
            .AsNoTracking()
            .Where(r => r.Id == request.Id &&
                        (!_currentUser.IsAuthenticated ||
                         ownerId == Guid.Empty ||
                         r.IdProducer == null ||
                         r.IdProducer == ownerId))
            .Select(r => new ResidueDetailDto(
                r.Id,
                r.ResidueType,
                r.Name,
                r.Description,
                r.Reference,
                r.IdLERCode,
                r.LerCode != null ? r.LerCode.Code : null,
                r.LerCode != null ? r.LerCode.Description : null,
                r.IsDangerous,
                r.IsRAEE,
                r.DangerousCode,
                r.FlowType,
                r.ProductUse,
                r.ProductCategory,
                r.WeightPerUnitKg,
                r.DefaultMeasureUnit,
                r.ReparabilityIndex,
                r.DisassemblyEase,
                r.ContainsHazardous,
                r.RecycledContentPercent,
                r.CompositionJson,
                r.PotentialLERCodesJson,
                r.MaterialsJson,
                r.IdProducer,
                r.Producer != null ? r.Producer.Name : null,
                r.ProducerRef,
                r.SourceSystem,
                r.IsActive,
                r.Version,
                r.Hash,
                r.CreatedAt,
                r.UpdatedAt,
                r.IdUser))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
