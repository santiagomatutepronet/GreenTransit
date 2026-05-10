using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Security.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.Security.Queries;

public record GetPagePermissionMatrixQuery : IRequest<PagePermissionMatrixDto>
{
    public string? ModuleFilter { get; init; }
    public string? SearchTerm { get; init; }
    public bool IncludeInactive { get; init; } = false;
}

public sealed class GetPagePermissionMatrixQueryHandler
    : IRequestHandler<GetPagePermissionMatrixQuery, PagePermissionMatrixDto>
{
    private readonly IApplicationDbContext _context;

    public GetPagePermissionMatrixQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagePermissionMatrixDto> Handle(
        GetPagePermissionMatrixQuery request, CancellationToken ct)
    {
        // 1. Perfiles (catálogo del sistema, sin filtro de tenant)
        var profiles = await _context.UserProfiles
            .IgnoreQueryFilters()
            .OrderBy(p => p.Reference)
            .Select(p => new ProfileSummaryDto
            {
                Id          = p.Id,
                Reference   = p.Reference,
                Description = p.Description ?? p.Reference
            })
            .ToListAsync(ct);

        // 2. PageDefinitions con permisos
        var query = _context.PageDefinitions
            .Include(d => d.Permissions)
            .AsNoTracking()
            .AsQueryable();

        if (!request.IncludeInactive)
            query = query.Where(d => d.IsActive);

        if (!string.IsNullOrWhiteSpace(request.ModuleFilter))
            query = query.Where(d => d.ModuleName == request.ModuleFilter);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(d =>
                d.Route.ToLower().Contains(term) ||
                d.PageName.ToLower().Contains(term));
        }

        var pages = await query
            .OrderBy(d => d.ModuleName)
            .ThenBy(d => d.SortOrder)
            .ThenBy(d => d.Route)
            .ToListAsync(ct);

        var profileIds = profiles.Select(p => p.Id).ToHashSet();

        // 3. Agrupar por módulo y construir la matriz
        var modules = pages
            .GroupBy(d => d.ModuleName)
            .Select(g => new ModuleGroupDto
            {
                ModuleName = g.Key,
                Pages = g.Select(d => new PageWithPermissionsDto
                {
                    Id        = d.ID,
                    Route     = d.Route,
                    PageName  = d.PageName,
                    IsActive  = d.IsActive,
                    SortOrder = d.SortOrder,
                    PermissionsByProfile = profileIds.ToDictionary(
                        pid => pid,
                        pid => d.Permissions
                            .FirstOrDefault(p => p.IdProfile == pid)?.AccessLevel)
                }).ToList()
            })
            .ToList();

        return new PagePermissionMatrixDto
        {
            Profiles = profiles,
            Modules  = modules
        };
    }
}
