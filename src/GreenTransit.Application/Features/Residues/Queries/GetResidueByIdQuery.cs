using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Residues.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.Residues.Queries;

public sealed record GetResidueByIdQuery(Guid Id) : IRequest<ResidueDetailDto?>;

public sealed class GetResidueByIdQueryHandler
    : IRequestHandler<GetResidueByIdQuery, ResidueDetailDto?>
{
    private readonly IApplicationDbContext _context;

    public GetResidueByIdQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<ResidueDetailDto?> Handle(
        GetResidueByIdQuery request,
        CancellationToken cancellationToken)
    {
        return await _context.Residues
            .AsNoTracking()
            .Where(r => r.Id == request.Id)
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
