using FluentValidation;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.EmissionFactors.Commands;

// ── CreateEmissionFactorSetCommand ────────────────────────────────────────────

/// <summary>
/// Crea un nuevo set de factores con todas sus líneas en una transacción.
/// Solo ADMIN.
/// </summary>
public sealed record CreateEmissionFactorSetCommand(
    string                              FactorSetName,
    string                              Version,
    DateTime                            ValidFrom,
    DateTime?                           ValidTo,
    string?                             Publisher,
    string?                             Reference,
    string?                             Methodology,
    IReadOnlyList<EmissionFactorLineDto> Factors
) : IRequest<Guid>;

/// <summary>Línea de factor de emisión para el comando de creación.</summary>
public sealed record EmissionFactorLineDto(
    string  VehicleType,
    string  FuelType,
    string? EuroClass,
    string  Unit,
    decimal Value
);

public sealed class CreateEmissionFactorSetCommandValidator
    : AbstractValidator<CreateEmissionFactorSetCommand>
{
    public CreateEmissionFactorSetCommandValidator()
    {
        RuleFor(x => x.FactorSetName)
            .NotEmpty().MaximumLength(200)
            .WithMessage("El nombre del set es obligatorio.");

        RuleFor(x => x.Version)
            .NotEmpty().MaximumLength(50)
            .WithMessage("La versión es obligatoria.");

        RuleFor(x => x.ValidFrom)
            .NotEmpty()
            .WithMessage("La fecha de inicio de validez es obligatoria.");

        RuleFor(x => x.ValidTo)
            .GreaterThan(x => x.ValidFrom)
            .When(x => x.ValidTo.HasValue)
            .WithMessage("ValidTo debe ser posterior a ValidFrom.");

        RuleFor(x => x.Factors)
            .NotEmpty().WithMessage("El set debe contener al menos una línea de factor.");

        RuleForEach(x => x.Factors).ChildRules(f =>
        {
            f.RuleFor(l => l.VehicleType).NotEmpty().MaximumLength(100);
            f.RuleFor(l => l.FuelType).NotEmpty().MaximumLength(100);
            f.RuleFor(l => l.Unit).NotEmpty().MaximumLength(50);
            f.RuleFor(l => l.Value).GreaterThan(0)
                .WithMessage("El valor del factor debe ser mayor que 0.");
        });
    }
}

public sealed class CreateEmissionFactorSetCommandHandler
    : IRequestHandler<CreateEmissionFactorSetCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public CreateEmissionFactorSetCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(
        CreateEmissionFactorSetCommand request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var set = new EmissionFactorSet
        {
            Id           = Guid.NewGuid(),
            OwnerId      = _currentUser.OwnerId == Guid.Empty ? null : _currentUser.OwnerId,
            FactorSetName = request.FactorSetName,
            Version      = request.Version,
            Status       = "Inactive",   // se activa explícitamente con ActivateEmissionFactorSetCommand
            ValidFrom    = request.ValidFrom,
            ValidTo      = request.ValidTo,
            Publisher    = request.Publisher,
            Reference    = request.Reference,
            Methodology  = request.Methodology,
            CreatedAt    = now,
            UpdatedAt    = now,
            IdUser       = _currentUser.IdUser
        };

        foreach (var line in request.Factors)
        {
            set.EmissionFactors.Add(new EmissionFactor
            {
                Id          = Guid.NewGuid(),
                FactorSetId = set.Id,
                VehicleType = line.VehicleType,
                FuelType    = line.FuelType,
                EuroClass   = line.EuroClass,
                Unit        = line.Unit,
                Value       = line.Value,
                CreatedAt   = now
            });
        }

        _context.EmissionFactorSets.Add(set);
        await _context.SaveChangesAsync(ct);
        return set.Id;
    }
}

// ── ActivateEmissionFactorSetCommand ──────────────────────────────────────────

/// <summary>
/// Activa el set indicado y desactiva todos los demás.
/// Solo uno puede estar activo a la vez. Solo ADMIN.
/// </summary>
public sealed record ActivateEmissionFactorSetCommand(Guid SetId) : IRequest<Unit>;

public sealed class ActivateEmissionFactorSetCommandHandler
    : IRequestHandler<ActivateEmissionFactorSetCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public ActivateEmissionFactorSetCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(
        ActivateEmissionFactorSetCommand request, CancellationToken ct)
    {
        var target = await _context.EmissionFactorSets
            .FirstOrDefaultAsync(s => s.Id == request.SetId, ct)
            ?? throw new InvalidOperationException("Set de factores no encontrado.");

        // Desactivar todos los demás sets activos
        var activeSets = await _context.EmissionFactorSets
            .Where(s => s.Status == "Active" && s.Id != request.SetId)
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
        return Unit.Value;
    }
}
