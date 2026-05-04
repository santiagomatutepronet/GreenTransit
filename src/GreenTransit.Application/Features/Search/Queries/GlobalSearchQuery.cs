using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Search.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.Search.Queries;

/// <summary>
/// Busca en paralelo sobre ServiceOrders, WasteMoves, EntryPlants, WasteMoveResidues
/// (DI/NT), Agreements y Entidades (BusinessEntity).
/// Si searchTerm tiene menos de 3 caracteres devuelve una lista vacía.
/// Todos los resultados están filtrados por OwnerId del usuario autenticado.
/// Máximo 5 resultados por tipo.
/// </summary>
public sealed record GlobalSearchQuery(string SearchTerm) : IRequest<GlobalSearchResultDto>;

public sealed class GlobalSearchQueryHandler
    : IRequestHandler<GlobalSearchQuery, GlobalSearchResultDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService   _currentUser;

    public GlobalSearchQueryHandler(
        IApplicationDbContext db,
        ICurrentUserService   currentUser)
    {
        _db          = db;
        _currentUser = currentUser;
    }

    public async Task<GlobalSearchResultDto> Handle(
        GlobalSearchQuery request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.SearchTerm)
            || request.SearchTerm.Trim().Length < 3)
            return new GlobalSearchResultDto([]);

        var term    = request.SearchTerm.Trim();
        var ownerId = _currentUser.OwnerId;

        // ── Búsquedas secuenciales (DbContext no es thread-safe) ─────────────
        var items = new List<GlobalSearchItemDto>();
        items.AddRange(await SearchServiceOrdersAsync(term, ownerId, ct));
        items.AddRange(await SearchWasteMovesAsync(term, ownerId, ct));
        items.AddRange(await SearchEntryPlantsAsync(term, ownerId, ct));
        items.AddRange(await SearchDiNtAsync(term, ownerId, ct));
        items.AddRange(await SearchAgreementsAsync(term, ownerId, ct));
        items.AddRange(await SearchEntitiesAsync(term, ownerId, ct));

        return new GlobalSearchResultDto(items);
    }

    // ── ServiceOrders ─────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<GlobalSearchItemDto>> SearchServiceOrdersAsync(
        string term, Guid ownerId, CancellationToken ct)
    {
        var rows = await _db.ServiceOrders
            .AsNoTracking()
            .Where(s => (ownerId == Guid.Empty || s.OwnerId == ownerId)
                     && s.ServiceOrderNumber.Contains(term))
            .OrderByDescending(s => s.IssuedAt)
            .Take(5)
            .Select(s => new { s.Id, s.ServiceOrderNumber, s.Status, s.IssuedByName })
            .ToListAsync(ct);

        return rows.Select(s => new GlobalSearchItemDto(
            s.Id.ToString(),
            s.ServiceOrderNumber,
            $"OS · {s.IssuedByName ?? "—"} · {s.Status ?? "—"}",
            GlobalSearchItemType.ServiceOrder,
            $"/service-orders/{s.Id}")).ToList();
    }

    // ── WasteMoves (por referencia) ───────────────────────────────────────────

    private async Task<IReadOnlyList<GlobalSearchItemDto>> SearchWasteMovesAsync(
        string term, Guid ownerId, CancellationToken ct)
    {
        var rows = await _db.WasteMoves
            .AsNoTracking()
            .Where(w => (ownerId == Guid.Empty || w.OwnerId == ownerId)
                     && w.WasteMoveReference != null
                     && w.WasteMoveReference.Contains(term))
            .OrderByDescending(w => w.RequestDate)
            .Take(5)
            .Select(w => new { w.Id, w.WasteMoveReference, w.ServiceStatus })
            .ToListAsync(ct);

        return rows.Select(w => new GlobalSearchItemDto(
            w.Id.ToString(),
            w.WasteMoveReference ?? w.Id.ToString(),
            $"Traslado · {w.ServiceStatus ?? "—"}",
            GlobalSearchItemType.WasteMove,
            $"/waste-moves/{w.Id}/timeline")).ToList();
    }

    // ── EntryPlants (por TicketScale) ─────────────────────────────────────────

    private async Task<IReadOnlyList<GlobalSearchItemDto>> SearchEntryPlantsAsync(
        string term, Guid ownerId, CancellationToken ct)
    {
        var rows = await _db.EntryPlants
            .AsNoTracking()
            .Where(e => (ownerId == Guid.Empty || e.OwnerId == ownerId)
                     && e.TicketScale != null
                     && e.TicketScale.Contains(term))
            .OrderByDescending(e => e.PlantEntryDate)
            .Take(5)
            .Select(e => new { e.Id, e.TicketScale, e.PlantEntryDate, e.NetWeight })
            .ToListAsync(ct);

        return rows.Select(e => new GlobalSearchItemDto(
            e.Id.ToString(),
            e.TicketScale ?? e.Id.ToString(),
            $"Ticket báscula · {e.PlantEntryDate:dd/MM/yyyy} · {e.NetWeight} kg",
            GlobalSearchItemType.EntryPlant,
            $"/entry-plants/{e.Id}")).ToList();
    }

    // ── WasteMoveResidues (DI / NT) ───────────────────────────────────────────

    private async Task<IReadOnlyList<GlobalSearchItemDto>> SearchDiNtAsync(
        string term, Guid ownerId, CancellationToken ct)
    {
        var rows = await _db.WasteMoveResidues
            .AsNoTracking()
            .Include(r => r.WasteMove)
            .Where(r => (ownerId == Guid.Empty || r.WasteMove!.OwnerId == ownerId)
                     && (r.DINumber != null && r.DINumber.Contains(term)
                      || r.NTNumber != null && r.NTNumber.Contains(term)))
            .Take(5)
            .Select(r => new
            {
                r.Id,
                r.DINumber,
                r.NTNumber,
                WasteMoveId        = r.IdWasteMove,
                WasteMoveReference = r.WasteMove!.WasteMoveReference
            })
            .ToListAsync(ct);

        return rows.Select(r =>
        {
            var docRef = r.DINumber ?? r.NTNumber ?? r.Id.ToString();
            var label  = r.DINumber != null ? "DI" : "NT";
            return new GlobalSearchItemDto(
                r.WasteMoveId.ToString(),
                docRef,
                $"{label} · Traslado {r.WasteMoveReference ?? r.WasteMoveId.ToString()}",
                GlobalSearchItemType.WasteMove,
                $"/waste-moves/{r.WasteMoveId}/timeline");
        }).ToList();
    }

    // ── Agreements ────────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<GlobalSearchItemDto>> SearchAgreementsAsync(
        string term, Guid ownerId, CancellationToken ct)
    {
        var rows = await _db.Agreements
            .AsNoTracking()
            .Where(a => (ownerId == Guid.Empty || a.OwnerId == ownerId)
                     && a.AgreementNumber.Contains(term))
            .OrderByDescending(a => a.EffectiveFrom)
            .Take(5)
            .Select(a => new { a.Id, a.AgreementNumber, a.Status, a.WasteStream })
            .ToListAsync(ct);

        return rows.Select(a => new GlobalSearchItemDto(
            a.Id.ToString(),
            a.AgreementNumber,
            $"Acuerdo · {a.Status ?? "—"} · {a.WasteStream ?? "—"}",
            GlobalSearchItemType.Agreement,
            $"/agreements/{a.Id}")).ToList();
    }

    // ── BusinessEntities ──────────────────────────────────────────────────────

    private async Task<IReadOnlyList<GlobalSearchItemDto>> SearchEntitiesAsync(
        string term, Guid ownerId, CancellationToken ct)
    {
        var rows = await _db.BusinessEntities
            .AsNoTracking()
            .Where(e => e.Name.Contains(term)
                     || e.NationalId != null && e.NationalId.Contains(term)
                     || e.CenterCode != null && e.CenterCode.Contains(term))
            .OrderBy(e => e.Name)
            .Take(5)
            .Select(e => new { e.Id, e.Name, e.NationalId, e.EntityRole })
            .ToListAsync(ct);

        return rows.Select(e => new GlobalSearchItemDto(
            e.Id.ToString(),
            e.Name,
            $"{e.EntityRole ?? "—"} · {e.NationalId ?? "—"}",
            GlobalSearchItemType.Entity,
            $"/entities/{e.Id}")).ToList();
    }
}
