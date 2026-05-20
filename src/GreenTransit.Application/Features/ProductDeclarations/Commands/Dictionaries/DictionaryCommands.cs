using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Entities;
using MediatR;

namespace GreenTransit.Application.Features.ProductDeclarations.Commands.Dictionaries;

// ── Categorías ────────────────────────────────────────────────────────────────

public sealed record UpsertDicProductDeclarationCategoryCommand(
    int?   Id,
    string Ref,
    string Description
) : IRequest<int>;

public sealed class UpsertDicProductDeclarationCategoryCommandHandler
    : IRequestHandler<UpsertDicProductDeclarationCategoryCommand, int>
{
    private readonly IApplicationDbContext _context;
    public UpsertDicProductDeclarationCategoryCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<int> Handle(
        UpsertDicProductDeclarationCategoryCommand request, CancellationToken ct)
    {
        if (request.Id.HasValue)
        {
            var entity = await _context.DicProductDeclarationCategories
                .FirstOrDefaultAsync(x => x.Id == request.Id.Value, ct)
                ?? throw new KeyNotFoundException($"Categoría {request.Id} no encontrada.");
            entity.Ref = request.Ref;
            entity.Description = request.Description;
            await _context.SaveChangesAsync(ct);
            return entity.Id;
        }
        else
        {
            var entity = new DicProductDeclarationCategory
                { Ref = request.Ref, Description = request.Description };
            _context.Add(entity);
            await _context.SaveChangesAsync(ct);
            return entity.Id;
        }
    }
}

// ── Periodos ──────────────────────────────────────────────────────────────────

public sealed record UpsertDicProductDeclarationPeriodCommand(
    int?   Id,
    string Ref,
    string Description
) : IRequest<int>;

public sealed class UpsertDicProductDeclarationPeriodCommandHandler
    : IRequestHandler<UpsertDicProductDeclarationPeriodCommand, int>
{
    private readonly IApplicationDbContext _context;
    public UpsertDicProductDeclarationPeriodCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<int> Handle(
        UpsertDicProductDeclarationPeriodCommand request, CancellationToken ct)
    {
        if (request.Id.HasValue)
        {
            var entity = await _context.DicProductDeclarationPeriods
                .FirstOrDefaultAsync(x => x.Id == request.Id.Value, ct)
                ?? throw new KeyNotFoundException($"Periodo {request.Id} no encontrado.");
            entity.Ref = request.Ref;
            entity.Description = request.Description;
            await _context.SaveChangesAsync(ct);
            return entity.Id;
        }
        else
        {
            var entity = new DicProductDeclarationPeriod
                { Ref = request.Ref, Description = request.Description };
            _context.Add(entity);
            await _context.SaveChangesAsync(ct);
            return entity.Id;
        }
    }
}

// ── Productos declarables ─────────────────────────────────────────────────────

public sealed record UpsertDicProductDeclarationProductCommand(
    int?   Id,
    string Ref,
    string Description,
    int?   CategoryId
) : IRequest<int>;

public sealed class UpsertDicProductDeclarationProductCommandHandler
    : IRequestHandler<UpsertDicProductDeclarationProductCommand, int>
{
    private readonly IApplicationDbContext _context;
    public UpsertDicProductDeclarationProductCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<int> Handle(
        UpsertDicProductDeclarationProductCommand request, CancellationToken ct)
    {
        if (request.Id.HasValue)
        {
            var entity = await _context.DicProductDeclarationProducts
                .FirstOrDefaultAsync(x => x.Id == request.Id.Value, ct)
                ?? throw new KeyNotFoundException($"Producto {request.Id} no encontrado.");
            entity.Ref = request.Ref;
            entity.Description = request.Description;
            entity.CategoryId = request.CategoryId;
            await _context.SaveChangesAsync(ct);
            return entity.Id;
        }
        else
        {
            var entity = new DicProductDeclarationProduct
            {
                Ref = request.Ref,
                Description = request.Description,
                CategoryId = request.CategoryId
            };
            _context.Add(entity);
            await _context.SaveChangesAsync(ct);
            return entity.Id;
        }
    }
}

// ── Fuentes ───────────────────────────────────────────────────────────────────

public sealed record UpsertDicProductDeclarationSourceCommand(
    int?   Id,
    string Ref,
    string Description
) : IRequest<int>;

public sealed class UpsertDicProductDeclarationSourceCommandHandler
    : IRequestHandler<UpsertDicProductDeclarationSourceCommand, int>
{
    private readonly IApplicationDbContext _context;
    public UpsertDicProductDeclarationSourceCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<int> Handle(
        UpsertDicProductDeclarationSourceCommand request, CancellationToken ct)
    {
        if (request.Id.HasValue)
        {
            var entity = await _context.DicProductDeclarationSources
                .FirstOrDefaultAsync(x => x.Id == request.Id.Value, ct)
                ?? throw new KeyNotFoundException($"Fuente {request.Id} no encontrada.");
            entity.Ref = request.Ref;
            entity.Description = request.Description;
            await _context.SaveChangesAsync(ct);
            return entity.Id;
        }
        else
        {
            var entity = new DicProductDeclarationSource
                { Ref = request.Ref, Description = request.Description };
            _context.Add(entity);
            await _context.SaveChangesAsync(ct);
            return entity.Id;
        }
    }
}

// ── Tipos ─────────────────────────────────────────────────────────────────────

public sealed record UpsertDicProductDeclarationTypeCommand(
    int?   Id,
    string Ref,
    string Description
) : IRequest<int>;

public sealed class UpsertDicProductDeclarationTypeCommandHandler
    : IRequestHandler<UpsertDicProductDeclarationTypeCommand, int>
{
    private readonly IApplicationDbContext _context;
    public UpsertDicProductDeclarationTypeCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<int> Handle(
        UpsertDicProductDeclarationTypeCommand request, CancellationToken ct)
    {
        if (request.Id.HasValue)
        {
            var entity = await _context.DicProductDeclarationTypes
                .FirstOrDefaultAsync(x => x.Id == request.Id.Value, ct)
                ?? throw new KeyNotFoundException($"Tipo {request.Id} no encontrado.");
            entity.Ref = request.Ref;
            entity.Description = request.Description;
            await _context.SaveChangesAsync(ct);
            return entity.Id;
        }
        else
        {
            var entity = new DicProductDeclarationType
                { Ref = request.Ref, Description = request.Description };
            _context.Add(entity);
            await _context.SaveChangesAsync(ct);
            return entity.Id;
        }
    }
}

// ── Usos ──────────────────────────────────────────────────────────────────────

public sealed record UpsertDicProductDeclarationUseCommand(
    int?   Id,
    string Ref,
    string Description
) : IRequest<int>;

public sealed class UpsertDicProductDeclarationUseCommandHandler
    : IRequestHandler<UpsertDicProductDeclarationUseCommand, int>
{
    private readonly IApplicationDbContext _context;
    public UpsertDicProductDeclarationUseCommandHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<int> Handle(
        UpsertDicProductDeclarationUseCommand request, CancellationToken ct)
    {
        if (request.Id.HasValue)
        {
            var entity = await _context.DicProductDeclarationUses
                .FirstOrDefaultAsync(x => x.Id == request.Id.Value, ct)
                ?? throw new KeyNotFoundException($"Uso {request.Id} no encontrado.");
            entity.Ref = request.Ref;
            entity.Description = request.Description;
            await _context.SaveChangesAsync(ct);
            return entity.Id;
        }
        else
        {
            var entity = new DicProductDeclarationUse
                { Ref = request.Ref, Description = request.Description };
            _context.Add(entity);
            await _context.SaveChangesAsync(ct);
            return entity.Id;
        }
    }
}
