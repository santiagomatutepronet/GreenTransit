using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Settlements.DTOs;
using GreenTransit.Domain.Authorization;
using GreenTransit.Domain.Entities;
using GreenTransit.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.Settlements.Commands;

// ── Constantes de estado ──────────────────────────────────────────────────────

file static class SettlementStatus
{
    public const string Pending  = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

// ── GenerateSettlementCommand ─────────────────────────────────────────────────

/// <summary>
/// Genera (o previsualiza) la liquidación para un acuerdo, año y mes dados.
/// Si <paramref name="DryRun"/> es true, devuelve el resultado calculado sin persistir.
/// </summary>
public sealed record GenerateSettlementCommand(
    Guid AgreementId,
    int  Year,
    int  Month,
    bool DryRun = false
) : IRequest<SettlementDetailDto>;

public sealed class GenerateSettlementCommandHandler
    : IRequestHandler<GenerateSettlementCommand, SettlementDetailDto>
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

    public async Task<SettlementDetailDto> Handle(
        GenerateSettlementCommand request, CancellationToken ct)
    {
        // Paso 1 — Validar acuerdo activo y unicidad
        var agreement = await _context.Agreements
            .Include(a => a.Scrap)
            .Include(a => a.PublicEntity)
            .FirstOrDefaultAsync(a => a.Id == request.AgreementId, ct)
            ?? throw new DomainException($"Acuerdo {request.AgreementId} no encontrado.");

        if (agreement.Status != Agreement.Statuses.Active)
            throw new DomainException("Solo se pueden generar liquidaciones para acuerdos activos.");

        if (!request.DryRun)
        {
            var duplicate = await _context.Settlements.AnyAsync(s =>
                s.AgreementId == request.AgreementId &&
                s.Year        == request.Year        &&
                s.Month       == request.Month       &&
                (s.Status == SettlementStatus.Pending || s.Status == SettlementStatus.Approved), ct);

            if (duplicate)
                throw new DomainException(
                    $"Ya existe una liquidación en estado Pending o Approved para el acuerdo {agreement.AgreementNumber} en {request.Year}/{request.Month:D2}.");
        }

        // Paso 2 — Recuperar EntryPlants del periodo dentro del ámbito del acuerdo
        var periodStart = new DateTime(request.Year, request.Month, 1);
        var periodEnd   = periodStart.AddMonths(1);

        // Obtenemos los WasteMoveIds vinculados al ámbito del acuerdo (mismo OwnerId)
        var ownerId = _currentUser.OwnerId;

        var entryPlants = await _context.EntryPlants
            .AsNoTracking()
            .Include(ep => ep.EntryPlantResidues)
                .ThenInclude(r => r.Residue)
                    .ThenInclude(r => r!.LerCode)
            .Where(ep =>
                ep.OwnerId == ownerId &&
                ep.PlantEntryDate >= periodStart &&
                ep.PlantEntryDate < periodEnd)
            .ToListAsync(ct);

        // Paso 3 — Agrupar por IdLERCode × ProductCategory y sumar NetWeight de EntryPlant
        // Fuente de verdad: EntryPlants.NetWeight
        var groups = entryPlants
            .SelectMany(ep => ep.EntryPlantResidues
                .Select(r => new
                {
                    ep.Id,
                    ep.NetWeight,
                    IdLERCode       = r.Residue?.IdLERCode,
                    ProductCategory = r.Residue?.ProductCategory,
                    LerCode         = r.Residue?.LerCode
                }))
            .GroupBy(x => new { x.IdLERCode, x.ProductCategory })
            .Select(g => new
            {
                g.Key.IdLERCode,
                g.Key.ProductCategory,
                WeightKg  = g.Sum(x => x.NetWeight ?? 0m),
                LerCode   = g.FirstOrDefault(x => x.LerCode != null)?.LerCode,
                SourceIds = g.Select(x => x.Id).Distinct().ToList()
            })
            .ToList();

        // Paso 4 — Aplicar TariffRulesJson y MinimumsJson
        var tariffRules = ParseJsonDict(agreement.TariffRulesJson);
        var minimums    = ParseJsonDict(agreement.MinimumsJson);
        var currency    = agreement.Currency ?? "EUR";

        // Líneas con datos LER disponibles para la previsualización y persistencia
        var lineData = groups.Select(g =>
        {
            var key        = g.IdLERCode?.ToString() ?? $"cat:{g.ProductCategory}";
            var pricePerKg = tariffRules.TryGetValue(key, out var p) ? p : 0m;
            var minWeight  = minimums.TryGetValue(key, out var m)    ? m : 0m;
            var effectiveWeight = Math.Max(g.WeightKg, minWeight);
            var amount     = effectiveWeight * pricePerKg;

            // ProductCategory en Residue es string?; en SettlementLine es int?
            int? productCategoryInt = int.TryParse(g.ProductCategory, out var parsed) ? parsed : null;

            var lineId = Guid.NewGuid();
            var entity = new SettlementLine
            {
                Id              = lineId,
                IdLERCode       = g.IdLERCode,
                ProductCategory = productCategoryInt,
                WeightKg        = g.WeightKg,
                PricePerKg      = pricePerKg,
                Amount          = amount,
                EvidenceType    = "EntryPlant",
                SourceIdsJson   = JsonSerializer.Serialize(g.SourceIds)
            };
            var dto = new SettlementLineDto(
                lineId,
                productCategoryInt,
                g.IdLERCode,
                g.LerCode?.Code,
                g.LerCode?.Description,
                g.WeightKg,
                pricePerKg,
                amount,
                "EntryPlant",
                entity.SourceIdsJson);

            return (Entity: entity, Dto: dto);
        }).ToList();

        var lines    = lineData.Select(x => x.Entity).ToList();
        var lineDtos = lineData.Select(x => x.Dto).ToList();

        // Paso 5 — Calcular cabecera
        var baseAmount        = lines.Sum(l => l.Amount);
        var adjustmentsAmount = 0m; // eco-modulación: sin reglas configuradas por defecto
        const decimal taxRate = 0.21m;
        var taxAmount         = (baseAmount + adjustmentsAmount) * taxRate;
        var totalAmount       = baseAmount + adjustmentsAmount + taxAmount;

        if (request.DryRun)
        {
            return BuildDetailDto(
                id:          Guid.Empty,
                number:      "PREVIEW",
                status:      "Preview",
                agreement:   agreement,
                year:        request.Year,
                month:       request.Month,
                baseAmount:  baseAmount,
                adjustments: adjustmentsAmount,
                tax:         taxAmount,
                total:       totalAmount,
                currency:    currency,
                lineDtos:    lineDtos);
        }

        // Persiste
        var settlementNumber = await GenerateSettlementNumberAsync(ct);

        var settlement = new Settlement
        {
            Id                = Guid.NewGuid(),
            OwnerId           = ownerId,
            SettlementNumber  = settlementNumber,
            Status            = SettlementStatus.Pending,
            AgreementId       = agreement.Id,
            Year              = request.Year,
            Month             = request.Month,
            IdScrap           = agreement.IdScrap,
            IdPublicEntity    = agreement.IdPublicEntity,
            Currency          = currency,
            BaseAmount        = baseAmount,
            AdjustmentsAmount = adjustmentsAmount,
            TaxAmount         = taxAmount,
            TotalAmount       = totalAmount,
            Version           = 1,
            CreatedAt         = DateTime.UtcNow,
            UpdatedAt         = DateTime.UtcNow,
            IdUser            = _currentUser.IdUser
        };

        foreach (var line in lines)
        {
            line.SettlementId = settlement.Id;
        }

        settlement.Hash = ComputeHash(settlement);
        settlement.SettlementLines = lines;

        _context.Settlements.Add(settlement);
        await _context.SaveChangesAsync(ct);

        return BuildDetailDto(
            id:          settlement.Id,
            number:      settlement.SettlementNumber,
            status:      settlement.Status,
            agreement:   agreement,
            year:        request.Year,
            month:       request.Month,
            baseAmount:  baseAmount,
            adjustments: adjustmentsAmount,
            tax:         taxAmount,
            total:       totalAmount,
            currency:    currency,
            lineDtos:    lineDtos);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static SettlementDetailDto BuildDetailDto(
        Guid           id,
        string         number,
        string         status,
        Agreement      agreement,
        int            year,
        int            month,
        decimal        baseAmount,
        decimal        adjustments,
        decimal        tax,
        decimal        total,
        string         currency,
        List<SettlementLineDto> lineDtos)
    {
        return new SettlementDetailDto(
            id,
            number,
            status,
            agreement.Id,
            agreement.AgreementNumber,
            year,
            month,
            agreement.IdScrap,
            agreement.Scrap?.Name,
            agreement.IdPublicEntity,
            agreement.PublicEntity?.Name,
            baseAmount,
            adjustments,
            tax,
            total,
            currency,
            null, null, null, null, null,
            1,
            DateTime.UtcNow,
            DateTime.UtcNow,
            lineDtos);
    }

    private async Task<string> GenerateSettlementNumberAsync(CancellationToken ct)
    {
        var year  = DateTime.UtcNow.Year;
        var count = await _context.Settlements
            .CountAsync(s => s.CreatedAt.Year == year, ct);
        return $"LIQ-{year}-{count + 1:D4}";
    }

    private static Dictionary<string, decimal> ParseJsonDict(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, decimal>>(json)
                   ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string ComputeHash(Settlement s)
    {
        var raw = $"{s.Id}|{s.AgreementId}|{s.Year}|{s.Month}|{s.TotalAmount}|{s.CreatedAt:O}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
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

        if (settlement.Status != SettlementStatus.Pending)
            throw new DomainException("Solo se pueden aprobar liquidaciones en estado Pending.");

        settlement.Status           = SettlementStatus.Approved;
        settlement.ValidationStatus = SettlementStatus.Approved;
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

        if (settlement.Status != SettlementStatus.Pending)
            throw new DomainException("Solo se pueden rechazar liquidaciones en estado Pending.");

        settlement.Status           = SettlementStatus.Rejected;
        settlement.ValidationStatus = SettlementStatus.Rejected;
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

public sealed record RecalculateSettlementCommand(Guid Id) : IRequest<SettlementDetailDto>;

public sealed class RecalculateSettlementCommandHandler
    : IRequestHandler<RecalculateSettlementCommand, SettlementDetailDto>
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

    public async Task<SettlementDetailDto> Handle(
        RecalculateSettlementCommand request, CancellationToken ct)
    {
        var settlement = await _context.Settlements
            .Include(s => s.SettlementLines)
            .FirstOrDefaultAsync(s => s.Id == request.Id, ct)
            ?? throw new DomainException($"Liquidación {request.Id} no encontrada.");

        if (settlement.Status == SettlementStatus.Approved)
            throw new DomainException("Un Settlement aprobado es inmutable y no puede recalcularse.");

        if (settlement.Status != SettlementStatus.Pending)
            throw new DomainException("Solo se pueden recalcular liquidaciones en estado Pending.");

        // Eliminar líneas actuales
        _context.SettlementLines.RemoveRange(settlement.SettlementLines);
        await _context.SaveChangesAsync(ct);

        // Volver a generar (dryRun = false, con el mismo acuerdo/año/mes)
        var generateCmd = new GenerateSettlementCommand(
            settlement.AgreementId,
            settlement.Year,
            settlement.Month ?? DateTime.UtcNow.Month,
            DryRun: false);

        // Eliminar el settlement antes de regenerar para evitar conflicto de unicidad
        _context.Settlements.Remove(settlement);
        await _context.SaveChangesAsync(ct);

        return await _mediator.Send(generateCmd, ct);
    }
}
