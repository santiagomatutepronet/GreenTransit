using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Geography.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GreenTransit.Application.Features.Geography.Queries;

// ── GetAllProvincesQuery ──────────────────────────────────────────────────────

/// <summary>Devuelve todas las provincias ordenadas por nombre. Catálogo compartido.</summary>
public sealed record GetAllProvincesQuery : IRequest<IEnumerable<ProvinceDto>>;

public sealed class GetAllProvincesQueryHandler
    : IRequestHandler<GetAllProvincesQuery, IEnumerable<ProvinceDto>>
{
    private const string CacheKey = "geo:provinces:all";
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    private readonly IApplicationDbContext _context;
    private readonly IMemoryCache _cache;

    public GetAllProvincesQueryHandler(IApplicationDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache   = cache;
    }

    public async Task<IEnumerable<ProvinceDto>> Handle(
        GetAllProvincesQuery request, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(CacheKey, out IEnumerable<ProvinceDto>? cached) && cached is not null)
            return cached;

        var result = await _context.Provinces
            .AsNoTracking()
            .OrderBy(p => p.Name ?? p.Ref)
            .Select(p => new ProvinceDto(p.Id, p.Name ?? p.Ref, p.Code))
            .ToListAsync(cancellationToken);

        _cache.Set(CacheKey, (IEnumerable<ProvinceDto>)result,
            new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl });

        return result;
    }
}

// ── GetCountriesQuery ─────────────────────────────────────────────────────────

/// <summary>Devuelve todos los países. Catálogo compartido sin filtro OwnerId.</summary>
public sealed record GetCountriesQuery : IRequest<IEnumerable<CountryDto>>;

public sealed class GetCountriesQueryHandler
    : IRequestHandler<GetCountriesQuery, IEnumerable<CountryDto>>
{
    private const string CacheKey = "geo:countries";
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    private readonly IApplicationDbContext _context;
    private readonly IMemoryCache _cache;

    public GetCountriesQueryHandler(IApplicationDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache   = cache;
    }

    public async Task<IEnumerable<CountryDto>> Handle(
        GetCountriesQuery request, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(CacheKey, out IEnumerable<CountryDto>? cached) && cached is not null)
            return cached;

        var result = await _context.Countries
            .AsNoTracking()
            .OrderBy(c => c.Ref)
            .Select(c => new CountryDto(c.Id, c.Ref, c.Code))
            .ToListAsync(cancellationToken);

        _cache.Set(CacheKey, (IEnumerable<CountryDto>)result,
            new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl });

        return result;
    }
}

// ── GetStatesByCountryQuery ───────────────────────────────────────────────────

/// <summary>Devuelve las comunidades autónomas / estados de un país.</summary>
public sealed record GetStatesByCountryQuery(int CountryId) : IRequest<IEnumerable<StateDto>>;

public sealed class GetStatesByCountryQueryHandler
    : IRequestHandler<GetStatesByCountryQuery, IEnumerable<StateDto>>
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    private readonly IApplicationDbContext _context;
    private readonly IMemoryCache _cache;

    public GetStatesByCountryQueryHandler(IApplicationDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache   = cache;
    }

    public async Task<IEnumerable<StateDto>> Handle(
        GetStatesByCountryQuery request, CancellationToken cancellationToken)
    {
        var key = $"geo:states:{request.CountryId}";

        if (_cache.TryGetValue(key, out IEnumerable<StateDto>? cached) && cached is not null)
            return cached;

        var result = await _context.TerritoryStates
            .AsNoTracking()
            .Where(s => s.IdCountry == request.CountryId)
            .OrderBy(s => s.Name ?? s.Ref)
            .Select(s => new StateDto(s.Id, s.Name ?? s.Ref, s.Code))
            .ToListAsync(cancellationToken);

        _cache.Set(key, (IEnumerable<StateDto>)result,
            new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl });

        return result;
    }
}

// ── GetProvincesByStateQuery ──────────────────────────────────────────────────

/// <summary>Devuelve las provincias de una comunidad autónoma / estado.</summary>
public sealed record GetProvincesByStateQuery(int StateId) : IRequest<IEnumerable<ProvinceDto>>;

public sealed class GetProvincesByStateQueryHandler
    : IRequestHandler<GetProvincesByStateQuery, IEnumerable<ProvinceDto>>
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    private readonly IApplicationDbContext _context;
    private readonly IMemoryCache _cache;

    public GetProvincesByStateQueryHandler(IApplicationDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache   = cache;
    }

    public async Task<IEnumerable<ProvinceDto>> Handle(
        GetProvincesByStateQuery request, CancellationToken cancellationToken)
    {
        var key = $"geo:provinces:{request.StateId}";

        if (_cache.TryGetValue(key, out IEnumerable<ProvinceDto>? cached) && cached is not null)
            return cached;

        var result = await _context.Provinces
            .AsNoTracking()
            .Where(p => p.IdState == request.StateId)
            .OrderBy(p => p.Name ?? p.Ref)
            .Select(p => new ProvinceDto(p.Id, p.Name ?? p.Ref, p.Code))
            .ToListAsync(cancellationToken);

        _cache.Set(key, (IEnumerable<ProvinceDto>)result,
            new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl });

        return result;
    }
}

// ── GetMunicipalitiesByProvinceQuery ──────────────────────────────────────────

/// <summary>Devuelve los municipios de una provincia.</summary>
public sealed record GetMunicipalitiesByProvinceQuery(int ProvinceId) : IRequest<IEnumerable<MunicipalityDto>>;

public sealed class GetMunicipalitiesByProvinceQueryHandler
    : IRequestHandler<GetMunicipalitiesByProvinceQuery, IEnumerable<MunicipalityDto>>
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    private readonly IApplicationDbContext _context;
    private readonly IMemoryCache _cache;

    public GetMunicipalitiesByProvinceQueryHandler(IApplicationDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache   = cache;
    }

    public async Task<IEnumerable<MunicipalityDto>> Handle(
        GetMunicipalitiesByProvinceQuery request, CancellationToken cancellationToken)
    {
        var key = $"geo:municipalities:{request.ProvinceId}";

        if (_cache.TryGetValue(key, out IEnumerable<MunicipalityDto>? cached) && cached is not null)
            return cached;

        var result = await _context.Municipalities
            .AsNoTracking()
            .Where(m => m.IdProvince == request.ProvinceId)
            .OrderBy(m => m.Name)
            .Select(m => new MunicipalityDto(m.Id, m.Name, m.Code))
            .ToListAsync(cancellationToken);

        _cache.Set(key, (IEnumerable<MunicipalityDto>)result,
            new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl });

        return result;
    }
}

// ── GetZipCodesByMunicipalityQuery ────────────────────────────────────────────

/// <summary>Devuelve los códigos postales de un municipio.</summary>
public sealed record GetZipCodesByMunicipalityQuery(int MunicipalityId) : IRequest<IEnumerable<string>>;

public sealed class GetZipCodesByMunicipalityQueryHandler
    : IRequestHandler<GetZipCodesByMunicipalityQuery, IEnumerable<string>>
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    private readonly IApplicationDbContext _context;
    private readonly IMemoryCache _cache;

    public GetZipCodesByMunicipalityQueryHandler(IApplicationDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache   = cache;
    }

    public async Task<IEnumerable<string>> Handle(
        GetZipCodesByMunicipalityQuery request, CancellationToken cancellationToken)
    {
        var key = $"geo:zipcodes:{request.MunicipalityId}";

        if (_cache.TryGetValue(key, out IEnumerable<string>? cached) && cached is not null)
            return cached;

        var result = await _context.MunicipalityZipCodes
            .AsNoTracking()
            .Where(z => z.IdMunicipality == request.MunicipalityId)
            .OrderBy(z => z.ZipCode)
            .Select(z => z.ZipCode)
            .ToListAsync(cancellationToken);

        _cache.Set(key, (IEnumerable<string>)result,
            new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl });

        return result;
    }
}
