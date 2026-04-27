using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GreenTransit.Application.Features.TreatmentOperations.Commands;

// ── Crear ─────────────────────────────────────────────────────────────────────
public sealed record CreateTreatmentOperationCommand(
    string  Code,
    string  OperationType,
    string  Description,
    string? ShortDescription,
    bool    IsRecycling,
    bool    IsEnergyRecovery,
    bool    IsPreparationForReuse,
    int?    SortOrder
) : IRequest<Guid>;

public sealed class CreateTreatmentOperationCommandHandler
    : IRequestHandler<CreateTreatmentOperationCommand, Guid>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<CreateTreatmentOperationCommandHandler> _logger;

    public CreateTreatmentOperationCommandHandler(
        IUnitOfWork uow,
        ILogger<CreateTreatmentOperationCommandHandler> logger)
    {
        _uow    = uow;
        _logger = logger;
    }

    public async Task<Guid> Handle(
        CreateTreatmentOperationCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new TreatmentOperation
        {
            Id                    = Guid.NewGuid(),
            Code                  = request.Code.Trim().ToUpper(),
            OperationType         = request.OperationType,
            Description           = request.Description.Trim(),
            ShortDescription      = request.ShortDescription?.Trim(),
            IsRecycling           = request.IsRecycling,
            IsEnergyRecovery      = request.IsEnergyRecovery,
            IsPreparationForReuse = request.IsPreparationForReuse,
            SortOrder             = request.SortOrder,
            IsActive              = true,
            CreatedAt             = DateTime.UtcNow,
            UpdatedAt             = DateTime.UtcNow
        };

        await _uow.TreatmentOperations.AddAsync(entity, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("TreatmentOperation '{Code}' creada con Id {Id}", entity.Code, entity.Id);
        return entity.Id;
    }
}

// ── Actualizar ────────────────────────────────────────────────────────────────
public sealed record UpdateTreatmentOperationCommand(
    Guid    Id,
    string  Code,
    string  OperationType,
    string  Description,
    string? ShortDescription,
    bool    IsRecycling,
    bool    IsEnergyRecovery,
    bool    IsPreparationForReuse,
    int?    SortOrder,
    bool    IsActive
) : IRequest;

public sealed class UpdateTreatmentOperationCommandHandler
    : IRequestHandler<UpdateTreatmentOperationCommand>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<UpdateTreatmentOperationCommandHandler> _logger;

    public UpdateTreatmentOperationCommandHandler(
        IUnitOfWork uow,
        ILogger<UpdateTreatmentOperationCommandHandler> logger)
    {
        _uow    = uow;
        _logger = logger;
    }

    public async Task Handle(
        UpdateTreatmentOperationCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await _uow.TreatmentOperations.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"TreatmentOperation {request.Id} no encontrada.");

        entity.Code                  = request.Code.Trim().ToUpper();
        entity.OperationType         = request.OperationType;
        entity.Description           = request.Description.Trim();
        entity.ShortDescription      = request.ShortDescription?.Trim();
        entity.IsRecycling           = request.IsRecycling;
        entity.IsEnergyRecovery      = request.IsEnergyRecovery;
        entity.IsPreparationForReuse = request.IsPreparationForReuse;
        entity.SortOrder             = request.SortOrder;
        entity.IsActive              = request.IsActive;
        entity.UpdatedAt             = DateTime.UtcNow;

        _uow.TreatmentOperations.Update(entity);
        await _uow.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("TreatmentOperation '{Code}' actualizada", entity.Code);
    }
}
