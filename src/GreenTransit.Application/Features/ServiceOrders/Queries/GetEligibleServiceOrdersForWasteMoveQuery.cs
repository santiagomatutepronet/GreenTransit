using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.ServiceOrders.DTOs;
using GreenTransit.Domain.Authorization;
using GreenTransit.Domain.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.ServiceOrders.Queries;

/// <summary>
/// Devuelve únicamente las Órdenes de Servicio elegibles para agruparse en un
/// nuevo Traslado (estado Pendiente/Planificada y sin traslado asignado).
///
/// Versión ligera de <see cref="GetServiceOrdersQuery"/> pensada para el
/// dropdown de "/waste-moves/new": proyecta sólo los campos necesarios,
/// sin Count ni subconsultas correlacionadas (Sum/Join de residuos).
/// </summary>
public sealed record GetEligibleServiceOrdersForWasteMoveQuery(int MaxItems = 200)
    : IRequest<IReadOnlyList<EligibleServiceOrderDto>>;

public sealed record EligibleServiceOrderDto(
    Guid     Id,
    string   ServiceOrderNumber,
    Guid?    IdPickupPoint,
    Guid?    IdLERCode,
    string?  LerCodeCode,
    string?  LerCodeDescription,
    decimal? EstimatedWeight);

public sealed class GetEligibleServiceOrdersForWasteMoveQueryHandler
    : IRequestHandler<GetEligibleServiceOrdersForWasteMoveQuery, IReadOnlyList<EligibleServiceOrderDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetEligibleServiceOrdersForWasteMoveQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<EligibleServiceOrderDto>> Handle(
        GetEligibleServiceOrdersForWasteMoveQuery request, CancellationToken ct)
    {
        var ownerId        = _currentUser.OwnerId;
        var linkedEntityId = _currentUser.LinkedEntityId;

        var q = _context.ServiceOrders.AsNoTracking()
            .Where(s => ownerId == Guid.Empty || s.OwnerId == ownerId)
            .Where(s => s.Status == ServiceOrderStatuses.Pending
                     || s.Status == ServiceOrderStatuses.Scheduled)
            .Where(s => s.WasteMoveReference == null || s.WasteMoveReference == "");

        // Aplica los filtros mínimos de perfil que afectan al alta de un traslado.
        // Mantener consistencia con GetServiceOrdersQueryHandler para los perfiles
        // que pueden crear traslados.
        if (_currentUser.IsInProfile(ProfileConstants.Producer) ||
            _currentUser.IsInProfile(ProfileConstants.PublicEnt))
        {
            q = q.Where(s => s.IdIssuedBy == linkedEntityId);
        }
        else if (_currentUser.IsInProfile(ProfileConstants.Scrap))
        {
            // SCRAPs: SOs aún sin traslado asignado (ya filtrado arriba).
        }

        var max = Math.Clamp(request.MaxItems, 1, 500);

        return await q
            .OrderByDescending(s => s.IssuedAt)
            .Take(max)
            .Select(s => new EligibleServiceOrderDto(
                s.Id,
                s.ServiceOrderNumber,
                s.IdPickupPoint,
                s.IdLERCode,
                s.LerCode != null ? s.LerCode.Code : null,
                s.LerCode != null ? s.LerCode.Description : null,
                s.EstimatedWeight))
            .ToListAsync(ct);
    }
}
