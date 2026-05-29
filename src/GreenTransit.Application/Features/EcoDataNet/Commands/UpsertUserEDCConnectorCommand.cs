using FluentValidation;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Authorization;
using GreenTransit.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.EcoDataNet.Commands;

// ── Comando ───────────────────────────────────────────────────────────────────

/// <summary>
/// Crea o actualiza la configuración del conector EDC de un usuario.
/// ADMIN puede gestionar cualquier usuario del tenant.
/// NO ADMIN solo puede gestionar su propio registro.
/// </summary>
public sealed record UpsertUserEDCConnectorCommand : IRequest<int>
{
    public int    UserId         { get; init; }
    public string EDCServerName  { get; init; } = string.Empty;
    public string EDCConnectorId { get; init; } = string.Empty;
    public string? ApiKey        { get; init; }
}

// ── Validación ────────────────────────────────────────────────────────────────

public sealed class UpsertUserEDCConnectorValidator
    : AbstractValidator<UpsertUserEDCConnectorCommand>
{
    public UpsertUserEDCConnectorValidator()
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage("El identificador de usuario es obligatorio.");

        RuleFor(x => x.EDCServerName)
            .NotEmpty().WithMessage("El nombre del servidor EDC es obligatorio.")
            .MaximumLength(255).WithMessage("El nombre del servidor EDC no puede superar 255 caracteres.");

        RuleFor(x => x.EDCConnectorId)
            .NotEmpty().WithMessage("El identificador del conector EDC es obligatorio.")
            .MaximumLength(255).WithMessage("El identificador del conector no puede superar 255 caracteres.");

        RuleFor(x => x.ApiKey)
            .MaximumLength(500).WithMessage("La API Key no puede superar 500 caracteres.")
            .When(x => x.ApiKey is not null);
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class UpsertUserEDCConnectorCommandHandler
    : IRequestHandler<UpsertUserEDCConnectorCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public UpsertUserEDCConnectorCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(UpsertUserEDCConnectorCommand request, CancellationToken ct)
    {
        var ownerId = _currentUser.OwnerId;

        // NO ADMIN solo puede modificar su propio conector
        if (!_currentUser.IsInProfile(ProfileConstants.Admin)
            && request.UserId != _currentUser.IdUser)
            throw new UnauthorizedAccessException(
                "No tiene permisos para modificar la configuración EDC de otro usuario.");

        // Verificar que el usuario pertenece al mismo tenant
        var userExists = await _context.AppUsers
            .AnyAsync(u => u.Id == request.UserId && u.OwnerId == ownerId, ct);

        if (!userExists)
            throw new KeyNotFoundException($"Usuario {request.UserId} no encontrado en este tenant.");

        var existing = await _context.UserEDCConnectors
            .FirstOrDefaultAsync(c => c.UserId == request.UserId, ct);

        if (existing is not null)
        {
            existing.EDCServerName  = request.EDCServerName;
            existing.EDCConnectorId = request.EDCConnectorId;
            existing.ApiKey         = request.ApiKey;
            await _context.SaveChangesAsync(ct);
            return existing.Id;
        }

        var newConnector = new UserEDCConnector
        {
            UserId         = request.UserId,
            EDCServerName  = request.EDCServerName,
            EDCConnectorId = request.EDCConnectorId,
            ApiKey         = request.ApiKey
        };

        _context.Add(newConnector);
        await _context.SaveChangesAsync(ct);
        return newConnector.Id;
    }
}
