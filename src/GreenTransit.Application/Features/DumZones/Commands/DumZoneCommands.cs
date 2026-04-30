using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.DumZones.Commands;

// ─────────────────────────────────────────────────────────────────────────────
// CreateDumZoneCommand
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Crea una nueva zona DUM. Solo perfil ADMIN.</summary>
public sealed record CreateDumZoneCommand(
    string  ZoneCode,
    string  Name,
    string? Description,
    string  GeometryJson,
    string  Status = "Active"
) : IRequest<Guid>;

public sealed class CreateDumZoneCommandValidator : AbstractValidator<CreateDumZoneCommand>
{
    public CreateDumZoneCommandValidator()
    {
        RuleFor(x => x.ZoneCode)
            .NotEmpty().MaximumLength(50)
            .WithMessage("El código de zona es obligatorio (máx. 50 caracteres).");

        RuleFor(x => x.Name)
            .NotEmpty().MaximumLength(200)
            .WithMessage("El nombre es obligatorio (máx. 200 caracteres).");

        RuleFor(x => x.GeometryJson)
            .NotEmpty()
            .Must(BeValidGeoJsonPolygon)
            .WithMessage("GeometryJson debe ser un GeoJSON Polygon o MultiPolygon válido (RFC 7946).");

        RuleFor(x => x.Status)
            .Must(s => s is "Active" or "Inactive")
            .WithMessage("El estado debe ser Active o Inactive.");
    }

    private static bool BeValidGeoJsonPolygon(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeEl)) return false;
            var type = typeEl.GetString();
            if (type != "Polygon" && type != "MultiPolygon") return false;
            if (!root.TryGetProperty("coordinates", out _)) return false;
            return true;
        }
        catch { return false; }
    }
}

public sealed class CreateDumZoneCommandHandler
    : IRequestHandler<CreateDumZoneCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public CreateDumZoneCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(CreateDumZoneCommand request, CancellationToken ct)
    {
        var duplicate = await _context.DumZones
            .AnyAsync(z => z.ZoneCode == request.ZoneCode, ct);

        if (duplicate)
            throw new InvalidOperationException(
                $"Ya existe una zona DUM con código '{request.ZoneCode}'.");

        var now = DateTime.UtcNow;
        var zone = new DumZone
        {
            Id           = Guid.NewGuid(),
            ZoneCode     = request.ZoneCode.Trim(),
            Name         = request.Name.Trim(),
            Description  = request.Description?.Trim(),
            GeometryJson = request.GeometryJson,
            Status       = request.Status,
            Version      = 1,
            Hash         = ComputeHash(request.GeometryJson),
            CreatedAt    = now,
            UpdatedAt    = now,
            IdUser       = _currentUser.IdUser
        };

        _context.DumZones.Add(zone);
        await _context.SaveChangesAsync(ct);
        return zone.Id;
    }

    private static string ComputeHash(string data)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// UpdateDumZoneCommand
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Actualiza los datos de una zona DUM. Solo perfil ADMIN.</summary>
public sealed record UpdateDumZoneCommand(
    Guid    Id,
    string  ZoneCode,
    string  Name,
    string? Description,
    string  GeometryJson,
    string  Status
) : IRequest;

public sealed class UpdateDumZoneCommandValidator : AbstractValidator<UpdateDumZoneCommand>
{
    public UpdateDumZoneCommandValidator()
    {
        RuleFor(x => x.ZoneCode)
            .NotEmpty().MaximumLength(50);

        RuleFor(x => x.Name)
            .NotEmpty().MaximumLength(200);

        RuleFor(x => x.GeometryJson)
            .NotEmpty()
            .Must(BeValidGeoJsonPolygon)
            .WithMessage("GeometryJson debe ser un GeoJSON Polygon o MultiPolygon válido (RFC 7946).");

        RuleFor(x => x.Status)
            .Must(s => s is "Active" or "Inactive")
            .WithMessage("El estado debe ser Active o Inactive.");
    }

    private static bool BeValidGeoJsonPolygon(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeEl)) return false;
            var type = typeEl.GetString();
            if (type != "Polygon" && type != "MultiPolygon") return false;
            if (!root.TryGetProperty("coordinates", out _)) return false;
            return true;
        }
        catch { return false; }
    }
}

public sealed class UpdateDumZoneCommandHandler : IRequestHandler<UpdateDumZoneCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public UpdateDumZoneCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task Handle(UpdateDumZoneCommand request, CancellationToken ct)
    {
        var zone = await _context.DumZones
            .FirstOrDefaultAsync(z => z.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Zona DUM {request.Id} no encontrada.");

        // Verificar unicidad de ZoneCode si cambia
        if (zone.ZoneCode != request.ZoneCode)
        {
            var duplicate = await _context.DumZones
                .AnyAsync(z => z.ZoneCode == request.ZoneCode && z.Id != request.Id, ct);
            if (duplicate)
                throw new InvalidOperationException(
                    $"Ya existe otra zona DUM con código '{request.ZoneCode}'.");
        }

        zone.ZoneCode     = request.ZoneCode.Trim();
        zone.Name         = request.Name.Trim();
        zone.Description  = request.Description?.Trim();
        zone.GeometryJson = request.GeometryJson;
        zone.Status       = request.Status;
        zone.Version      += 1;
        zone.Hash         = ComputeHash(request.GeometryJson);
        zone.UpdatedAt    = DateTime.UtcNow;
        zone.IdUser       = _currentUser.IdUser;

        await _context.SaveChangesAsync(ct);
    }

    private static string ComputeHash(string data)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// AddRestrictionRuleCommand
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Añade una regla de restricción a una zona DUM. Solo perfil ADMIN.</summary>
public sealed record AddRestrictionRuleCommand(
    Guid      ZoneId,
    string    RuleCode,
    DateTime  ValidFrom,
    DateTime? ValidTo,
    string    ConditionsJson,
    string    ActionType,
    string?   ActionReason
) : IRequest<Guid>;

public sealed class AddRestrictionRuleCommandValidator
    : AbstractValidator<AddRestrictionRuleCommand>
{
    private static readonly string[] ValidActions = ["Block", "Restrict", "Allow", "Notify"];

    public AddRestrictionRuleCommandValidator()
    {
        RuleFor(x => x.ZoneId)
            .NotEmpty().WithMessage("El Id de zona es obligatorio.");

        RuleFor(x => x.RuleCode)
            .NotEmpty().MaximumLength(50)
            .WithMessage("El código de regla es obligatorio (máx. 50 caracteres).");

        RuleFor(x => x.ValidFrom)
            .NotEmpty().WithMessage("La fecha de inicio de vigencia es obligatoria.");

        RuleFor(x => x.ValidTo)
            .GreaterThan(x => x.ValidFrom)
            .When(x => x.ValidTo.HasValue)
            .WithMessage("La fecha fin debe ser posterior a la fecha inicio.");

        RuleFor(x => x.ConditionsJson)
            .NotEmpty()
            .Must(BeValidJson)
            .WithMessage("ConditionsJson debe ser un JSON válido.");

        RuleFor(x => x.ActionType)
            .NotEmpty()
            .Must(a => ValidActions.Contains(a))
            .WithMessage("ActionType debe ser: Block, Restrict, Allow o Notify.");
    }

    private static bool BeValidJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try { JsonDocument.Parse(json); return true; }
        catch { return false; }
    }
}

public sealed class AddRestrictionRuleCommandHandler
    : IRequestHandler<AddRestrictionRuleCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public AddRestrictionRuleCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(AddRestrictionRuleCommand request, CancellationToken ct)
    {
        var zoneExists = await _context.DumZones
            .AnyAsync(z => z.Id == request.ZoneId, ct);

        if (!zoneExists)
            throw new KeyNotFoundException($"Zona DUM {request.ZoneId} no encontrada.");

        var now = DateTime.UtcNow;
        var rule = new DumRestrictionRule
        {
            Id             = Guid.NewGuid(),
            ZoneId         = request.ZoneId,
            RuleCode       = request.RuleCode.Trim(),
            Status         = "Active",
            ValidFrom      = request.ValidFrom,
            ValidTo        = request.ValidTo,
            ConditionsJson = request.ConditionsJson,
            ActionType     = request.ActionType,
            ActionReason   = request.ActionReason?.Trim(),
            Version        = 1,
            CreatedAt      = now,
            UpdatedAt      = now
        };

        _context.DumRestrictionRules.Add(rule);
        await _context.SaveChangesAsync(ct);
        return rule.Id;
    }
}
