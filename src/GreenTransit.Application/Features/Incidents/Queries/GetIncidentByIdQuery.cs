using System.Text.Json;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Incidents.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.Incidents.Queries;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetIncidentByIdQuery(Guid Id) : IRequest<IncidentDetailDto?>;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetIncidentByIdQueryHandler
    : IRequestHandler<GetIncidentByIdQuery, IncidentDetailDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    private static readonly JsonSerializerOptions _jsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    public GetIncidentByIdQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<IncidentDetailDto?> Handle(
        GetIncidentByIdQuery request, CancellationToken ct)
    {
        var ownerId = _currentUser.OwnerId;

        var incident = await _context.Incidents
            .AsNoTracking()
            .Where(i => i.Id == request.Id &&
                        (ownerId == Guid.Empty || i.OwnerId == ownerId))
            .FirstOrDefaultAsync(ct);

        if (incident is null) return null;

        string? wasteMoveStatus = null;
        if (!string.IsNullOrWhiteSpace(incident.WasteMoveReference))
        {
            wasteMoveStatus = await _context.WasteMoves
                .AsNoTracking()
                .Where(w => w.WasteMoveReference == incident.WasteMoveReference &&
                            (ownerId == Guid.Empty || w.OwnerId == ownerId))
                .Select(w => w.ServiceStatus)
                .FirstOrDefaultAsync(ct);
        }

        IncidentResolutionDto? resolution = null;
        if (!string.IsNullOrWhiteSpace(incident.ResolutionJson))
        {
            try
            {
                resolution = JsonSerializer.Deserialize<IncidentResolutionDto>(
                    incident.ResolutionJson, _jsonOptions);
            }
            catch { /* JSON malformado: se devuelve null */ }
        }

        return new IncidentDetailDto(
            incident.Id,
            incident.OwnerId,
            incident.Type,
            incident.Severity,
            incident.ClosedAt == null,
            incident.OpenedAt,
            incident.ClosedAt,
            incident.ServiceOrderId,
            incident.WasteMoveReference,
            wasteMoveStatus,
            incident.TicketScale,
            incident.ReportedByName,
            incident.ReportedByNationalId,
            incident.ReportedByCenterCode,
            incident.Description,
            resolution,
            incident.Version,
            incident.Hash,
            incident.CreatedAt,
            incident.UpdatedAt,
            incident.IdUser);
    }
}
