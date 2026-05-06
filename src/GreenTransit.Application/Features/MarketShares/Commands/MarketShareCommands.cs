using FluentValidation;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Authorization;
using GreenTransit.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.MarketShares.Commands;

// ── Crear cuota ───────────────────────────────────────────────────────────────

public sealed record CreateMarketShareCommand(
    Guid     IdScrap,
    string   Category,
    string?  AutonomousCommunity,
    int      Year,
    decimal  Weight,
    int?     Period,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo,
    string?  FlowType
) : IRequest<Guid>;

public sealed class CreateMarketShareCommandValidator : AbstractValidator<CreateMarketShareCommand>
{
    public CreateMarketShareCommandValidator()
    {
        RuleFor(x => x.IdScrap).NotEmpty();
        RuleFor(x => x.Category).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Weight).GreaterThan(0);
    }
}

public sealed class CreateMarketShareCommandHandler : IRequestHandler<CreateMarketShareCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public CreateMarketShareCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(CreateMarketShareCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsInProfile(ProfileConstants.Admin))
            throw new UnauthorizedAccessException("Solo el administrador puede gestionar cuotas de mercado.");

        var duplicate = await _context.MarketShares.AnyAsync(ms =>
            ms.OwnerId             == _currentUser.OwnerId &&
            ms.IdScrap             == request.IdScrap &&
            ms.Category            == request.Category &&
            ms.AutonomousCommunity == request.AutonomousCommunity &&
            ms.Year                == request.Year &&
            ms.Period              == request.Period, ct);

        if (duplicate)
            throw new InvalidOperationException(
                "Ya existe una cuota de mercado con el mismo SCRAP, categoría, CCAA, año y periodo.");

        var entity = new MarketShare
        {
            Id                 = Guid.NewGuid(),
            OwnerId            = _currentUser.OwnerId,
            IdScrap            = request.IdScrap,
            Category           = request.Category,
            AutonomousCommunity = request.AutonomousCommunity,
            Year               = request.Year,
            Weight             = request.Weight,
            Period             = request.Period,
            EffectiveFrom      = request.EffectiveFrom,
            EffectiveTo        = request.EffectiveTo,
            FlowType           = request.FlowType,
            Version            = 1
        };

        _context.MarketShares.Add(entity);
        await _context.SaveChangesAsync(ct);
        return entity.Id;
    }
}

// ── Actualizar cuota ──────────────────────────────────────────────────────────

public sealed record UpdateMarketShareCommand(
    Guid     Id,
    string   Category,
    string?  AutonomousCommunity,
    int      Year,
    decimal  Weight,
    int?     Period,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo,
    string?  FlowType
) : IRequest;

public sealed class UpdateMarketShareCommandValidator : AbstractValidator<UpdateMarketShareCommand>
{
    public UpdateMarketShareCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Category).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Weight).GreaterThan(0);
    }
}

public sealed class UpdateMarketShareCommandHandler : IRequestHandler<UpdateMarketShareCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public UpdateMarketShareCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task Handle(UpdateMarketShareCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsInProfile(ProfileConstants.Admin))
            throw new UnauthorizedAccessException("Solo el administrador puede gestionar cuotas de mercado.");

        var entity = await _context.MarketShares
            .FirstOrDefaultAsync(ms => ms.Id == request.Id && ms.OwnerId == _currentUser.OwnerId, ct)
            ?? throw new KeyNotFoundException($"Cuota de mercado {request.Id} no encontrada.");

        // Unicidad: si cambian los campos de clave, verificar duplicado
        var duplicate = await _context.MarketShares.AnyAsync(ms =>
            ms.Id                  != request.Id &&
            ms.OwnerId             == _currentUser.OwnerId &&
            ms.IdScrap             == entity.IdScrap &&
            ms.Category            == request.Category &&
            ms.AutonomousCommunity == request.AutonomousCommunity &&
            ms.Year                == request.Year &&
            ms.Period              == request.Period, ct);

        if (duplicate)
            throw new InvalidOperationException(
                "Ya existe una cuota de mercado con el mismo SCRAP, categoría, CCAA, año y periodo.");

        entity.Category            = request.Category;
        entity.AutonomousCommunity = request.AutonomousCommunity;
        entity.Year                = request.Year;
        entity.Weight              = request.Weight;
        entity.Period              = request.Period;
        entity.EffectiveFrom       = request.EffectiveFrom;
        entity.EffectiveTo         = request.EffectiveTo;
        entity.FlowType            = request.FlowType;
        entity.Version++;

        await _context.SaveChangesAsync(ct);
    }
}

// ── Eliminar cuota ────────────────────────────────────────────────────────────

public sealed record DeleteMarketShareCommand(Guid Id) : IRequest;

public sealed class DeleteMarketShareCommandHandler : IRequestHandler<DeleteMarketShareCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public DeleteMarketShareCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task Handle(DeleteMarketShareCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsInProfile(ProfileConstants.Admin))
            throw new UnauthorizedAccessException("Solo el administrador puede eliminar cuotas de mercado.");

        var entity = await _context.MarketShares
            .FirstOrDefaultAsync(ms => ms.Id == request.Id && ms.OwnerId == _currentUser.OwnerId, ct)
            ?? throw new KeyNotFoundException($"Cuota de mercado {request.Id} no encontrada.");

        _context.MarketShares.Remove(entity);
        await _context.SaveChangesAsync(ct);
    }
}
