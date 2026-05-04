using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.PlantEnergies.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;
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
    int?    Month           = null
) : IRequest<IReadOnlyList<PlantEnergyDto>>;

public sealed class GetPlantEnergiesQueryHandler
    : IRequestHandler<GetPlantEnergiesQuery, IReadOnlyList<PlantEnergyDto>>
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

    public async Task<IReadOnlyList<PlantEnergyDto>> Handle(
        GetPlantEnergiesQuery request, CancellationToken ct)
    {
        var ownerId = _currentUser.OwnerId;

        var query = _context.PlantEnergies
            .AsNoTracking()
            .Where(e => e.OwnerId == ownerId);

        if (!string.IsNullOrWhiteSpace(request.PlantCenterCode))
            query = query.Where(e => e.PlantCenterCode == request.PlantCenterCode);

        if (request.Year.HasValue)
            query = query.Where(e => e.Year == request.Year.Value);

        if (request.Month.HasValue)
            query = query.Where(e => e.Month == request.Month.Value);

        return await query
            .OrderBy(e => e.Year)
            .ThenBy(e => e.Month)
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
