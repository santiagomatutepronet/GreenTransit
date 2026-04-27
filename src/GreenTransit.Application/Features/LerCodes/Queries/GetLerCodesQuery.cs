using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.LerCodes.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

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
