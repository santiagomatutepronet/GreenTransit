using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.ProductDeclarations.Queries.Dictionaries;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public sealed record DicProductDeclarationCategoryDto(int Id, string Ref, string Description);
public sealed record DicProductDeclarationPeriodDto(int Id, string Ref, string Description);
public sealed record DicProductDeclarationProductDto(int Id, string Ref, string Description, int? CategoryId, string? CategoryDescription);
public sealed record DicProductDeclarationSourceDto(int Id, string Ref, string Description);
public sealed record DicProductDeclarationTypeDto(int Id, string Ref, string Description);
public sealed record DicProductDeclarationUseDto(int Id, string Ref, string Description);

// ── Queries ───────────────────────────────────────────────────────────────────

public sealed record GetDicProductDeclarationCategoriesQuery(string? Search = null)
    : IRequest<List<DicProductDeclarationCategoryDto>>;

public sealed class GetDicProductDeclarationCategoriesQueryHandler
    : IRequestHandler<GetDicProductDeclarationCategoriesQuery, List<DicProductDeclarationCategoryDto>>
{
    private readonly IApplicationDbContext _context;
    public GetDicProductDeclarationCategoriesQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<List<DicProductDeclarationCategoryDto>> Handle(
        GetDicProductDeclarationCategoriesQuery request, CancellationToken ct)
    {
        var q = _context.DicProductDeclarationCategories.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
            q = q.Where(x => x.Ref.Contains(request.Search)
                           || x.Description.Contains(request.Search));
        return await q.OrderBy(x => x.Ref)
            .Select(x => new DicProductDeclarationCategoryDto(x.Id, x.Ref, x.Description))
            .ToListAsync(ct);
    }
}

public sealed record GetDicProductDeclarationPeriodsQuery(string? Search = null)
    : IRequest<List<DicProductDeclarationPeriodDto>>;

public sealed class GetDicProductDeclarationPeriodsQueryHandler
    : IRequestHandler<GetDicProductDeclarationPeriodsQuery, List<DicProductDeclarationPeriodDto>>
{
    private readonly IApplicationDbContext _context;
    public GetDicProductDeclarationPeriodsQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<List<DicProductDeclarationPeriodDto>> Handle(
        GetDicProductDeclarationPeriodsQuery request, CancellationToken ct)
    {
        var q = _context.DicProductDeclarationPeriods.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
            q = q.Where(x => x.Ref.Contains(request.Search)
                           || x.Description.Contains(request.Search));
        return await q.OrderBy(x => x.Ref)
            .Select(x => new DicProductDeclarationPeriodDto(x.Id, x.Ref, x.Description))
            .ToListAsync(ct);
    }
}

public sealed record GetDicProductDeclarationProductsQuery(string? Search = null)
    : IRequest<List<DicProductDeclarationProductDto>>;

public sealed class GetDicProductDeclarationProductsQueryHandler
    : IRequestHandler<GetDicProductDeclarationProductsQuery, List<DicProductDeclarationProductDto>>
{
    private readonly IApplicationDbContext _context;
    public GetDicProductDeclarationProductsQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<List<DicProductDeclarationProductDto>> Handle(
        GetDicProductDeclarationProductsQuery request, CancellationToken ct)
    {
        var q = _context.DicProductDeclarationProducts
            .AsNoTracking()
            .Include(x => x.Category);
        IQueryable<DicProductDeclarationProduct> filtered = q;
        if (!string.IsNullOrWhiteSpace(request.Search))
            filtered = filtered.Where(x => x.Ref.Contains(request.Search)
                                        || x.Description.Contains(request.Search));
        return await filtered.OrderBy(x => x.Ref)
            .Select(x => new DicProductDeclarationProductDto(
                x.Id, x.Ref, x.Description, x.CategoryId,
                x.Category != null ? x.Category.Description : null))
            .ToListAsync(ct);
    }
}

public sealed record GetDicProductDeclarationSourcesQuery(string? Search = null)
    : IRequest<List<DicProductDeclarationSourceDto>>;

public sealed class GetDicProductDeclarationSourcesQueryHandler
    : IRequestHandler<GetDicProductDeclarationSourcesQuery, List<DicProductDeclarationSourceDto>>
{
    private readonly IApplicationDbContext _context;
    public GetDicProductDeclarationSourcesQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<List<DicProductDeclarationSourceDto>> Handle(
        GetDicProductDeclarationSourcesQuery request, CancellationToken ct)
    {
        var q = _context.DicProductDeclarationSources.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
            q = q.Where(x => x.Ref.Contains(request.Search)
                           || x.Description.Contains(request.Search));
        return await q.OrderBy(x => x.Ref)
            .Select(x => new DicProductDeclarationSourceDto(x.Id, x.Ref, x.Description))
            .ToListAsync(ct);
    }
}

public sealed record GetDicProductDeclarationTypesQuery(string? Search = null)
    : IRequest<List<DicProductDeclarationTypeDto>>;

public sealed class GetDicProductDeclarationTypesQueryHandler
    : IRequestHandler<GetDicProductDeclarationTypesQuery, List<DicProductDeclarationTypeDto>>
{
    private readonly IApplicationDbContext _context;
    public GetDicProductDeclarationTypesQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<List<DicProductDeclarationTypeDto>> Handle(
        GetDicProductDeclarationTypesQuery request, CancellationToken ct)
    {
        var q = _context.DicProductDeclarationTypes.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
            q = q.Where(x => x.Ref.Contains(request.Search)
                           || x.Description.Contains(request.Search));
        return await q.OrderBy(x => x.Ref)
            .Select(x => new DicProductDeclarationTypeDto(x.Id, x.Ref, x.Description))
            .ToListAsync(ct);
    }
}

public sealed record GetDicProductDeclarationUsesQuery(string? Search = null)
    : IRequest<List<DicProductDeclarationUseDto>>;

public sealed class GetDicProductDeclarationUsesQueryHandler
    : IRequestHandler<GetDicProductDeclarationUsesQuery, List<DicProductDeclarationUseDto>>
{
    private readonly IApplicationDbContext _context;
    public GetDicProductDeclarationUsesQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<List<DicProductDeclarationUseDto>> Handle(
        GetDicProductDeclarationUsesQuery request, CancellationToken ct)
    {
        var q = _context.DicProductDeclarationUses.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
            q = q.Where(x => x.Ref.Contains(request.Search)
                           || x.Description.Contains(request.Search));
        return await q.OrderBy(x => x.Ref)
            .Select(x => new DicProductDeclarationUseDto(x.Id, x.Ref, x.Description))
            .ToListAsync(ct);
    }
}
