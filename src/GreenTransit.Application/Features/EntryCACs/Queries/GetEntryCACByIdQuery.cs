using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.EntryCACs.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.EntryCACs.Queries;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetEntryCACByIdQuery(Guid Id) : IRequest<EntryCACDetailDto?>;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetEntryCACByIdQueryHandler
    : IRequestHandler<GetEntryCACByIdQuery, EntryCACDetailDto?>
{
    private readonly IApplicationDbContext _context;

    public GetEntryCACByIdQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<EntryCACDetailDto?> Handle(
        GetEntryCACByIdQuery request, CancellationToken ct)
    {
        var e = await _context.EntryCACs
            .AsNoTracking()
            .Include(x => x.EntryCACResidues)
                .ThenInclude(r => r.Residue)
            .FirstOrDefaultAsync(x => x.Id == request.Id, ct);

        if (e is null) return null;

        var residues = e.EntryCACResidues
            .Select(r => new EntryCACResidueDto(
                r.Id,
                r.IdEntryCAC,
                r.IdResidue,
                r.Residue?.Name,
                r.Weight,
                r.MeasureUnit,
                r.Units,
                r.PriceWeight,
                r.PriceUnit))
            .ToList();

        return new EntryCACDetailDto(
            e.Id,
            e.IdWasteMove,
            e.WasteMoveReference,
            e.OwnerId,
            e.CACEntryDate,
            e.TypeContainer,
            e.PriceContainer,
            e.CollectionMethod,
            e.IdUser,
            e.DateCreateSys,
            e.DateModifiedSys,
            residues);
    }
}
