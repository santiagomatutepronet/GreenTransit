using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Authorization;
using GreenTransit.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.ProductDeclarations.Queries;

// ── DTO ───────────────────────────────────────────────────────────────────────

public sealed record DeclarationDashboardWidgetsDto(
    int                           IssuedPendingValidation,
    int                           TotalOwn,
    Dictionary<string, int>       OwnByState,
    decimal                       CurrentPeriodQuantity,
    int                           CurrentYear,
    int                           CurrentPeriod
);

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetDeclarationDashboardWidgetsQuery : IRequest<DeclarationDashboardWidgetsDto>;

public sealed class GetDeclarationDashboardWidgetsQueryHandler
    : IRequestHandler<GetDeclarationDashboardWidgetsQuery, DeclarationDashboardWidgetsDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GetDeclarationDashboardWidgetsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<DeclarationDashboardWidgetsDto> Handle(
        GetDeclarationDashboardWidgetsQuery request, CancellationToken ct)
    {
        var now    = DateTime.UtcNow;
        var year   = now.Year;
        var period = (now.Month - 1) / 3 + 1; // 1-4

        // ── Pendientes de validación (solo útil para ADMIN) ────────────────
        var issuedCount = 0;
        if (_currentUser.IsInProfile(ProfileConstants.Admin))
        {
            issuedCount = await _context.ProductDeclarations
                .AsNoTracking()
                .CountAsync(pd => pd.State == ProductDeclaration.States.Issued, ct);
        }

        // ── Propias del PRODUCER ───────────────────────────────────────────
        var totalOwn  = 0;
        var ownByState = new Dictionary<string, int>();
        if (_currentUser.IsInProfile(ProfileConstants.Producer)
            && _currentUser.LinkedEntityId.HasValue)
        {
            var ownId = _currentUser.LinkedEntityId.Value;
            var own   = await _context.ProductDeclarations
                .AsNoTracking()
                .Where(pd => pd.IdProducer == ownId)
                .GroupBy(pd => pd.State)
                .Select(g => new { State = g.Key ?? "Borrador", Count = g.Count() })
                .ToListAsync(ct);
            totalOwn  = own.Sum(x => x.Count);
            ownByState = own.ToDictionary(x => x.State, x => x.Count);
        }

        // ── Volumen declarado en el periodo actual ─────────────────────────
        var periodQty = await _context.Products
            .AsNoTracking()
            .Where(p => p.ProductDeclaration!.Year == year
                     && p.ProductDeclaration.Period == period
                     && (_currentUser.IsInProfile(ProfileConstants.Admin)
                         || (_currentUser.IsInProfile(ProfileConstants.Producer)
                             && p.ProductDeclaration.IdProducer == _currentUser.LinkedEntityId)))
            .SumAsync(p => (decimal?)(p.Quantity ?? 0), ct) ?? 0;

        return new DeclarationDashboardWidgetsDto(
            issuedCount, totalOwn, ownByState, periodQty, year, period);
    }
}
