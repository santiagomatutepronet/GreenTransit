using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.EmissionFactors.DTOs;
using GreenTransit.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.EmissionFactors.Queries;

// ── GetEmissionFactorSetsQuery ────────────────────────────────────────────────

/// <summary>Devuelve todos los sets de factores de emisión con su conteo de líneas.</summary>
public sealed record GetEmissionFactorSetsQuery : IRequest<IReadOnlyList<EmissionFactorSetDto>>;

public sealed class GetEmissionFactorSetsQueryHandler
    : IRequestHandler<GetEmissionFactorSetsQuery, IReadOnlyList<EmissionFactorSetDto>>
{
    private readonly IApplicationDbContext _context;

    public GetEmissionFactorSetsQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<IReadOnlyList<EmissionFactorSetDto>> Handle(
        GetEmissionFactorSetsQuery request, CancellationToken ct)
    {
        _context.IgnoreTenantFilter();
        try
        {
        return await _context.EmissionFactorSets
            .AsNoTracking()
            .OrderByDescending(s => s.ValidFrom)
            .Select(s => new EmissionFactorSetDto(
                s.Id,
                s.FactorSetName,
                s.Version,
                s.Status,
                s.ValidFrom,
                s.ValidTo,
                s.Publisher,
                s.Reference,
                s.Methodology,
                s.EmissionFactors.Count,
                s.CreatedAt))
            .ToListAsync(ct);
        }
        finally { _context.RestoreTenantFilter(); }
    }
}

// ── GetEmissionFactorSetByIdQuery

/// <summary>Devuelve el detalle completo de un set (cabecera + líneas).</summary>
public sealed record GetEmissionFactorSetByIdQuery(Guid SetId)
    : IRequest<EmissionFactorSetDetailDto?>;

public sealed class GetEmissionFactorSetByIdQueryHandler
    : IRequestHandler<GetEmissionFactorSetByIdQuery, EmissionFactorSetDetailDto?>
{
    private readonly IApplicationDbContext _context;

    public GetEmissionFactorSetByIdQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<EmissionFactorSetDetailDto?> Handle(
        GetEmissionFactorSetByIdQuery request, CancellationToken ct)
    {
        _context.IgnoreTenantFilter();
        EmissionFactorSet? set;
        try
        {
            set = await _context.EmissionFactorSets
                .AsNoTracking()
                .Include(s => s.EmissionFactors)
                .FirstOrDefaultAsync(s => s.Id == request.SetId, ct);
        }
        finally { _context.RestoreTenantFilter(); }

        if (set is null) return null;

        return new EmissionFactorSetDetailDto(
            set.Id,
            set.FactorSetName,
            set.Version,
            set.Status,
            set.ValidFrom,
            set.ValidTo,
            set.Publisher,
            set.Reference,
            set.Methodology,
            set.EmissionFactors
               .OrderBy(f => f.VehicleType).ThenBy(f => f.FuelType)
               .Select(f => new EmissionFactorDto(
                   f.Id, f.VehicleType, f.FuelType, f.EuroClass, f.Unit, f.Value))
               .ToList());
    }
}

// ── GetActiveEmissionFactorsQuery ─────────────────────────────────────────────

/// <summary>Devuelve el set activo con todas sus líneas para uso en cálculo de emisiones.</summary>
public sealed record GetActiveEmissionFactorsQuery
    : IRequest<EmissionFactorSetDetailDto?>;

public sealed class GetActiveEmissionFactorsQueryHandler
    : IRequestHandler<GetActiveEmissionFactorsQuery, EmissionFactorSetDetailDto?>
{
    private readonly IApplicationDbContext _context;

    public GetActiveEmissionFactorsQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<EmissionFactorSetDetailDto?> Handle(
        GetActiveEmissionFactorsQuery request, CancellationToken ct)
    {
        _context.IgnoreTenantFilter();
        EmissionFactorSet? set;
        try
        {
            set = await _context.EmissionFactorSets
                .AsNoTracking()
                .Include(s => s.EmissionFactors)
                .FirstOrDefaultAsync(s => s.Status == "Active", ct);
        }
        finally { _context.RestoreTenantFilter(); }

        if (set is null) return null;

        return new EmissionFactorSetDetailDto(
            set.Id,
            set.FactorSetName,
            set.Version,
            set.Status,
            set.ValidFrom,
            set.ValidTo,
            set.Publisher,
            set.Reference,
            set.Methodology,
            set.EmissionFactors
               .OrderBy(f => f.VehicleType).ThenBy(f => f.FuelType)
               .Select(f => new EmissionFactorDto(
                   f.Id, f.VehicleType, f.FuelType, f.EuroClass, f.Unit, f.Value))
               .ToList());
    }
}
