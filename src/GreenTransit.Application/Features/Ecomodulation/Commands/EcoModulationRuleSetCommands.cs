using FluentValidation;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Ecomodulation.DTOs;
using GreenTransit.Domain.Authorization;
using GreenTransit.Domain.Entities;
using MediatR;

namespace GreenTransit.Application.Features.Ecomodulation.Commands;

// ── CreateEcoModulationRuleSetCommand ─────────────────────────────────────────

/// <summary>Crea un nuevo conjunto de reglas de ecomodulación con sus reglas. Solo ADMIN.</summary>
public sealed record CreateEcoModulationRuleSetCommand(
    string                                  RuleSetName,
    string                                  Version,
    DateTime                                ValidFrom,
    DateTime?                               ValidTo,
    string?                                 PublisherName,
    string?                                 PublisherNationalId,
    string?                                 PublisherCenterCode,
    IReadOnlyList<EcoModulationRuleLineDto> Rules
) : IRequest<Guid>;

public sealed class CreateEcoModulationRuleSetCommandValidator
    : AbstractValidator<CreateEcoModulationRuleSetCommand>
{
    public CreateEcoModulationRuleSetCommandValidator()
    {
        RuleFor(x => x.RuleSetName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Version).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ValidFrom).NotEmpty();
        RuleFor(x => x.ValidTo)
            .GreaterThan(x => x.ValidFrom)
            .When(x => x.ValidTo.HasValue)
            .WithMessage("ValidTo debe ser posterior a ValidFrom.");
        RuleFor(x => x.Rules).NotEmpty().WithMessage("El conjunto debe contener al menos una regla.");
        RuleForEach(x => x.Rules).ChildRules(r =>
        {
            r.RuleFor(l => l.RuleCode).NotEmpty().MaximumLength(64);
            r.RuleFor(l => l.CriteriaJson).NotEmpty();
            r.RuleFor(l => l.FeeImpactType).NotEmpty().MaximumLength(32);
        });
    }
}

public sealed class CreateEcoModulationRuleSetCommandHandler
    : IRequestHandler<CreateEcoModulationRuleSetCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public CreateEcoModulationRuleSetCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(CreateEcoModulationRuleSetCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsInProfile(ProfileConstants.Admin))
            throw new UnauthorizedAccessException("Solo el administrador puede gestionar conjuntos de reglas de ecomodulación.");

        var now = DateTime.UtcNow;
        var set = new EcoModulationRuleSet
        {
            Id                  = Guid.NewGuid(),
            OwnerId             = _currentUser.OwnerId,
            RuleSetName         = request.RuleSetName,
            Version             = request.Version,
            Status              = "Inactive",
            ValidFrom           = request.ValidFrom,
            ValidTo             = request.ValidTo,
            PublisherName       = request.PublisherName,
            PublisherNationalId = request.PublisherNationalId,
            PublisherCenterCode = request.PublisherCenterCode,
            Hash                = Guid.NewGuid().ToString("N"),
            CreatedAt           = now,
            UpdatedAt           = now,
            IdUser              = _currentUser.IdUser
        };

        foreach (var line in request.Rules)
        {
            set.EcoModulationRules.Add(new EcoModulationRule
            {
                Id             = Guid.NewGuid(),
                RuleSetId      = set.Id,
                RuleCode       = line.RuleCode,
                ProductCategory = line.ProductCategory,
                CriteriaJson   = line.CriteriaJson,
                FeeImpactType  = line.FeeImpactType,
                FeeImpactValue = line.FeeImpactValue,
                CreatedAt      = now
            });
        }

        _context.Add(set);
        await _context.SaveChangesAsync(ct);
        return set.Id;
    }
}

// ── UpdateEcoModulationRuleSetCommand ─────────────────────────────────────────

/// <summary>Actualiza la cabecera de un conjunto de reglas y reemplaza sus reglas. Solo ADMIN.</summary>
public sealed record UpdateEcoModulationRuleSetCommand(
    Guid                                    Id,
    string                                  RuleSetName,
    string                                  Version,
    DateTime                                ValidFrom,
    DateTime?                               ValidTo,
    string?                                 PublisherName,
    string?                                 PublisherNationalId,
    string?                                 PublisherCenterCode,
    IReadOnlyList<EcoModulationRuleLineDto> Rules
) : IRequest;

public sealed class UpdateEcoModulationRuleSetCommandValidator
    : AbstractValidator<UpdateEcoModulationRuleSetCommand>
{
    public UpdateEcoModulationRuleSetCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.RuleSetName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Version).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ValidFrom).NotEmpty();
        RuleFor(x => x.ValidTo)
            .GreaterThan(x => x.ValidFrom)
            .When(x => x.ValidTo.HasValue)
            .WithMessage("ValidTo debe ser posterior a ValidFrom.");
        RuleFor(x => x.Rules).NotEmpty().WithMessage("El conjunto debe contener al menos una regla.");
        RuleForEach(x => x.Rules).ChildRules(r =>
        {
            r.RuleFor(l => l.RuleCode).NotEmpty().MaximumLength(64);
            r.RuleFor(l => l.CriteriaJson).NotEmpty();
            r.RuleFor(l => l.FeeImpactType).NotEmpty().MaximumLength(32);
        });
    }
}

public sealed class UpdateEcoModulationRuleSetCommandHandler
    : IRequestHandler<UpdateEcoModulationRuleSetCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public UpdateEcoModulationRuleSetCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task Handle(UpdateEcoModulationRuleSetCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsInProfile(ProfileConstants.Admin))
            throw new UnauthorizedAccessException("Solo el administrador puede gestionar conjuntos de reglas de ecomodulación.");

        var set = await _context.EcoModulationRuleSets
            .Include(x => x.EcoModulationRules)
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.OwnerId == _currentUser.OwnerId, ct)
            ?? throw new KeyNotFoundException($"Conjunto de reglas {request.Id} no encontrado.");

        var now = DateTime.UtcNow;
        set.RuleSetName         = request.RuleSetName;
        set.Version             = request.Version;
        set.ValidFrom           = request.ValidFrom;
        set.ValidTo             = request.ValidTo;
        set.PublisherName       = request.PublisherName;
        set.PublisherNationalId = request.PublisherNationalId;
        set.PublisherCenterCode = request.PublisherCenterCode;
        set.Hash                = Guid.NewGuid().ToString("N");
        set.UpdatedAt           = now;
        set.IdUser              = _currentUser.IdUser;

        // Borrar reglas existentes directamente en BD (evita problemas de concurrencia en el change tracker)
        await _context.EcoModulationRules
            .Where(r => r.RuleSetId == set.Id)
            .ExecuteDeleteAsync(ct);

        // Añadir las nuevas reglas como entidades nuevas (detached del set original)
        var newRules = request.Rules.Select(line => new EcoModulationRule
        {
            Id              = Guid.NewGuid(),
            RuleSetId       = set.Id,
            RuleCode        = line.RuleCode,
            ProductCategory = line.ProductCategory,
            CriteriaJson    = line.CriteriaJson,
            FeeImpactType   = line.FeeImpactType,
            FeeImpactValue  = line.FeeImpactValue,
            CreatedAt       = now
        }).ToList();

        _context.AddRange(newRules);
        await _context.SaveChangesAsync(ct);
    }
}

// ── ActivateEcoModulationRuleSetCommand ───────────────────────────────────────

/// <summary>Activa el conjunto indicado y desactiva todos los demás. Solo ADMIN.</summary>
public sealed record ActivateEcoModulationRuleSetCommand(Guid Id) : IRequest;

public sealed class ActivateEcoModulationRuleSetCommandHandler
    : IRequestHandler<ActivateEcoModulationRuleSetCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public ActivateEcoModulationRuleSetCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task Handle(ActivateEcoModulationRuleSetCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsInProfile(ProfileConstants.Admin))
            throw new UnauthorizedAccessException("Solo el administrador puede activar conjuntos de reglas de ecomodulación.");

        var target = await _context.EcoModulationRuleSets
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.OwnerId == _currentUser.OwnerId, ct)
            ?? throw new KeyNotFoundException($"Conjunto de reglas {request.Id} no encontrado.");

        var activeSets = await _context.EcoModulationRuleSets
            .Where(x => x.OwnerId == _currentUser.OwnerId && x.Status == "Active" && x.Id != request.Id)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        foreach (var s in activeSets)
        {
            s.Status    = "Inactive";
            s.UpdatedAt = now;
            s.IdUser    = _currentUser.IdUser;
        }

        target.Status    = "Active";
        target.UpdatedAt = now;
        target.IdUser    = _currentUser.IdUser;

        await _context.SaveChangesAsync(ct);
    }
}

// ── DeleteEcoModulationRuleSetCommand ─────────────────────────────────────────

/// <summary>Elimina un conjunto de reglas inactivo. Solo ADMIN.</summary>
public sealed record DeleteEcoModulationRuleSetCommand(Guid Id) : IRequest;

public sealed class DeleteEcoModulationRuleSetCommandHandler
    : IRequestHandler<DeleteEcoModulationRuleSetCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public DeleteEcoModulationRuleSetCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task Handle(DeleteEcoModulationRuleSetCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsInProfile(ProfileConstants.Admin))
            throw new UnauthorizedAccessException("Solo el administrador puede eliminar conjuntos de reglas de ecomodulación.");

        var set = await _context.EcoModulationRuleSets
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.OwnerId == _currentUser.OwnerId, ct)
            ?? throw new KeyNotFoundException($"Conjunto de reglas {request.Id} no encontrado.");

        if (set.Status == "Active")
            throw new InvalidOperationException("No se puede eliminar un conjunto de reglas activo. Desactívelo primero.");

        _context.Remove(set);
        await _context.SaveChangesAsync(ct);
    }
}
