using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Settlements.DTOs;
using GreenTransit.Domain.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.Settlements.Queries;

// ── Listado paginado ──────────────────────────────────────────────────────────

/// <summary>Devuelve liquidaciones filtradas y paginadas.</summary>
public sealed record GetSettlementsQuery(
    string?  Status      = null,
    Guid?    AgreementId = null,
    int?     Year        = null,
    int?     Month       = null,
    Guid?    IdScrap     = null,
    int      Page        = 1,
    int      PageSize    = 20
) : IRequest<SettlementsPageDto>;

public sealed record SettlementsPageDto(
    IReadOnlyList<SettlementSummaryDto> Items,
    int TotalCount
);

public sealed class GetSettlementsQueryHandler
    : IRequestHandler<GetSettlementsQuery, SettlementsPageDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetSettlementsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<SettlementsPageDto> Handle(GetSettlementsQuery request, CancellationToken ct)
    {
        var q = _context.Settlements
            .AsNoTracking()
            .Include(s => s.Agreement)
            .Include(s => s.Scrap)
            .Include(s => s.PublicEntity)
            .AsQueryable();

        if (request.Status is not null)
            q = q.Where(s => s.Status == request.Status);
        if (request.AgreementId.HasValue)
            q = q.Where(s => s.AgreementId == request.AgreementId.Value);
        if (request.Year.HasValue)
            q = q.Where(s => s.Year == request.Year.Value);
        if (request.Month.HasValue)
            q = q.Where(s => s.Month == request.Month.Value);

        // Filtro SCRAP: solo sus liquidaciones
        if (_currentUser.IsInProfile(ProfileConstants.Scrap) && _currentUser.LinkedEntityId.HasValue)
            q = q.Where(s => s.IdScrap == _currentUser.LinkedEntityId.Value);
        else if (request.IdScrap.HasValue)
            q = q.Where(s => s.IdScrap == request.IdScrap.Value);

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(s => s.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(s => new SettlementSummaryDto(
                s.Id,
                s.SettlementNumber,
                s.Status,
                s.AgreementId,
                s.Agreement.AgreementNumber,
                s.Year,
                s.Month,
                s.IdScrap,
                s.Scrap != null ? s.Scrap.Name : null,
                s.IdPublicEntity,
                s.PublicEntity != null ? s.PublicEntity.Name : null,
                s.BaseAmount,
                s.AdjustmentsAmount,
                s.TaxAmount,
                s.TotalAmount,
                s.Currency,
                s.Validator,
                s.ValidatedAt,
                s.CreatedAt))
            .ToListAsync(ct);

        return new SettlementsPageDto(items, total);
    }
}

// ── Detalle por Id ────────────────────────────────────────────────────────────

/// <summary>Devuelve el detalle completo de una liquidación con sus líneas.</summary>
public sealed record GetSettlementByIdQuery(Guid Id) : IRequest<SettlementDetailDto?>;

public sealed class GetSettlementByIdQueryHandler
    : IRequestHandler<GetSettlementByIdQuery, SettlementDetailDto?>
{
    private readonly IApplicationDbContext _context;

    public GetSettlementByIdQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<SettlementDetailDto?> Handle(GetSettlementByIdQuery request, CancellationToken ct)
    {
        var s = await _context.Settlements
            .AsNoTracking()
            .Include(s => s.Agreement)
            .Include(s => s.Scrap)
            .Include(s => s.PublicEntity)
            .Include(s => s.SettlementLines)
                .ThenInclude(l => l.LerCode)
            .FirstOrDefaultAsync(s => s.Id == request.Id, ct);

        if (s is null) return null;

        var lines = s.SettlementLines
            .Select(l => new SettlementLineDto(
                l.Id,
                l.ProductCategory,
                l.IdLERCode,
                l.LerCode?.Code,
                l.LerCode?.Description,
                l.WeightKg,
                l.PricePerKg,
                l.Amount,
                l.EvidenceType,
                l.SourceIdsJson))
            .ToList();

        return new SettlementDetailDto(
            s.Id,
            s.SettlementNumber,
            s.Status,
            s.AgreementId,
            s.Agreement.AgreementNumber,
            s.Year,
            s.Month,
            s.IdScrap,
            s.Scrap?.Name,
            s.IdPublicEntity,
            s.PublicEntity?.Name,
            s.BaseAmount,
            s.AdjustmentsAmount,
            s.TaxAmount,
            s.TotalAmount,
            s.Currency,
            s.EvidenceRefsJson,
            s.Validator,
            s.ValidationStatus,
            s.ValidatedAt,
            s.ValidationRef,
            s.Version,
            s.CreatedAt,
            s.UpdatedAt,
            lines);
    }
}

// ── Compatibilidad con AgreementDetail ───────────────────────────────────────

/// <summary>Devuelve el resumen de liquidaciones vinculadas a un acuerdo.</summary>
public sealed record GetSettlementsByAgreementQuery(Guid AgreementId)
    : IRequest<List<SettlementSummaryDto>>;

public sealed class GetSettlementsByAgreementQueryHandler
    : IRequestHandler<GetSettlementsByAgreementQuery, List<SettlementSummaryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetSettlementsByAgreementQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<List<SettlementSummaryDto>> Handle(
        GetSettlementsByAgreementQuery request, CancellationToken ct)
        => await _context.Settlements
            .AsNoTracking()
            .Include(s => s.Agreement)
            .Include(s => s.Scrap)
            .Include(s => s.PublicEntity)
            .Where(s => s.AgreementId == request.AgreementId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new SettlementSummaryDto(
                s.Id,
                s.SettlementNumber,
                s.Status,
                s.AgreementId,
                s.Agreement.AgreementNumber,
                s.Year,
                s.Month,
                s.IdScrap,
                s.Scrap != null ? s.Scrap.Name : null,
                s.IdPublicEntity,
                s.PublicEntity != null ? s.PublicEntity.Name : null,
                s.BaseAmount,
                s.AdjustmentsAmount,
                s.TaxAmount,
                s.TotalAmount,
                s.Currency,
                s.Validator,
                s.ValidatedAt,
                s.CreatedAt))
            .ToListAsync(ct);
}
