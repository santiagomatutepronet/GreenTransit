using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Common.Models;
using GreenTransit.Application.Features.ProductDeclarations.DTOs;
using GreenTransit.Domain.Authorization;
using GreenTransit.Domain.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.ProductDeclarations.Queries;

/// <summary>Devuelve el listado paginado de declaraciones de producción con filtros por perfil.</summary>
public sealed record GetProductDeclarationsQuery(
    int?      Year        = null,
    int?      Period      = null,
    string?   State       = null,
    Guid?     IdProducer  = null,
    string?   Type        = null,
    DateTime? DateFrom    = null,
    DateTime? DateTo      = null,
    int       PageNumber  = 1,
    int       PageSize    = 15
) : IRequest<PaginatedResult<ProductDeclarationDto>>;

public sealed class GetProductDeclarationsQueryHandler
    : IRequestHandler<GetProductDeclarationsQuery, PaginatedResult<ProductDeclarationDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetProductDeclarationsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<ProductDeclarationDto>> Handle(
        GetProductDeclarationsQuery request, CancellationToken ct)
    {
        var q = _context.ProductDeclarations
            .AsNoTracking()
            .Include(pd => pd.Producer)
            .AsQueryable();

        // ── Filtro por perfil ─────────────────────────────────────────────────
        if (_currentUser.IsInProfile(ProfileConstants.Producer))
        {
            // El productor solo ve sus propias declaraciones
            var linkedId = _currentUser.LinkedEntityId;
            q = q.Where(pd => pd.IdProducer == linkedId);
        }
        else if (_currentUser.IsInProfile(ProfileConstants.Scrap))
        {
            // SCRAP: ve declaraciones de productores adheridos (derivado de WasteMoves)
            var linkedId = _currentUser.LinkedEntityId;
            if (linkedId.HasValue)
            {
                var ownerId = _currentUser.OwnerId;
                var producerIds = _context.WasteMoves
                    .Where(wm => (wm.IdScrap == linkedId.Value || wm.IdScrap2 == linkedId.Value)
                              && (ownerId == Guid.Empty || wm.OwnerId == ownerId)
                              && wm.ServiceOrderId != null)
                    .Join(_context.ServiceOrders,
                        wm => wm.ServiceOrderId, so => so.Id,
                        (wm, so) => so.IdIssuedBy)
                    .Distinct();
                q = q.Where(pd => pd.IdProducer != null && producerIds.Contains(pd.IdProducer.Value));
            }
        }
        else if (_currentUser.IsInProfile(ProfileConstants.Coordinator))
        {
            // COORDINATOR: declaraciones de productores de SCRAPs coordinados
            var linkedId = _currentUser.LinkedEntityId;
            if (linkedId.HasValue)
            {
                var ownerId = _currentUser.OwnerId;
                var scrapIds = _context.Agreements
                    .Where(a => a.IdCoordinator == linkedId.Value && (ownerId == Guid.Empty || a.OwnerId == ownerId))
                    .Select(a => a.IdScrap).Distinct();
                var producerIds = _context.WasteMoves
                    .Where(wm => scrapIds.Contains(wm.IdScrap)
                              && (ownerId == Guid.Empty || wm.OwnerId == ownerId)
                              && wm.ServiceOrderId != null)
                    .Join(_context.ServiceOrders,
                        wm => wm.ServiceOrderId, so => so.Id,
                        (wm, so) => so.IdIssuedBy)
                    .Distinct();
                q = q.Where(pd => pd.IdProducer != null && producerIds.Contains(pd.IdProducer.Value));
            }
        }
        // DISPATCH_OFFICE / ADMIN: sin restricción adicional

        // ── Filtros opcionales ────────────────────────────────────────────────
        if (request.Year.HasValue)
            q = q.Where(pd => pd.Year == request.Year);

        if (request.Period.HasValue)
            q = q.Where(pd => pd.Period == request.Period);

        if (!string.IsNullOrWhiteSpace(request.State))
            q = q.Where(pd => pd.State == request.State);

        if (request.IdProducer.HasValue && !_currentUser.IsInProfile(ProfileConstants.Producer))
            q = q.Where(pd => pd.IdProducer == request.IdProducer);

        if (!string.IsNullOrWhiteSpace(request.Type))
            q = q.Where(pd => pd.Type == request.Type);

        if (request.DateFrom.HasValue)
            q = q.Where(pd => pd.DateCreate >= request.DateFrom);

        if (request.DateTo.HasValue)
            q = q.Where(pd => pd.DateCreate <= request.DateTo);

        var total = await q.CountAsync(ct);

        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var items = await q
            .OrderByDescending(pd => pd.DateCreateSys)
            .Skip((request.PageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(pd => new ProductDeclarationDto(
                pd.Id,
                pd.OwnerId,
                pd.Period,
                pd.Year,
                pd.Month,
                pd.Currency,
                pd.State,
                pd.DateCreate,
                pd.DateEmit,
                pd.Reference,
                pd.IdProducer,
                pd.Producer != null ? pd.Producer.Name : null,
                pd.Amount,
                pd.Type,
                pd.DateCreateSys,
                pd.DateModifiedSys))
            .ToListAsync(ct);

        return PaginatedResult<ProductDeclarationDto>.Create(
            items, total, request.PageNumber, pageSize);
    }
}
