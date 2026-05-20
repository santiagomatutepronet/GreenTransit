using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GreenTransit.Application.Features.Entities.Commands;

// ── Resultado ─────────────────────────────────────────────────────────────────
public sealed record DeactivateEntityResult(bool HasLinkedUser, string? LinkedUserLogin);

// ── Comando ───────────────────────────────────────────────────────────────────
public sealed record DeactivateEntityCommand(Guid Id) : IRequest<DeactivateEntityResult>;

// ── Handler ───────────────────────────────────────────────────────────────────
public sealed class DeactivateEntityCommandHandler
    : IRequestHandler<DeactivateEntityCommand, DeactivateEntityResult>
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<DeactivateEntityCommandHandler> _logger;

    public DeactivateEntityCommandHandler(
        IApplicationDbContext context,
        ILogger<DeactivateEntityCommandHandler> logger)
    {
        _context = context;
        _logger  = logger;
    }

    public async Task<DeactivateEntityResult> Handle(
        DeactivateEntityCommand request, CancellationToken ct)
    {
        var entity = await _context.BusinessEntities
            .FirstOrDefaultAsync(e => e.Id == request.Id, ct)
            ?? throw new DomainException($"Entidad {request.Id} no encontrada.");

        entity.IsActive = false;
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Entidad {Id} desactivada.", entity.Id);

        // Busca usuario vinculado para informar a la UI
        string? linkedLogin = null;
        if (!string.IsNullOrWhiteSpace(entity.Email))
        {
            linkedLogin = await _context.AppUsers
                .IgnoreQueryFilters()
                .Where(u => u.Email == entity.Email || u.Login == entity.Email)
                .Select(u => (string?)u.Login)
                .FirstOrDefaultAsync(ct);
        }

        return new DeactivateEntityResult(linkedLogin is not null, linkedLogin);
    }
}
