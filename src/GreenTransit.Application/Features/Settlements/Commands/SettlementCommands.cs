using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Settlements.DTOs;
using GreenTransit.Domain.Authorization;
using GreenTransit.Domain.Entities;
using GreenTransit.Domain.Exceptions;
using MediatR;

namespace GreenTransit.Application.Features.Settlements.Commands;

// ── Constantes de estado ──────────────────────────────────────────────────────

internal static class SettlementStatuses
{
    public const string Pending  = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

// ── GenerateSettlementCommand — persiste y devuelve el Guid ──────────────────

/// <summary>
/// Genera y persiste la liquidación para un acuerdo, año y mes dados.
/// Devuelve el Id del <see cref="Settlement"/> creado.
/// Para previsualizar sin persistir, usar PreviewSettlementQuery.
/// </summary>
public sealed record GenerateSettlementCommand(
    Guid AgreementId,
    int  Year,
    int  Month
) : IRequest<Guid>, ITransactional;

public sealed class GenerateSettlementCommandHandler
    : IRequestHandler<GenerateSettlementCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public GenerateSettlementCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(GenerateSettlementCommand request, CancellationToken ct)
    {
        var ownerId = _currentUser.OwnerId;

        var agreement = await _context.Agreements
            .Include(a => a.Scrap)
            .Include(a => a.PublicEntity)
            .FirstOrDefaultAsync(a => a.Id == request.AgreementId, ct)
            ?? throw new DomainException($"Acuerdo {request.AgreementId} no encontrado.");

        if (agreement.Status != Agreement.Statuses.Active)
            throw new DomainException("Solo se pueden generar liquidaciones para acuerdos activos.");

        var duplicate = await _context.Settlements.AnyAsync(s =>
            s.AgreementId == request.AgreementId &&
            s.Year        == request.Year        &&
            s.Month       == request.Month       &&
            (s.Status == SettlementStatuses.Pending || s.Status == SettlementStatuses.Approved), ct);

        if (duplicate)
            throw new DomainException(
                $"Ya existe una liquidación en estado Pending o Approved para el acuerdo " +
                $"{agreement.AgreementNumber} en {request.Year}/{request.Month:D2}.");

        var calc   = await SettlementCalculationHelper.ComputeAsync(_context, agreement, request.Year, request.Month, ownerId, ct);
        var number = await SettlementCalculationHelper.GenerateSettlementNumberAsync(_context, ct);

        var settlement = new Settlement
        {
            Id                = Guid.NewGuid(),
            OwnerId           = ownerId,
            SettlementNumber  = number,
            Status            = SettlementStatuses.Pending,
            AgreementId       = agreement.Id,
            Year              = request.Year,
            Month             = request.Month,
            IdScrap           = agreement.IdScrap,
            IdPublicEntity    = agreement.IdPublicEntity,
            Currency          = calc.Currency,
            BaseAmount        = calc.BaseAmount,
            AdjustmentsAmount = calc.AdjustmentsAmount,
            TaxAmount         = calc.TaxAmount,
            TotalAmount       = calc.TotalAmount,
            Version           = 1,
            CreatedAt         = DateTime.UtcNow,
            UpdatedAt         = DateTime.UtcNow,
            IdUser            = _currentUser.IdUser
        };

        foreach (var line in calc.Lines.Select(x => x.Entity))
            line.SettlementId = settlement.Id;

        settlement.Hash            = SettlementCalculationHelper.ComputeHash(settlement);
        settlement.SettlementLines = calc.Lines.Select(x => x.Entity).ToList();

        _context.Add(settlement);
        await _context.SaveChangesAsync(ct);

        return settlement.Id;
    }
}

// ── ApproveSettlementCommand ──────────────────────────────────────────────────

public sealed record ApproveSettlementCommand(Guid Id) : IRequest<Unit>;

public sealed class ApproveSettlementCommandHandler
    : IRequestHandler<ApproveSettlementCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public ApproveSettlementCommandHandler(
        IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(ApproveSettlementCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsInAnyProfile(ProfileConstants.Scrap, ProfileConstants.Admin))
            throw new UnauthorizedAccessException("Solo SCRAP o ADMIN pueden aprobar liquidaciones.");

        var settlement = await _context.Settlements
            .FirstOrDefaultAsync(s => s.Id == request.Id, ct)
            ?? throw new DomainException($"Liquidación {request.Id} no encontrada.");

        if (settlement.Status != SettlementStatuses.Pending)
            throw new DomainException("Solo se pueden aprobar liquidaciones en estado Pending.");

        settlement.Status           = SettlementStatuses.Approved;
        settlement.ValidationStatus = SettlementStatuses.Approved;
        settlement.ValidatedAt      = DateTime.UtcNow;
        settlement.Validator        = _currentUser.UserName;
        settlement.UpdatedAt        = DateTime.UtcNow;
        settlement.Version++;

        await _context.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ── RejectSettlementCommand ───────────────────────────────────────────────────

public sealed record RejectSettlementCommand(Guid Id, string Reason) : IRequest<Unit>;

public sealed class RejectSettlementCommandHandler
    : IRequestHandler<RejectSettlementCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public RejectSettlementCommandHandler(
        IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(RejectSettlementCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsInAnyProfile(ProfileConstants.Scrap, ProfileConstants.Admin))
            throw new UnauthorizedAccessException("Solo SCRAP o ADMIN pueden rechazar liquidaciones.");

        var settlement = await _context.Settlements
            .FirstOrDefaultAsync(s => s.Id == request.Id, ct)
            ?? throw new DomainException($"Liquidación {request.Id} no encontrada.");

        if (settlement.Status != SettlementStatuses.Pending)
            throw new DomainException("Solo se pueden rechazar liquidaciones en estado Pending.");

        settlement.Status           = SettlementStatuses.Rejected;
        settlement.ValidationStatus = SettlementStatuses.Rejected;
        settlement.ValidationRef    = request.Reason;
        settlement.ValidatedAt      = DateTime.UtcNow;
        settlement.Validator        = _currentUser.UserName;
        settlement.UpdatedAt        = DateTime.UtcNow;
        settlement.Version++;

        await _context.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

// ── RecalculateSettlementCommand ──────────────────────────────────────────────

public sealed record RecalculateSettlementCommand(Guid Id) : IRequest<Guid>, ITransactional;

public sealed class RecalculateSettlementCommandHandler
    : IRequestHandler<RecalculateSettlementCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;
    private readonly IMediator             _mediator;

    public RecalculateSettlementCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser,
        IMediator             mediator)
    {
        _context     = context;
        _currentUser = currentUser;
        _mediator    = mediator;
    }

    public async Task<Guid> Handle(
        RecalculateSettlementCommand request, CancellationToken ct)
    {
        var settlement = await _context.Settlements
            .Include(s => s.SettlementLines)
            .FirstOrDefaultAsync(s => s.Id == request.Id, ct)
            ?? throw new DomainException($"Liquidación {request.Id} no encontrada.");

        if (settlement.Status == SettlementStatuses.Approved)
            throw new DomainException("Un Settlement aprobado es inmutable y no puede recalcularse.");

        if (settlement.Status != SettlementStatuses.Pending)
            throw new DomainException("Solo se pueden recalcular liquidaciones en estado Pending.");

        var agreementId = settlement.AgreementId;
        var year        = settlement.Year;
        var month       = settlement.Month ?? DateTime.UtcNow.Month;

        _context.RemoveRange(settlement.SettlementLines);
        _context.Remove(settlement);
        await _context.SaveChangesAsync(ct);

        return await _mediator.Send(
            new GenerateSettlementCommand(agreementId, year, month), ct);
    }
}
