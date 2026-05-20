using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Common.Models;
using GreenTransit.Application.Features.PlantEnergies.DTOs;
using GreenTransit.Domain.Authorization;
using MediatR;
using Microsoft.Extensions.Options;

namespace GreenTransit.Application.Features.PlantEnergies.Queries;

// ── Opciones de configuración ─────────────────────────────────────────────────

public sealed class PlantEnergyOptions
{
    public const string Section = "PlantEnergy";
    /// <summary>Factor de emisión de la red eléctrica en kgCO₂e/kWh. Default España 2024.</summary>
    public decimal GridEmissionFactor { get; set; } = 0.27m;
}

// ── GetPlantEnergiesQuery ─────────────────────────────────────────────────────

/// <summary>Devuelve registros de consumo energético filtrados por planta, año y mes.
/// Filtra siempre por OwnerId del usuario autenticado.</summary>
public sealed record GetPlantEnergiesQuery(
    string? PlantCenterCode = null,
    int?    Year            = null,
    int?    Month           = null,
    int     PageNumber      = 1,
    int     PageSize        = 15
) : IRequest<PaginatedResult<PlantEnergyDto>>;

public sealed class GetPlantEnergiesQueryHandler
    : IRequestHandler<GetPlantEnergiesQuery, PaginatedResult<PlantEnergyDto>>
{
    private readonly IApplicationDbContext   _context;
    private readonly ICurrentUserService     _currentUser;

    public GetPlantEnergiesQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<PlantEnergyDto>> Handle(
        GetPlantEnergiesQuery request, CancellationToken ct)
    {
        var ownerId        = _currentUser.OwnerId;
        var linkedEntityId = _currentUser.LinkedEntityId;

        var query = _context.PlantEnergies
            .AsNoTracking()
            .Where(e => ownerId == Guid.Empty || e.OwnerId == ownerId);

        // ── Filtro por perfil ─────────────────────────────────────────────────
        if (_currentUser.IsInProfile(ProfileConstants.PlantOp))
        {
            // PLANT_OP: solo datos de su planta
            var centerCode = await _context.BusinessEntities
                .Where(e => e.Id == linkedEntityId)
                .Select(e => e.CenterCode)
                .FirstOrDefaultAsync(ct);
            if (centerCode != null)
                query = query.Where(e => e.PlantCenterCode == centerCode);
        }
        else if (_currentUser.IsInProfile(ProfileConstants.Scrap))
        {
            // SCRAP: datos de plantas que procesan sus residuos
            var plantCenterCodes = await _context.WasteMoves
                .Where(wm => (wm.IdScrap == linkedEntityId || wm.IdScrap2 == linkedEntityId)
                          && (ownerId == Guid.Empty || wm.OwnerId == ownerId))
                .Join(_context.BusinessEntities, wm => wm.IdDestination, e => e.Id,
                    (wm, e) => e.CenterCode)
                .Where(cc => cc != null)
                .Distinct().ToListAsync(ct);
            query = query.Where(e => plantCenterCodes.Contains(e.PlantCenterCode));
        }
        else if (_currentUser.IsInProfile(ProfileConstants.Coordinator))
        {
            // COORDINATOR: energía de plantas de traslados de SCRAPs coordinados
            var scrapIds = (await _context.Agreements
                .Where(a => a.IdCoordinator == linkedEntityId && (ownerId == Guid.Empty || a.OwnerId == ownerId))
                .Select(a => a.IdScrap).Distinct().ToListAsync(ct))
                .Where(id => id.HasValue).Select(id => id!.Value).ToList();
            var plantCenterCodes = await _context.WasteMoves
                .Where(wm => wm.IdScrap.HasValue && scrapIds.Contains(wm.IdScrap.Value) && (ownerId == Guid.Empty || wm.OwnerId == ownerId))
                .Join(_context.BusinessEntities, wm => wm.IdDestination, e => e.Id,
                    (wm, e) => e.CenterCode)
                .Where(cc => cc != null)
                .Distinct().ToListAsync(ct);
            query = query.Where(e => plantCenterCodes.Contains(e.PlantCenterCode));
        }
        // DISPATCH_OFFICE / ADMIN / PUBLIC_ENT: sin filtro adicional (todo el tenant)

        if (!string.IsNullOrWhiteSpace(request.PlantCenterCode))
            query = query.Where(e => e.PlantCenterCode == request.PlantCenterCode);

        if (request.Year.HasValue)
            query = query.Where(e => e.Year == request.Year.Value);

        if (request.Month.HasValue)
            query = query.Where(e => e.Month == request.Month.Value);

        var total = await query.CountAsync(ct);

        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var items = await query
            .OrderByDescending(e => e.Year)
            .ThenByDescending(e => e.Month)
            .ThenBy(e => e.PlantCenterCode)
            .Skip((request.PageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new PlantEnergyDto(
                e.Id,
                e.PlantName,
                e.PlantCenterCode,
                e.Year,
                e.Month,
                e.KwhTotal,
                e.Source,
                e.GridMixRef,
                e.AllocationMethod,
                e.Notes,
                e.CreatedAt,
                e.UpdatedAt))
            .ToListAsync(ct);

        return PaginatedResult<PlantEnergyDto>.Create(items, total, request.PageNumber, pageSize);
    }
}

// ── GetPlantEnergySummaryQuery ────────────────────────────────────────────────

/// <summary>Resumen anual de consumo de una planta con los 12 valores mensuales
/// y el cálculo de CO₂e usando el factor de red eléctrica de appsettings.</summary>
public sealed record GetPlantEnergySummaryQuery(
    string PlantCenterCode,
    int    Year
) : IRequest<PlantEnergySummaryDto>;

public sealed class GetPlantEnergySummaryQueryHandler
    : IRequestHandler<GetPlantEnergySummaryQuery, PlantEnergySummaryDto>
{
    private readonly IApplicationDbContext   _context;
    private readonly ICurrentUserService     _currentUser;
    private readonly decimal                 _gridEmissionFactor;

    public GetPlantEnergySummaryQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser,
        IOptions<PlantEnergyOptions> options)
    {
        _context            = context;
        _currentUser        = currentUser;
        _gridEmissionFactor = options.Value.GridEmissionFactor;
    }

    public async Task<PlantEnergySummaryDto> Handle(
        GetPlantEnergySummaryQuery request, CancellationToken ct)
    {
        var ownerId = _currentUser.OwnerId;

        var records = await _context.PlantEnergies
            .AsNoTracking()
            .Where(e => e.OwnerId            == ownerId
                     && e.PlantCenterCode    == request.PlantCenterCode
                     && e.Year               == request.Year
                     && e.Month              != null)
            .ToListAsync(ct);

        var monthly = new decimal?[12];
        foreach (var r in records)
            if (r.Month is >= 1 and <= 12)
                monthly[r.Month.Value - 1] = r.KwhTotal;

        var totalKwh  = monthly.Where(v => v.HasValue).Sum(v => v!.Value);
        var totalCO2e = totalKwh * _gridEmissionFactor;

        return new PlantEnergySummaryDto(
            request.PlantCenterCode,
            request.Year,
            monthly,
            totalKwh,
            totalCO2e,
            _gridEmissionFactor);
    }
}
