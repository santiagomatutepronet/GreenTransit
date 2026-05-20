using GreenTransit.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.Reporting.Queries;

// ── DTO ───────────────────────────────────────────────────────────────────────

public sealed record DocumentRepositoryItemDto(
    string    Id,
    string    SourceType,
    string    Reference,
    string    DocumentType,
    DateTime? Date,
    string?   Hash,
    string?   SignatureStatus
);

// ── Query ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Devuelve el listado de documentos del repositorio documental del tenant.
/// Incluye documentos de acuerdos, traslados y liquidaciones con filtros opcionales.
/// </summary>
public sealed record GetDocumentRepositoryQuery(
    string?   FilterType     = null,
    string?   FilterRef      = null,
    DateTime? FilterDateFrom = null,
    DateTime? FilterDateTo   = null
) : IRequest<List<DocumentRepositoryItemDto>>;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetDocumentRepositoryQueryHandler
    : IRequestHandler<GetDocumentRepositoryQuery, List<DocumentRepositoryItemDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetDocumentRepositoryQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<List<DocumentRepositoryItemDto>> Handle(
        GetDocumentRepositoryQuery request, CancellationToken ct)
    {
        var ownerId = _currentUser.OwnerId;
        var list    = new List<DocumentRepositoryItemDto>();

        // ── AgreementDocuments ────────────────────────────────────────────────
        if (request.FilterType is null or "Agreement")
        {
            var aQuery = _context.AgreementDocuments
                .AsNoTracking()
                .Include(d => d.Agreement)
                .Where(d => ownerId == Guid.Empty || d.Agreement.OwnerId == ownerId);

            if (!string.IsNullOrWhiteSpace(request.FilterRef))
                aQuery = aQuery.Where(d => d.Agreement.AgreementNumber.Contains(request.FilterRef));
            if (request.FilterDateFrom.HasValue)
                aQuery = aQuery.Where(d => d.SignedAt >= request.FilterDateFrom);
            if (request.FilterDateTo.HasValue)
                aQuery = aQuery.Where(d => d.SignedAt <= request.FilterDateTo.Value.AddDays(1));

            var agrDocs = await aQuery
                .OrderByDescending(d => d.SignedAt)
                .Select(d => new {
                    d.Id, d.DocumentType, d.DocumentHash, d.SignedAt, d.SignatureProvider,
                    Reference = d.Agreement.AgreementNumber
                })
                .ToListAsync(ct);

            list.AddRange(agrDocs.Select(d => new DocumentRepositoryItemDto(
                d.Id.ToString(), "Agreement", d.Reference,
                d.DocumentType, d.SignedAt, d.DocumentHash, d.SignatureProvider)));
        }

        // ── WasteMoves ────────────────────────────────────────────────────────
        if (request.FilterType is null or "WasteMove")
        {
            var wmQuery = _context.WasteMoves
                .AsNoTracking()
                .Where(w => (ownerId == Guid.Empty || w.OwnerId == ownerId)
                         && w.DocumentId != null);

            if (!string.IsNullOrWhiteSpace(request.FilterRef))
                wmQuery = wmQuery.Where(w => w.WasteMoveReference != null &&
                                             w.WasteMoveReference.Contains(request.FilterRef));
            if (request.FilterDateFrom.HasValue)
                wmQuery = wmQuery.Where(w => w.DateCreateSys >= request.FilterDateFrom);
            if (request.FilterDateTo.HasValue)
                wmQuery = wmQuery.Where(w => w.DateCreateSys <= request.FilterDateTo.Value.AddDays(1));

            var wmDocs = await wmQuery
                .OrderByDescending(w => w.DateCreateSys)
                .Select(w => new {
                    w.Id, w.DocumentId, w.DocumentHash, w.DateCreateSys,
                    w.SignatureStatus, w.WasteMoveReference
                })
                .Take(200)
                .ToListAsync(ct);

            list.AddRange(wmDocs.Select(w => new DocumentRepositoryItemDto(
                w.Id.ToString(), "WasteMove", w.WasteMoveReference ?? w.Id.ToString(),
                "Documento traslado", w.DateCreateSys, w.DocumentHash, w.SignatureStatus)));
        }

        // ── Settlements ───────────────────────────────────────────────────────
        if (request.FilterType is null or "Settlement")
        {
            var slQuery = _context.Settlements
                .AsNoTracking()
                .Where(s => (ownerId == Guid.Empty || s.OwnerId == ownerId)
                         && s.EvidenceRefsJson != null);

            if (!string.IsNullOrWhiteSpace(request.FilterRef))
                slQuery = slQuery.Where(s => s.SettlementNumber.Contains(request.FilterRef));
            if (request.FilterDateFrom.HasValue)
                slQuery = slQuery.Where(s => s.CreatedAt >= request.FilterDateFrom);
            if (request.FilterDateTo.HasValue)
                slQuery = slQuery.Where(s => s.CreatedAt <= request.FilterDateTo.Value.AddDays(1));

            var slDocs = await slQuery
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => new {
                    s.Id, s.SettlementNumber, s.Hash, s.CreatedAt, s.ValidationStatus
                })
                .Take(100)
                .ToListAsync(ct);

            list.AddRange(slDocs.Select(s => new DocumentRepositoryItemDto(
                s.Id.ToString(), "Settlement", s.SettlementNumber,
                "Liquidación (evidencias)", s.CreatedAt, s.Hash, s.ValidationStatus)));
        }

        return list;
    }
}
