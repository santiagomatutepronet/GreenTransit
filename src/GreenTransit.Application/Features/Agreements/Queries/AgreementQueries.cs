using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Common.Models;
using GreenTransit.Application.Features.Agreements.DTOs;
using GreenTransit.Domain.Authorization;
using GreenTransit.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.Agreements.Queries;

// ── GetAgreementsQuery ────────────────────────────────────────────────────────

/// <summary>Listado paginado de acuerdos con filtros. Filtra por OwnerId.</summary>
public sealed record GetAgreementsQuery(
    string? Status          = null,
    Guid?   IdScrap         = null,
    Guid?   IdPublicEntity  = null,
    int?    Year            = null,
    string? SearchTerm      = null,
    int     PageNumber      = 1,
    int     PageSize        = 20
) : IRequest<PaginatedResult<AgreementDto>>;

public sealed class GetAgreementsQueryHandler
    : IRequestHandler<GetAgreementsQuery, PaginatedResult<AgreementDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetAgreementsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<AgreementDto>> Handle(
        GetAgreementsQuery request, CancellationToken ct)
    {
        var q = _context.Agreements
            .AsNoTracking()
            .Include(a => a.Scrap)
            .Include(a => a.PublicEntity)
            .Include(a => a.Coordinator)
            .AsQueryable();

        // ── Filtro por perfil ─────────────────────────────────────────────────
        if (_currentUser.IsInProfile(ProfileConstants.Scrap))
        {
            var linkedId = _currentUser.LinkedEntityId;
            q = q.Where(a => a.IdScrap == linkedId);
        }
        else if (_currentUser.IsInProfile(ProfileConstants.PublicEnt))
        {
            var linkedId = _currentUser.LinkedEntityId;
            q = q.Where(a => a.IdPublicEntity == linkedId);
        }
        else if (_currentUser.IsInProfile(ProfileConstants.Coordinator))
        {
            var linkedId = _currentUser.LinkedEntityId;
            q = q.Where(a => a.IdCoordinator == linkedId);
        }

        // ── Filtros de búsqueda ───────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(request.Status))
            q = q.Where(a => a.Status == request.Status);

        if (request.IdScrap.HasValue)
            q = q.Where(a => a.IdScrap == request.IdScrap.Value);

        if (request.IdPublicEntity.HasValue)
            q = q.Where(a => a.IdPublicEntity == request.IdPublicEntity.Value);

        if (request.Year.HasValue)
            q = q.Where(a => a.EffectiveFrom.Year == request.Year.Value
                           || (a.EffectiveTo.HasValue && a.EffectiveTo.Value.Year == request.Year.Value));

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            q = q.Where(a => a.AgreementNumber.Contains(request.SearchTerm));

        var totalCount = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(a => a.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new AgreementDto(
                a.Id,
                a.OwnerId,
                a.AgreementNumber,
                a.Status,
                a.EffectiveFrom,
                a.EffectiveTo,
                a.IdScrap,
                a.Scrap != null ? a.Scrap.Name : null,
                a.IdPublicEntity,
                a.PublicEntity != null ? a.PublicEntity.Name : null,
                a.IdCoordinator,
                a.Coordinator != null ? a.Coordinator.Name : null,
                a.WasteStream,
                a.SubStream,
                a.AutonomousCommunity,
                a.TariffModelType,
                a.Currency,
                a.Version,
                a.CreatedAt,
                a.UpdatedAt))
            .ToListAsync(ct);

        return PaginatedResult<AgreementDto>.Create(items, totalCount, request.PageNumber, request.PageSize);
    }
}

// ── GetAgreementByIdQuery ─────────────────────────────────────────────────────

/// <summary>Detalle completo de un acuerdo, incluyendo documentos adjuntos.</summary>
public sealed record GetAgreementByIdQuery(Guid Id) : IRequest<AgreementDetailDto?>;

public sealed class GetAgreementByIdQueryHandler
    : IRequestHandler<GetAgreementByIdQuery, AgreementDetailDto?>
{
    private readonly IApplicationDbContext _context;

    public GetAgreementByIdQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<AgreementDetailDto?> Handle(
        GetAgreementByIdQuery request, CancellationToken ct)
    {
        var a = await _context.Agreements
            .AsNoTracking()
            .Include(x => x.Scrap)
            .Include(x => x.PublicEntity)
            .Include(x => x.Coordinator)
            .Include(x => x.AgreementDocuments)
            .FirstOrDefaultAsync(x => x.Id == request.Id, ct);

        if (a is null) return null;

        var docs = a.AgreementDocuments
            .Select(d => new AgreementDocumentDto(
                d.Id, d.AgreementId, d.DocumentType, d.DocumentId,
                d.DocumentHash, d.SignedAt, d.SignatureProvider))
            .ToList();

        return new AgreementDetailDto(
            a.Id, a.OwnerId, a.AgreementNumber, a.Status,
            a.EffectiveFrom, a.EffectiveTo,
            a.IdScrap,        a.Scrap?.Name,
            a.IdPublicEntity, a.PublicEntity?.Name,
            a.IdCoordinator,  a.Coordinator?.Name,
            a.WasteStream, a.SubStream, a.AutonomousCommunity,
            a.ProvinceCode, a.MunicipalityCode, a.CoveredMethodsJson,
            a.TariffModelType, a.Currency,
            a.TariffRulesJson, a.MinimumsJson, a.ObligationsJson,
            a.Hash, a.Version, a.CreatedAt, a.UpdatedAt,
            docs);
    }
}

// ── GetExpiringAgreementsQuery ────────────────────────────────────────────────

/// <summary>Acuerdos en estado Active cuyo EffectiveTo cae en los próximos daysThreshold días.</summary>
public sealed record GetExpiringAgreementsQuery(int DaysThreshold = 30)
    : IRequest<IReadOnlyList<ExpiringAgreementDto>>;

public sealed class GetExpiringAgreementsQueryHandler
    : IRequestHandler<GetExpiringAgreementsQuery, IReadOnlyList<ExpiringAgreementDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetExpiringAgreementsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ExpiringAgreementDto>> Handle(
        GetExpiringAgreementsQuery request, CancellationToken ct)
    {
        var today    = DateTime.UtcNow.Date;
        var horizon  = today.AddDays(request.DaysThreshold);

        var q = _context.Agreements
            .AsNoTracking()
            .Include(a => a.Scrap)
            .Include(a => a.PublicEntity)
            .Where(a => a.Status == Agreement.Statuses.Active
                     && a.EffectiveTo.HasValue
                     && a.EffectiveTo.Value.Date >= today
                     && a.EffectiveTo.Value.Date <= horizon);

        if (_currentUser.IsInProfile(ProfileConstants.Scrap))
        {
            var linkedId = _currentUser.LinkedEntityId;
            q = q.Where(a => a.IdScrap == linkedId);
        }

        return await q
            .OrderBy(a => a.EffectiveTo)
            .Select(a => new ExpiringAgreementDto(
                a.Id,
                a.AgreementNumber,
                a.Scrap != null ? a.Scrap.Name : null,
                a.PublicEntity != null ? a.PublicEntity.Name : null,
                a.EffectiveTo!.Value,
                (int)(a.EffectiveTo.Value.Date - today).TotalDays))
            .ToListAsync(ct);
    }
}
