using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GreenTransit.Application.Features.Residues.Commands;

// ── Crear ─────────────────────────────────────────────────────────────────────
public sealed record CreateResidueCommand(
    string   ResidueType,
    string   Name,
    string?  Description,
    string?  Reference,
    Guid?    IdLERCode,
    bool     IsDangerous,
    bool     IsRAEE,
    string?  DangerousCode,
    string?  FlowType,
    string?  ProductUse,
    string?  ProductCategory,
    decimal? WeightPerUnitKg,
    string?  DefaultMeasureUnit,
    // Ecodiseño
    int?     ReparabilityIndex,
    string?  DisassemblyEase,
    bool?    ContainsHazardous,
    decimal? RecycledContentPercent,
    string?  CompositionJson,
    string?  PotentialLERCodesJson,
    string?  MaterialsJson,
    // Productor
    Guid?    IdProducer,
    string?  ProducerRef
) : IRequest<Guid>;

public sealed class CreateResidueCommandHandler
    : IRequestHandler<CreateResidueCommand, Guid>
{
    private readonly IUnitOfWork           _uow;
    private readonly ICurrentUserService   _currentUser;
    private readonly ILogger<CreateResidueCommandHandler> _logger;

    public CreateResidueCommandHandler(
        IUnitOfWork uow,
        ICurrentUserService currentUser,
        ILogger<CreateResidueCommandHandler> logger)
    {
        _uow         = uow;
        _currentUser = currentUser;
        _logger      = logger;
    }

    public async Task<Guid> Handle(CreateResidueCommand request, CancellationToken cancellationToken)
    {
        var entity = new Residue
        {
            Id                    = Guid.NewGuid(),
            ResidueType           = request.ResidueType,
            Name                  = request.Name.Trim(),
            Description           = request.Description?.Trim(),
            Reference             = request.Reference?.Trim(),
            IdLERCode             = request.IdLERCode,
            IsDangerous           = request.IsDangerous,
            IsRAEE                = request.IsRAEE,
            DangerousCode         = request.DangerousCode?.Trim(),
            FlowType              = request.FlowType?.Trim(),
            ProductUse            = request.ProductUse?.Trim(),
            ProductCategory       = request.ProductCategory?.Trim(),
            WeightPerUnitKg       = request.WeightPerUnitKg,
            DefaultMeasureUnit    = request.DefaultMeasureUnit?.Trim(),
            ReparabilityIndex     = request.ReparabilityIndex,
            DisassemblyEase       = request.DisassemblyEase?.Trim(),
            ContainsHazardous     = request.ContainsHazardous,
            RecycledContentPercent = request.RecycledContentPercent,
            CompositionJson       = request.CompositionJson,
            PotentialLERCodesJson = request.PotentialLERCodesJson,
            MaterialsJson         = request.MaterialsJson,
            IdProducer            = request.IdProducer,
            ProducerRef           = request.ProducerRef?.Trim(),
            IsActive              = true,
            Version               = 1,
            IdUser                = _currentUser.IdUser,
            CreatedAt             = DateTime.UtcNow,
            UpdatedAt             = DateTime.UtcNow
        };

        await _uow.Residues.AddAsync(entity, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Residuo '{Name}' ({Type}) creado con Id {Id}",
            entity.Name, entity.ResidueType, entity.Id);
        return entity.Id;
    }
}

// ── Actualizar ────────────────────────────────────────────────────────────────
public sealed record UpdateResidueCommand(
    Guid     Id,
    string   ResidueType,
    string   Name,
    string?  Description,
    string?  Reference,
    Guid?    IdLERCode,
    bool     IsDangerous,
    bool     IsRAEE,
    string?  DangerousCode,
    string?  FlowType,
    string?  ProductUse,
    string?  ProductCategory,
    decimal? WeightPerUnitKg,
    string?  DefaultMeasureUnit,
    int?     ReparabilityIndex,
    string?  DisassemblyEase,
    bool?    ContainsHazardous,
    decimal? RecycledContentPercent,
    string?  CompositionJson,
    string?  PotentialLERCodesJson,
    string?  MaterialsJson,
    Guid?    IdProducer,
    string?  ProducerRef
) : IRequest;

public sealed class UpdateResidueCommandHandler
    : IRequestHandler<UpdateResidueCommand>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<UpdateResidueCommandHandler> _logger;

    public UpdateResidueCommandHandler(IUnitOfWork uow, ILogger<UpdateResidueCommandHandler> logger)
    {
        _uow    = uow;
        _logger = logger;
    }

    public async Task Handle(UpdateResidueCommand request, CancellationToken cancellationToken)
    {
        var entity = await _uow.Residues.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Residuo {request.Id} no encontrado.");

        entity.ResidueType           = request.ResidueType;
        entity.Name                  = request.Name.Trim();
        entity.Description           = request.Description?.Trim();
        entity.Reference             = request.Reference?.Trim();
        entity.IdLERCode             = request.IdLERCode;
        entity.IsDangerous           = request.IsDangerous;
        entity.IsRAEE                = request.IsRAEE;
        entity.DangerousCode         = request.DangerousCode?.Trim();
        entity.FlowType              = request.FlowType?.Trim();
        entity.ProductUse            = request.ProductUse?.Trim();
        entity.ProductCategory       = request.ProductCategory?.Trim();
        entity.WeightPerUnitKg       = request.WeightPerUnitKg;
        entity.DefaultMeasureUnit    = request.DefaultMeasureUnit?.Trim();
        entity.ReparabilityIndex     = request.ReparabilityIndex;
        entity.DisassemblyEase       = request.DisassemblyEase?.Trim();
        entity.ContainsHazardous     = request.ContainsHazardous;
        entity.RecycledContentPercent = request.RecycledContentPercent;
        entity.CompositionJson       = request.CompositionJson;
        entity.PotentialLERCodesJson = request.PotentialLERCodesJson;
        entity.MaterialsJson         = request.MaterialsJson;
        entity.IdProducer            = request.IdProducer;
        entity.ProducerRef           = request.ProducerRef?.Trim();
        entity.Version              += 1;
        entity.UpdatedAt             = DateTime.UtcNow;

        await _uow.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Residuo '{Name}' actualizado", entity.Name);
    }
}

// ── Toggle activo ─────────────────────────────────────────────────────────────
public sealed record ToggleResidueActiveCommand(Guid Id) : IRequest;

public sealed class ToggleResidueActiveCommandHandler
    : IRequestHandler<ToggleResidueActiveCommand>
{
    private readonly IUnitOfWork _uow;

    public ToggleResidueActiveCommandHandler(IUnitOfWork uow)
        => _uow = uow;

    public async Task Handle(ToggleResidueActiveCommand request, CancellationToken cancellationToken)
    {
        var entity = await _uow.Residues.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Residuo {request.Id} no encontrado.");

        entity.IsActive  = !entity.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        await _uow.SaveChangesAsync(cancellationToken);
    }
}
