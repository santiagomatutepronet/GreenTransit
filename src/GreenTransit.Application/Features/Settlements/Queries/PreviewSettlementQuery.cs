using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Settlements.DTOs;
using GreenTransit.Domain.Entities;
using GreenTransit.Domain.Exceptions;
using MediatR;

namespace GreenTransit.Application.Features.Settlements.Queries;

// ── PreviewSettlementQuery — calcula sin persistir ────────────────────────────

/// <summary>
/// Previsualiza (dry-run) el cálculo de una liquidación para un acuerdo, año y mes
/// sin persistir ningún dato en la base de datos.
///
/// Para generar y persistir la liquidación, usar
/// <see cref="GenerateSettlementCommand"/>.
/// </summary>
public sealed record PreviewSettlementQuery(
    Guid AgreementId,
    int  Year,
    int  Month
) : IRequest<SettlementDetailDto>;

public sealed class PreviewSettlementQueryHandler
    : IRequestHandler<PreviewSettlementQuery, SettlementDetailDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public PreviewSettlementQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<SettlementDetailDto> Handle(
        PreviewSettlementQuery request, CancellationToken ct)
    {
        var ownerId = _currentUser.OwnerId;

        var agreement = await _context.Agreements
            .Include(a => a.Scrap)
            .Include(a => a.PublicEntity)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.AgreementId, ct)
            ?? throw new DomainException($"Acuerdo {request.AgreementId} no encontrado.");

        if (agreement.Status != Agreement.Statuses.Active)
            throw new DomainException("Solo se pueden previsualizar liquidaciones para acuerdos activos.");

        var calc = await SettlementCalculationHelper.ComputeAsync(
            _context, agreement, request.Year, request.Month, ownerId, ct);

        return SettlementCalculationHelper.BuildDetailDto(
            id:          Guid.Empty,
            number:      "PREVIEW",
            status:      "Preview",
            agreement:   agreement,
            year:        request.Year,
            month:       request.Month,
            baseAmount:  calc.BaseAmount,
            adjustments: calc.AdjustmentsAmount,
            tax:         calc.TaxAmount,
            total:       calc.TotalAmount,
            currency:    calc.Currency,
            lineDtos:    calc.Lines.Select(x => x.Dto).ToList());
    }
}
