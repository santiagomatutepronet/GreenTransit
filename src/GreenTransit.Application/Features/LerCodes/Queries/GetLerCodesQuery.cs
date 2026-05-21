using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Common.Models;
using GreenTransit.Application.Features.LerCodes.DTOs;
using MediatR;

namespace GreenTransit.Application.Features.LerCodes.Queries;

/// <summary>
/// Devuelve la estructura jerárquica capítulo → subcapítulo → códigos LER.
/// Catálogo global: sin filtro OwnerId.
/// </summary>
public sealed record GetLerCodesQuery(
    string? Chapter     = null,
    string? SubChapter  = null,
    bool?   IsDangerous = null,
    bool?   IsRAEE      = null,
    string? SearchTerm  = null
) : IRequest<List<LerChapterDto>>;

public sealed class GetLerCodesQueryHandler
    : IRequestHandler<GetLerCodesQuery, List<LerChapterDto>>
{
    private readonly IApplicationDbContext _context;

    public GetLerCodesQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<List<LerChapterDto>> Handle(
        GetLerCodesQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.LerCodes.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Chapter))
            query = query.Where(l => l.Chapter == request.Chapter);

        if (!string.IsNullOrWhiteSpace(request.SubChapter))
            query = query.Where(l => l.SubChapter == request.SubChapter);

        if (request.IsDangerous.HasValue)
            query = query.Where(l => l.IsDangerous == request.IsDangerous.Value);

        if (request.IsRAEE.HasValue)
            query = query.Where(l => l.IsRAEE == request.IsRAEE.Value);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.ToLower();
            query = query.Where(l =>
                l.Code.ToLower().Contains(term) ||
                l.Description.ToLower().Contains(term) ||
                (l.ChapterDescription != null && l.ChapterDescription.ToLower().Contains(term)));
        }

        var flat = await query
            .OrderBy(l => l.Chapter)
            .ThenBy(l => l.SubChapter)
            .ThenBy(l => l.Code)
            .Select(l => new LerCodeDto(
                l.Id, l.Code, l.CodeExtended, l.Description,
                l.Chapter, l.ChapterDescription,
                l.SubChapter, l.SubChapterDescription,
                l.IsDangerous, l.IsRAEE, l.DefaultProductCategory, l.IsActive))
            .ToListAsync(cancellationToken);

        // Construir jerarquía en memoria
        var chapters = flat
            .GroupBy(l => new { l.Chapter, l.ChapterDescription })
            .OrderBy(g => g.Key.Chapter)
            .Select(g => new LerChapterDto(
                g.Key.Chapter,
                g.Key.ChapterDescription,
                g.GroupBy(l => new { l.SubChapter, l.SubChapterDescription })
                 .OrderBy(sg => sg.Key.SubChapter)
                 .Select(sg => new LerSubChapterDto(
                     sg.Key.SubChapter,
                     sg.Key.SubChapterDescription,
                     sg.ToList()))
                 .ToList()))
            .ToList();

        return chapters;
    }
}

// ── GetLerCodesPagedQuery — listado plano paginado (para la página de gestión) ─

/// <summary>
/// Devuelve una lista plana paginada de códigos LER para la página de gestión.
/// Mantiene <see cref="GetLerCodesQuery"/> intacta para los selectores de formularios.
/// </summary>
public sealed record GetLerCodesPagedQuery(
    string? Chapter     = null,
    string? SubChapter  = null,
    bool?   IsDangerous = null,
    bool?   IsRAEE      = null,
    string? SearchTerm  = null,
    int     PageNumber  = 1,
    int     PageSize    = 15,
    string? SortBy      = null,
    bool    SortDescending = false
) : IRequest<PaginatedResult<LerCodeDto>>;

public sealed class GetLerCodesPagedQueryHandler
    : IRequestHandler<GetLerCodesPagedQuery, PaginatedResult<LerCodeDto>>
{
    private readonly IApplicationDbContext _context;

    public GetLerCodesPagedQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<PaginatedResult<LerCodeDto>> Handle(
        GetLerCodesPagedQuery request, CancellationToken ct)
    {
        var query = _context.LerCodes.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Chapter))
            query = query.Where(l => l.Chapter == request.Chapter);

        if (!string.IsNullOrWhiteSpace(request.SubChapter))
            query = query.Where(l => l.SubChapter == request.SubChapter);

        if (request.IsDangerous.HasValue)
            query = query.Where(l => l.IsDangerous == request.IsDangerous.Value);

        if (request.IsRAEE.HasValue)
            query = query.Where(l => l.IsRAEE == request.IsRAEE.Value);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.ToLower();
            query = query.Where(l =>
                l.Code.ToLower().Contains(term) ||
                l.Description.ToLower().Contains(term) ||
                (l.ChapterDescription != null && l.ChapterDescription.ToLower().Contains(term)));
        }

        var total = await query.CountAsync(ct);

        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        IQueryable<GreenTransit.Domain.Entities.LerCode> sorted = (request.SortBy?.ToLowerInvariant()) switch
        {
            "code"               => request.SortDescending ? query.OrderByDescending(l => l.Code)               : query.OrderBy(l => l.Code),
            "description"        => request.SortDescending ? query.OrderByDescending(l => l.Description)        : query.OrderBy(l => l.Description),
            "chapter"            => request.SortDescending ? query.OrderByDescending(l => l.Chapter)            : query.OrderBy(l => l.Chapter),
            "subchapter"         => request.SortDescending ? query.OrderByDescending(l => l.SubChapter)         : query.OrderBy(l => l.SubChapter),
            "isdangerous"        => request.SortDescending ? query.OrderByDescending(l => l.IsDangerous)        : query.OrderBy(l => l.IsDangerous),
            "israee"             => request.SortDescending ? query.OrderByDescending(l => l.IsRAEE)             : query.OrderBy(l => l.IsRAEE),
            _                    => query.OrderBy(l => l.Chapter).ThenBy(l => l.SubChapter).ThenBy(l => l.Code),
        };

        var items = await sorted
            .Skip((request.PageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new LerCodeDto(
                l.Id, l.Code, l.CodeExtended, l.Description,
                l.Chapter, l.ChapterDescription,
                l.SubChapter, l.SubChapterDescription,
                l.IsDangerous, l.IsRAEE, l.DefaultProductCategory, l.IsActive))
            .ToListAsync(ct);

        return PaginatedResult<LerCodeDto>.Create(items, total, request.PageNumber, pageSize);
    }
}
