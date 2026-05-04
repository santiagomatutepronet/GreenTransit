using FluentValidation;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.PlantEnergies.Commands;

// ── CreatePlantEnergyCommand ──────────────────────────────────────────────────

/// <summary>Crea un registro de consumo energético mensual para una planta.</summary>
public sealed record CreatePlantEnergyCommand(
    string   PlantName,
    string   PlantCenterCode,
    int      Year,
    int      Month,
    decimal  KwhTotal,
    string?  Source          = null,
    string?  GridMixRef      = null,
    string?  AllocationMethod = null,
    string?  Notes           = null
) : IRequest<Guid>;

public sealed class CreatePlantEnergyCommandValidator
    : AbstractValidator<CreatePlantEnergyCommand>
{
    public CreatePlantEnergyCommandValidator()
    {
        RuleFor(x => x.PlantName)
            .NotEmpty().MaximumLength(200)
            .WithMessage("El nombre de la planta es obligatorio (máx. 200 caracteres).");

        RuleFor(x => x.PlantCenterCode)
            .NotEmpty().MaximumLength(50)
            .WithMessage("El código NIMA de la planta es obligatorio.");

        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 2100)
            .WithMessage("El año debe estar entre 2000 y 2100.");

        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12)
            .WithMessage("El mes debe estar entre 1 y 12.");

        RuleFor(x => x.KwhTotal)
            .GreaterThanOrEqualTo(0)
            .WithMessage("El consumo no puede ser negativo.");
    }
}

public sealed class CreatePlantEnergyCommandHandler
    : IRequestHandler<CreatePlantEnergyCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public CreatePlantEnergyCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(
        CreatePlantEnergyCommand request, CancellationToken ct)
    {
        var ownerId = _currentUser.OwnerId;

        var duplicate = await _context.PlantEnergies.AnyAsync(e =>
            e.OwnerId        == ownerId
         && e.PlantCenterCode == request.PlantCenterCode
         && e.Year            == request.Year
         && e.Month           == request.Month, ct);

        if (duplicate)
            throw new InvalidOperationException(
                $"Ya existe un registro para la planta '{request.PlantCenterCode}' " +
                $"en {request.Month:D2}/{request.Year}.");

        var entity = new PlantEnergy
        {
            Id               = Guid.NewGuid(),
            OwnerId          = ownerId,
            PlantName        = request.PlantName,
            PlantCenterCode  = request.PlantCenterCode,
            Year             = request.Year,
            Month            = request.Month,
            KwhTotal         = request.KwhTotal,
            Source           = request.Source,
            GridMixRef       = request.GridMixRef,
            AllocationMethod = request.AllocationMethod,
            Notes            = request.Notes,
            CreatedAt        = DateTime.UtcNow,
            UpdatedAt        = DateTime.UtcNow,
            IdUser           = _currentUser.IdUser,
            Version          = 1
        };

        _context.PlantEnergies.Add(entity);
        await _context.SaveChangesAsync(ct);
        return entity.Id;
    }
}

// ── UpdatePlantEnergyCommand ──────────────────────────────────────────────────

/// <summary>Actualiza un registro de consumo energético existente.</summary>
public sealed record UpdatePlantEnergyCommand(
    Guid     Id,
    string   PlantName,
    string   PlantCenterCode,
    int      Year,
    int      Month,
    decimal  KwhTotal,
    string?  Source           = null,
    string?  GridMixRef       = null,
    string?  AllocationMethod  = null,
    string?  Notes            = null
) : IRequest<Unit>;

public sealed class UpdatePlantEnergyCommandValidator
    : AbstractValidator<UpdatePlantEnergyCommand>
{
    public UpdatePlantEnergyCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.PlantName)
            .NotEmpty().MaximumLength(200);

        RuleFor(x => x.PlantCenterCode)
            .NotEmpty().MaximumLength(50);

        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 2100);

        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12);

        RuleFor(x => x.KwhTotal)
            .GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdatePlantEnergyCommandHandler
    : IRequestHandler<UpdatePlantEnergyCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public UpdatePlantEnergyCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(
        UpdatePlantEnergyCommand request, CancellationToken ct)
    {
        var ownerId = _currentUser.OwnerId;

        var entity = await _context.PlantEnergies
            .FirstOrDefaultAsync(e => e.Id == request.Id && e.OwnerId == ownerId, ct)
            ?? throw new InvalidOperationException("Registro no encontrado.");

        // Verificar duplicado para el nuevo mes (distinto al que se edita)
        var duplicate = await _context.PlantEnergies.AnyAsync(e =>
            e.Id             != request.Id
         && e.OwnerId        == ownerId
         && e.PlantCenterCode == request.PlantCenterCode
         && e.Year            == request.Year
         && e.Month           == request.Month, ct);

        if (duplicate)
            throw new InvalidOperationException(
                $"Ya existe un registro para la planta '{request.PlantCenterCode}' " +
                $"en {request.Month:D2}/{request.Year}.");

        entity.PlantName        = request.PlantName;
        entity.PlantCenterCode  = request.PlantCenterCode;
        entity.Year             = request.Year;
        entity.Month            = request.Month;
        entity.KwhTotal         = request.KwhTotal;
        entity.Source           = request.Source;
        entity.GridMixRef       = request.GridMixRef;
        entity.AllocationMethod = request.AllocationMethod;
        entity.Notes            = request.Notes;
        entity.UpdatedAt        = DateTime.UtcNow;
        entity.IdUser           = _currentUser.IdUser;
        entity.Version++;

        await _context.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
