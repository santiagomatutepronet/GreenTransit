using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Settlements.DTOs;
using GreenTransit.Domain.Entities;

namespace GreenTransit.Application.Features.Settlements;

/// <summary>
/// Lógica de cálculo de liquidación compartida entre GenerateSettlementCommand
/// y PreviewSettlementQuery.
/// </summary>
internal static class SettlementCalculationHelper
{
    internal sealed record CalculationResult(
        decimal BaseAmount,
        decimal AdjustmentsAmount,
        decimal TaxAmount,
        decimal TotalAmount,
        string  Currency,
        List<(SettlementLine Entity, SettlementLineDto Dto)> Lines);

    internal static async Task<CalculationResult> ComputeAsync(
        IApplicationDbContext context,
        Agreement             agreement,
        int                   year,
        int                   month,
        Guid                  ownerId,
        CancellationToken     ct)
    {
        var periodStart = new DateTime(year, month, 1);
        var periodEnd   = periodStart.AddMonths(1);

        // Proyección directa — evita cargar Residue completo con CompositionJson/PotentialLERCodesJson
        var flatResidues = await context.EntryPlants
            .AsNoTracking()
            .Where(ep =>
                ep.OwnerId == ownerId &&
                ep.PlantEntryDate >= periodStart &&
                ep.PlantEntryDate < periodEnd)
            .SelectMany(ep => ep.EntryPlantResidues, (ep, r) => new
            {
                SourceId        = ep.Id,
                ep.NetWeight,
                IdLERCode       = r.Residue != null ? r.Residue.IdLERCode       : (Guid?)null,
                ProductCategory = r.Residue != null ? r.Residue.ProductCategory : null,
                LerCode         = r.Residue != null && r.Residue.LerCode != null
                                    ? r.Residue.LerCode.Code        : null,
                LerDescription  = r.Residue != null && r.Residue.LerCode != null
                                    ? r.Residue.LerCode.Description : null
            })
            .ToListAsync(ct);

        var groups = flatResidues
            .GroupBy(x => new { x.IdLERCode, x.ProductCategory })
            .Select(g => new
            {
                g.Key.IdLERCode,
                g.Key.ProductCategory,
                WeightKg    = g.Sum(x => x.NetWeight ?? 0m),
                LerCode     = g.FirstOrDefault(x => x.LerCode != null)?.LerCode,
                LerDescr    = g.FirstOrDefault(x => x.LerDescription != null)?.LerDescription,
                SourceIds   = g.Select(x => x.SourceId).Distinct().ToList()
            })
            .ToList();

        var tariffRules = ParseJsonDict(agreement.TariffRulesJson);
        var minimums    = ParseJsonDict(agreement.MinimumsJson);
        var currency    = agreement.Currency ?? "EUR";

        var lineData = groups.Select(g =>
        {
            var key        = g.IdLERCode?.ToString() ?? $"cat:{g.ProductCategory}";
            var pricePerKg = tariffRules.TryGetValue(key, out var p) ? p : 0m;
            var minWeight  = minimums.TryGetValue(key, out var m)    ? m : 0m;
            var effectiveWeight = Math.Max(g.WeightKg, minWeight);
            var amount     = effectiveWeight * pricePerKg;
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
                g.LerCode,
                g.LerDescr,
                g.WeightKg,
                pricePerKg,
                amount,
                "EntryPlant",
                entity.SourceIdsJson);

            return (Entity: entity, Dto: dto);
        }).ToList();

        var baseAmount        = lineData.Sum(x => x.Entity.Amount);
        var adjustmentsAmount = 0m;
        const decimal taxRate = 0.21m;
        var taxAmount         = (baseAmount + adjustmentsAmount) * taxRate;
        var totalAmount       = baseAmount + adjustmentsAmount + taxAmount;

        return new CalculationResult(
            baseAmount, adjustmentsAmount, taxAmount, totalAmount, currency, lineData);
    }

    internal static SettlementDetailDto BuildDetailDto(
        Guid      id,
        string    number,
        string    status,
        Agreement agreement,
        int       year,
        int       month,
        decimal   baseAmount,
        decimal   adjustments,
        decimal   tax,
        decimal   total,
        string    currency,
        List<SettlementLineDto> lineDtos)
        => new(
            id, number, status,
            agreement.Id, agreement.AgreementNumber,
            year, month,
            agreement.IdScrap, agreement.Scrap?.Name,
            agreement.IdPublicEntity, agreement.PublicEntity?.Name,
            baseAmount, adjustments, tax, total, currency,
            null, null, null, null, null,
            1, DateTime.UtcNow, DateTime.UtcNow,
            lineDtos);

    internal static string ComputeHash(Settlement s)
    {
        var raw = $"{s.Id}|{s.AgreementId}|{s.Year}|{s.Month}|{s.TotalAmount}|{s.CreatedAt:O}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))
               .ToLowerInvariant();
    }

    internal static async Task<string> GenerateSettlementNumberAsync(
        IApplicationDbContext context, CancellationToken ct)
    {
        var year  = DateTime.UtcNow.Year;
        var count = await context.Settlements
            .CountAsync(s => s.CreatedAt.Year == year, ct);
        return $"LIQ-{year}-{count + 1:D4}";
    }

    private static Dictionary<string, decimal> ParseJsonDict(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<Dictionary<string, decimal>>(json) ?? []; }
        catch { return []; }
    }
}
