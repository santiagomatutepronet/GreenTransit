namespace GreenTransit.Application.Features.TreatmentOperations.DTOs;

/// <summary>DTO para listado y selector de operaciones R/D.</summary>
public sealed record TreatmentOperationDto(
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
);
