using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GreenTransit.Application.Features.LerCodes.Commands;

// ── Crear ─────────────────────────────────────────────────────────────────────
public sealed record CreateLerCodeCommand(
    string  Code,
    string? CodeExtended,
    string  Description,
    string? Chapter,
    string? ChapterDescription,
    string? SubChapter,
    string? SubChapterDescription,
    bool    IsDangerous,
    bool    IsRAEE,
    string? DefaultProductCategory,
    string? Notes
) : IRequest<Guid>;

public sealed class CreateLerCodeCommandHandler
    : IRequestHandler<CreateLerCodeCommand, Guid>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<CreateLerCodeCommandHandler> _logger;

    public CreateLerCodeCommandHandler(IUnitOfWork uow, ILogger<CreateLerCodeCommandHandler> logger)
    {
        _uow    = uow;
        _logger = logger;
    }

    public async Task<Guid> Handle(CreateLerCodeCommand request, CancellationToken cancellationToken)
    {
        var entity = new LerCode
        {
            Id                    = Guid.NewGuid(),
            Code                  = request.Code.Trim(),
            CodeExtended          = request.CodeExtended?.Trim(),
            Description           = request.Description.Trim(),
            Chapter               = request.Chapter?.Trim(),
            ChapterDescription    = request.ChapterDescription?.Trim(),
            SubChapter            = request.SubChapter?.Trim(),
            SubChapterDescription = request.SubChapterDescription?.Trim(),
            IsDangerous           = request.IsDangerous,
            IsRAEE                = request.IsRAEE,
            DefaultProductCategory = request.DefaultProductCategory?.Trim(),
            Notes                 = request.Notes?.Trim(),
            IsActive              = true,
            CreatedAt             = DateTime.UtcNow,
            UpdatedAt             = DateTime.UtcNow
        };

        await _uow.LerCodes.AddAsync(entity, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("LER {Code} creado con Id {Id}", entity.Code, entity.Id);
        return entity.Id;
    }
}

// ── Actualizar ────────────────────────────────────────────────────────────────
public sealed record UpdateLerCodeCommand(
    Guid    Id,
    string  Code,
    string? CodeExtended,
    string  Description,
    string? Chapter,
    string? ChapterDescription,
    string? SubChapter,
    string? SubChapterDescription,
    bool    IsDangerous,
    bool    IsRAEE,
    string? DefaultProductCategory,
    string? Notes
) : IRequest;

public sealed class UpdateLerCodeCommandHandler
    : IRequestHandler<UpdateLerCodeCommand>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<UpdateLerCodeCommandHandler> _logger;

    public UpdateLerCodeCommandHandler(IUnitOfWork uow, ILogger<UpdateLerCodeCommandHandler> logger)
    {
        _uow    = uow;
        _logger = logger;
    }

    public async Task Handle(UpdateLerCodeCommand request, CancellationToken cancellationToken)
    {
        var entity = await _uow.LerCodes.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"LerCode {request.Id} no encontrado.");

        entity.Code                  = request.Code.Trim();
        entity.CodeExtended          = request.CodeExtended?.Trim();
        entity.Description           = request.Description.Trim();
        entity.Chapter               = request.Chapter?.Trim();
        entity.ChapterDescription    = request.ChapterDescription?.Trim();
        entity.SubChapter            = request.SubChapter?.Trim();
        entity.SubChapterDescription = request.SubChapterDescription?.Trim();
        entity.IsDangerous           = request.IsDangerous;
        entity.IsRAEE                = request.IsRAEE;
        entity.DefaultProductCategory = request.DefaultProductCategory?.Trim();
        entity.Notes                 = request.Notes?.Trim();
        entity.UpdatedAt             = DateTime.UtcNow;

        await _uow.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("LER {Code} actualizado", entity.Code);
    }
}

// ── Toggle activo ─────────────────────────────────────────────────────────────
public sealed record ToggleLerCodeActiveCommand(Guid Id) : IRequest;

public sealed class ToggleLerCodeActiveCommandHandler
    : IRequestHandler<ToggleLerCodeActiveCommand>
{
    private readonly IUnitOfWork _uow;

    public ToggleLerCodeActiveCommandHandler(IUnitOfWork uow)
        => _uow = uow;

    public async Task Handle(ToggleLerCodeActiveCommand request, CancellationToken cancellationToken)
    {
        var entity = await _uow.LerCodes.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"LerCode {request.Id} no encontrado.");

        entity.IsActive  = !entity.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        await _uow.SaveChangesAsync(cancellationToken);
    }
}
