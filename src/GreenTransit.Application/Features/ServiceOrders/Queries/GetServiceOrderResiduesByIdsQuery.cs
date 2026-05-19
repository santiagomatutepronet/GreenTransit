using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.ServiceOrders.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.ServiceOrders.Queries;

/// <summary>
/// Devuelve las líneas de residuo de un conjunto de Órdenes de Servicio en una
/// única consulta. Diseñado para el formulario de alta de Traslado, que necesita
/// expandir varias SOs seleccionadas a la vez sin pagar N consultas
/// "GetServiceOrderById" con Includes pesados.
/// </summary>
public sealed record GetServiceOrderResiduesByIdsQuery(IReadOnlyCollection<Guid> ServiceOrderIds)
    : IRequest<IReadOnlyList<ServiceOrderResidueDto>>;

public sealed class GetServiceOrderResiduesByIdsQueryHandler
    : IRequestHandler<GetServiceOrderResiduesByIdsQuery, IReadOnlyList<ServiceOrderResidueDto>>
{
    private readonly IApplicationDbContext _context;

    public GetServiceOrderResiduesByIdsQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<IReadOnlyList<ServiceOrderResidueDto>> Handle(
        GetServiceOrderResiduesByIdsQuery request, CancellationToken ct)
    {
        if (request.ServiceOrderIds.Count == 0)
            return [];

        var ids = request.ServiceOrderIds.ToArray();

        return await _context.ServiceOrderResidues
            .AsNoTracking()
            .Where(r => ids.Contains(r.IdServiceOrder))
            .OrderBy(r => r.IdServiceOrder)
            .ThenBy(r => r.SortOrder)
            .Select(r => new ServiceOrderResidueDto(
                r.Id,
                r.IdServiceOrder,
                r.SortOrder,
                r.IdLERCode,
                r.LerCode != null ? r.LerCode.Code : null,
                r.LerCode != null ? r.LerCode.Description : null,
                r.LerCode != null && r.LerCode.IsDangerous,
                r.ProductUse,
                r.ProductCategory,
                r.EstimatedWeight,
                r.MeasureUnit,
                r.Units))
            .ToListAsync(ct);
    }
}
