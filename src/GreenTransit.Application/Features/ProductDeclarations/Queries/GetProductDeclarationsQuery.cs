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
            // El SCRAP ve las declaraciones de productores adheridos a sus acuerdos
            var linkedId = _currentUser.LinkedEntityId;
            if (linkedId.HasValue)
            {
                var producerIds = _context.Agreements
                    .Where(a => a.IdScrap == linkedId.Value)
                    .Select(a => a.IdPublicEntity);
                q = q.Where(pd => pd.IdProducer != null && producerIds.Contains(pd.IdProducer.Value));
            }
        }
        // ADMIN: sin restricción adicional

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
