using FluentValidation;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Authorization;
using GreenTransit.Domain.Services;
using MediatR;

namespace GreenTransit.Application.Features.ProductDeclarations.Commands;

// ── Emitir declaración (Borrador → Emitido) ───────────────────────────────────

public sealed record EmitProductDeclarationCommand(Guid Id) : IRequest;

public sealed class EmitProductDeclarationCommandHandler
    : IRequestHandler<EmitProductDeclarationCommand>
{
    private readonly IApplicationDbContext                       _context;
    private readonly ICurrentUserService                         _currentUser;
    private readonly ProductDeclarationStateService              _stateService;
    private readonly IProductDeclarationNotificationService      _notifier;

    public EmitProductDeclarationCommandHandler(
        IApplicationDbContext                  context,
        ICurrentUserService                    currentUser,
        ProductDeclarationStateService         stateService,
        IProductDeclarationNotificationService notifier)
    {
        _context      = context;
        _currentUser  = currentUser;
        _stateService = stateService;
        _notifier     = notifier;
    }

    public async Task Handle(EmitProductDeclarationCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsInAnyProfile(ProfileConstants.Admin, ProfileConstants.Producer))
            throw new UnauthorizedAccessException("No tienes permiso para emitir declaraciones.");

        var declaration = await _context.ProductDeclarations
            .Include(pd => pd.Products)
            .Include(pd => pd.Producer)
            .FirstOrDefaultAsync(pd => pd.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Declaración {request.Id} no encontrada.");

        if (_currentUser.IsInProfile(ProfileConstants.Producer)
            && declaration.IdProducer != _currentUser.LinkedEntityId)
            throw new UnauthorizedAccessException(
                "No tienes permiso para emitir esta declaración.");

        _stateService.Emit(declaration);
        declaration.IdUser = _currentUser.IdUser;

        await _context.SaveChangesAsync(ct);

        await _notifier.NotifyIssuedAsync(
            declaration.Id,
            declaration.Producer?.Name,
            declaration.Year,
            declaration.Period,
            ct);
    }
}

// ── Validar declaración (Emitido → Validado) ──────────────────────────────────

public sealed record ValidateProductDeclarationCommand(Guid Id) : IRequest;

public sealed class ValidateProductDeclarationCommandHandler
    : IRequestHandler<ValidateProductDeclarationCommand>
{
    private readonly IApplicationDbContext                       _context;
    private readonly ICurrentUserService                         _currentUser;
    private readonly ProductDeclarationStateService              _stateService;
    private readonly IProductDeclarationNotificationService      _notifier;

    public ValidateProductDeclarationCommandHandler(
        IApplicationDbContext                  context,
        ICurrentUserService                    currentUser,
        ProductDeclarationStateService         stateService,
        IProductDeclarationNotificationService notifier)
    {
        _context      = context;
        _currentUser  = currentUser;
        _stateService = stateService;
        _notifier     = notifier;
    }

    public async Task Handle(ValidateProductDeclarationCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsInProfile(ProfileConstants.Admin))
            throw new UnauthorizedAccessException(
                "Solo los administradores pueden validar declaraciones.");

        var declaration = await _context.ProductDeclarations
            .Include(pd => pd.Products)
            .FirstOrDefaultAsync(pd => pd.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Declaración {request.Id} no encontrada.");

        _stateService.Validate(declaration);
        declaration.IdUser = _currentUser.IdUser;

        await _context.SaveChangesAsync(ct);

        await _notifier.NotifyValidatedAsync(
            declaration.Id, declaration.IdProducer, declaration.Reference, ct);
    }
}

// ── Rechazar declaración (Emitido → Rechazado) ────────────────────────────────

public sealed record RejectProductDeclarationCommand(Guid Id, string Reason) : IRequest;

public sealed class RejectProductDeclarationCommandHandler
    : IRequestHandler<RejectProductDeclarationCommand>
{
    private readonly IApplicationDbContext                       _context;
    private readonly ICurrentUserService                         _currentUser;
    private readonly ProductDeclarationStateService              _stateService;
    private readonly IProductDeclarationNotificationService      _notifier;

    public RejectProductDeclarationCommandHandler(
        IApplicationDbContext                  context,
        ICurrentUserService                    currentUser,
        ProductDeclarationStateService         stateService,
        IProductDeclarationNotificationService notifier)
    {
        _context      = context;
        _currentUser  = currentUser;
        _stateService = stateService;
        _notifier     = notifier;
    }

    public async Task Handle(RejectProductDeclarationCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsInProfile(ProfileConstants.Admin))
            throw new UnauthorizedAccessException(
                "Solo los administradores pueden rechazar declaraciones.");

        var declaration = await _context.ProductDeclarations
            .Include(pd => pd.Products)
            .FirstOrDefaultAsync(pd => pd.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Declaración {request.Id} no encontrada.");

        _stateService.Reject(declaration, request.Reason);
        declaration.IdUser = _currentUser.IdUser;

        await _context.SaveChangesAsync(ct);

        await _notifier.NotifyRejectedAsync(
            declaration.Id, declaration.IdProducer, declaration.Reference, request.Reason, ct);
    }
}

public sealed class RejectProductDeclarationCommandValidator
    : AbstractValidator<RejectProductDeclarationCommand>
{
    public RejectProductDeclarationCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("El motivo de rechazo es obligatorio.")
            .MinimumLength(10).WithMessage("El motivo debe tener al menos 10 caracteres.");
    }
}
