using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.ProductDeclarations.DTOs;
using GreenTransit.Domain.Authorization;
using GreenTransit.Domain.Constants;
using GreenTransit.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.ProductDeclarations.Queries;

/// <summary>Devuelve los KPIs del panel de declaraciones de producción.</summary>
public sealed record GetProductDeclarationDashboardQuery(
    int?  Year       = null,
    int?  Period     = null,
    Guid? IdProducer = null
) : IRequest<ProductDeclarationDashboardDto>;

public sealed class GetProductDeclarationDashboardQueryHandler
    : IRequestHandler<GetProductDeclarationDashboardQuery, ProductDeclarationDashboardDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetProductDeclarationDashboardQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<ProductDeclarationDashboardDto> Handle(
        GetProductDeclarationDashboardQuery request, CancellationToken ct)
    {
        var q = _context.ProductDeclarations.AsNoTracking().AsQueryable();

        // ── Filtro por perfil ─────────────────────────────────────────────────
        if (_currentUser.IsInProfile(ProfileConstants.Producer))
        {
            var linkedId = _currentUser.LinkedEntityId;
            q = q.Where(pd => pd.IdProducer == linkedId);
        }
        else if (_currentUser.IsInProfile(ProfileConstants.Scrap))
        {
            var linkedId = _currentUser.LinkedEntityId;
            if (linkedId.HasValue)
            {
                var producerIds = _context.Agreements
                    .Where(a => a.IdScrap == linkedId.Value)
                    .Select(a => a.IdPublicEntity);
                q = q.Where(pd => pd.IdProducer != null && producerIds.Contains(pd.IdProducer.Value));
            }
        }

        // ── Filtros opcionales ────────────────────────────────────────────────
        if (request.Year.HasValue)
            q = q.Where(pd => pd.Year == request.Year);

        if (request.Period.HasValue)
            q = q.Where(pd => pd.Period == request.Period);

        if (request.IdProducer.HasValue && !_currentUser.IsInProfile(ProfileConstants.Producer))
            q = q.Where(pd => pd.IdProducer == request.IdProducer);

        // ── Agrupación por estado ─────────────────────────────────────────────
        var byState = await q
            .GroupBy(pd => pd.State ?? "Sin estado")
            .Select(g => new { State = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        // ── Totales ───────────────────────────────────────────────────────────
        var totalAmount = await q.SumAsync(pd => pd.Amount ?? 0m, ct);

        // ── Σ Quantity por Residue (top 10) ───────────────────────────────────
        var declarationIds = await q.Select(pd => pd.Id).ToListAsync(ct);

        var productFlat = await _context.Products
            .AsNoTracking()
            .Where(p => declarationIds.Contains(p.IdProductDeclaration))
            .Include(p => p.Residue)
            .Select(p => new { p.IdResidue, ResidueName = p.Residue != null ? p.Residue.Name : null, p.Quantity })
            .ToListAsync(ct);

        var totalQuantity = productFlat.Sum(p => p.Quantity ?? 0m);

        var topProducts = productFlat
            .GroupBy(p => new { p.IdResidue, p.ResidueName })
            .Select(g => new TopProductDto(g.Key.IdResidue, g.Key.ResidueName, g.Sum(p => p.Quantity ?? 0m)))
            .OrderByDescending(t => t.TotalQuantity)
            .Take(10)
            .ToList();

        // ── Productores sin declaración en el periodo ─────────────────────────
        var producersWithDeclaration = await q
            .Where(pd => pd.IdProducer.HasValue)
            .Select(pd => pd.IdProducer!.Value)
            .Distinct()
            .ToListAsync(ct);

        var totalProducers = await _context.BusinessEntities
            .AsNoTracking()
            .CountAsync(e => e.EntityRole == EntityRoles.Producer, ct);

        var producersWithoutDeclaration = Math.Max(0, totalProducers - producersWithDeclaration.Count);

        return new ProductDeclarationDashboardDto(
            byState.ToDictionary(x => x.State, x => x.Count),
            totalAmount,
            totalQuantity,
            topProducts,
            producersWithoutDeclaration);
    }
}
