using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.WasteMoves.DTOs;
using MediatR;

namespace GreenTransit.Application.Features.Reporting.Queries;

/// <summary>
/// Busca el ciclo de vida completo de un residuo por DINumber, NTNumber,
/// TicketScale o WasteMoveReference.
/// Filtra siempre por OwnerId del usuario autenticado.
/// Reutiliza <see cref="WasteMoveTimelineDto"/> como DTO de respuesta.
/// </summary>
public sealed record GetResidueTraceabilityQuery(string SearchTerm)
    : IRequest<WasteMoveTimelineDto?>;

public sealed class GetResidueTraceabilityQueryHandler
    : IRequestHandler<GetResidueTraceabilityQuery, WasteMoveTimelineDto?>
{
    private readonly IMediator           _mediator;
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService   _currentUser;

    public GetResidueTraceabilityQueryHandler(
        IMediator            mediator,
        IApplicationDbContext db,
        ICurrentUserService   currentUser)
    {
        _mediator    = mediator;
        _db          = db;
        _currentUser = currentUser;
    }

    public async Task<WasteMoveTimelineDto?> Handle(
        GetResidueTraceabilityQuery request, CancellationToken ct)
    {
        var term    = request.SearchTerm?.Trim() ?? string.Empty;
        var ownerId = _currentUser.OwnerId;

        if (string.IsNullOrEmpty(term)) return null;

        // Resolver WasteMoveId a partir del término de búsqueda
        Guid? wasteMoveId = await ResolveWasteMoveIdAsync(term, ownerId, ct);

        if (wasteMoveId is null) return null;

        // Reutilizar el handler del timeline pasando el WasteMoveId
        return await _mediator.Send(
            new WasteMoves.Queries.GetWasteMoveTimelineQuery(wasteMoveId.Value), ct);
    }

    private async Task<Guid?> ResolveWasteMoveIdAsync(
        string term, Guid ownerId, CancellationToken ct)
    {
        // 1. Por WasteMoveReference
        var wmByRef = await _db.WasteMoves
            .AsNoTracking()
            .Where(w => (ownerId == Guid.Empty || w.OwnerId == ownerId)
                     && w.WasteMoveReference != null
                     && w.WasteMoveReference == term)
            .Select(w => (Guid?)w.Id)
            .FirstOrDefaultAsync(ct);

        if (wmByRef.HasValue) return wmByRef;

        // 2. Por DINumber o NTNumber en WasteMoveResidues
        var wmByDoc = await _db.WasteMoveResidues
            .AsNoTracking()
            .Include(r => r.WasteMove)
            .Where(r => (ownerId == Guid.Empty || r.WasteMove!.OwnerId == ownerId)
                     && (r.DINumber == term || r.NTNumber == term))
            .Select(r => (Guid?)r.IdWasteMove)
            .FirstOrDefaultAsync(ct);

        if (wmByDoc.HasValue) return wmByDoc;

        // 3. Por TicketScale en EntryPlants
        var wmByTicket = await _db.EntryPlants
            .AsNoTracking()
            .Where(e => (ownerId == Guid.Empty || e.OwnerId == ownerId)
                     && e.TicketScale == term)
            .Select(e => (Guid?)e.IdWasteMove)
            .FirstOrDefaultAsync(ct);

        if (wmByTicket.HasValue) return wmByTicket;

        // 4. Por TicketScale en TreatmentPlants
        var wmByTpTicket = await _db.TreatmentPlants
            .AsNoTracking()
            .Where(t => (ownerId == Guid.Empty || t.OwnerId == ownerId)
                     && t.TicketScale == term
                     && t.IdWasteMove.HasValue)
            .Select(t => t.IdWasteMove)
            .FirstOrDefaultAsync(ct);

        return wmByTpTicket;
    }
}
