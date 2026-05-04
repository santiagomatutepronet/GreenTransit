using ClosedXML.Excel;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Reporting.DTOs;
using MediatR;

namespace GreenTransit.Application.Features.Reporting.Queries;

/// <summary>
/// Genera un fichero XLSX con 3 hojas: Resumen, Por Categoría, Histórico Trimestral.
/// El fichero se genera íntegramente en memoria (nunca se persiste en servidor).
/// ContentType: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet
/// </summary>
public sealed record ExportKpisToExcelQuery(
    int     Year,
    int?    Quarter,
    Guid?   IdScrap,
    string? AutonomousCommunity,
    string? Category
) : IRequest<ExportKpisResultDto>;

public sealed class ExportKpisToExcelQueryHandler
    : IRequestHandler<ExportKpisToExcelQuery, ExportKpisResultDto>
{
    private readonly IMediator _mediator;

    public ExportKpisToExcelQueryHandler(IMediator mediator) => _mediator = mediator;

    public async Task<ExportKpisResultDto> Handle(
        ExportKpisToExcelQuery request, CancellationToken ct)
    {
        // Reutiliza la lógica de GetRegulatoryKpisQuery
        var dto = await _mediator.Send(new GetRegulatoryKpisQuery(
            request.Year, request.Quarter, request.IdScrap,
            request.AutonomousCommunity, request.Category), ct);

        using var wb = new XLWorkbook();

        BuildSummarySheet(wb, dto);
        BuildCategorySheet(wb, dto);
        BuildQuarterlySheet(wb, dto);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);

        var periodoLabel = dto.Quarter.HasValue ? $"Q{dto.Quarter}_{dto.Year}" : $"{dto.Year}";
        var fileName     = $"KPIs_Regulatorios_{periodoLabel}.xlsx";

        return new ExportKpisResultDto(
            ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    // ── Hoja 1: Resumen ───────────────────────────────────────────────────────

    private static void BuildSummarySheet(XLWorkbook wb, RegulatoryKpisDto dto)
    {
        var ws = wb.Worksheets.Add("Resumen");

        // Título
        ws.Cell(1, 1).Value = "KPIs Regulatorios — GreenTransit";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Range(1, 1, 1, 3).Merge();

        ws.Cell(2, 1).Value = $"Periodo: {(dto.Quarter.HasValue ? $"Q{dto.Quarter} {dto.Year}" : dto.Year.ToString())}";
        ws.Cell(2, 1).Style.Font.Italic = true;

        // Cabecera
        int row = 4;
        WriteHeaderRow(ws, row, ["KPI", "Valor", "Objetivo normativo", "Cumple"]);
        row++;

        void WriteKpi(string label, string value, string target, bool? cumple)
        {
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 2).Value = value;
            ws.Cell(row, 3).Value = target;
            ws.Cell(row, 4).Value = cumple.HasValue ? (cumple.Value ? "✓" : "✗") : "—";
            if (cumple.HasValue)
                ws.Cell(row, 4).Style.Font.FontColor =
                    cumple.Value ? XLColor.DarkGreen : XLColor.DarkRed;
            row++;
        }

        WriteKpi("Tasa de Reciclaje (%)",
            $"{dto.RecyclingRatePercent:N2} %",
            $"≥ {dto.MinRecyclingPercent:N1} %",
            dto.RecyclingRatePercent >= dto.MinRecyclingPercent);

        WriteKpi("Tasa de Preparación Reutilización (%)",
            $"{dto.ReusePreparationPercent:N2} %",
            $"≥ {dto.MinReusePercent:N1} %",
            dto.ReusePreparationPercent >= dto.MinReusePercent);

        WriteKpi("Intensidad CO₂ (kgCO₂e/ton)",
            $"{dto.CO2IntensityKgPerTon:N2}",
            "—", null);

        WriteKpi("Total kg tratados",
            $"{dto.TotalWeightKg:N0} kg",
            "—", null);

        WriteKpi("Total traslados clasificados",
            $"{dto.TotalTransportsCount}",
            "—", null);

        // MarketShare compliance
        if (dto.MarketShareComplianceList.Any())
        {
            row++;
            ws.Cell(row, 1).Value = "Cumplimiento de Cuotas de Mercado";
            ws.Cell(row, 1).Style.Font.Bold = true;
            row++;
            WriteHeaderRow(ws, row, ["Categoría", "CCAA", "Objetivo (kg)", "Real (kg)", "Cumplimiento (%)"]);
            row++;

            foreach (var ms in dto.MarketShareComplianceList)
            {
                ws.Cell(row, 1).Value = ms.Category;
                ws.Cell(row, 2).Value = ms.AutonomousCommunity ?? "—";
                ws.Cell(row, 3).Value = (double)ms.TargetKg;
                ws.Cell(row, 4).Value = (double)ms.ActualKg;
                ws.Cell(row, 5).Value = ms.CompliancePercent;
                ws.Cell(row, 5).Style.Font.FontColor =
                    ms.CompliancePercent >= 100 ? XLColor.DarkGreen : XLColor.DarkRed;
                row++;
            }
        }

        ws.Columns().AdjustToContents();
    }

    // ── Hoja 2: Por Categoría ─────────────────────────────────────────────────

    private static void BuildCategorySheet(XLWorkbook wb, RegulatoryKpisDto dto)
    {
        var ws = wb.Worksheets.Add("Por Categoría");

        ws.Cell(1, 1).Value = "KPIs por Categoría de Residuo";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Range(1, 1, 1, 4).Merge();

        WriteHeaderRow(ws, 3, ["Categoría", "Total kg", "Tasa Reciclaje (%)", "Tasa Reutilización (%)"]);

        int row = 4;
        foreach (var cat in dto.ByCategory)
        {
            ws.Cell(row, 1).Value = cat.Category;
            ws.Cell(row, 2).Value = (double)cat.TotalWeightKg;
            ws.Cell(row, 3).Value = cat.RecyclingRatePercent;
            ws.Cell(row, 4).Value = cat.ReusePreparationPercent;
            row++;
        }

        ws.Columns().AdjustToContents();
    }

    // ── Hoja 3: Histórico Trimestral ──────────────────────────────────────────

    private static void BuildQuarterlySheet(XLWorkbook wb, RegulatoryKpisDto dto)
    {
        var ws = wb.Worksheets.Add("Histórico Trimestral");

        ws.Cell(1, 1).Value = $"Histórico Trimestral {dto.Year}";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Range(1, 1, 1, 6).Merge();

        WriteHeaderRow(ws, 3, [
            "Trimestre",
            "Total kg",
            "Traslados",
            "Tasa Reciclaje (%)",
            "Tasa Reutilización (%)",
            "Intensidad CO₂ (kgCO₂e/ton)"
        ]);

        int row = 4;
        foreach (var q in dto.ByQuarter)
        {
            ws.Cell(row, 1).Value = $"Q{q.Quarter}";
            ws.Cell(row, 2).Value = (double)q.TotalWeightKg;
            ws.Cell(row, 3).Value = q.TotalTransportsCount;
            ws.Cell(row, 4).Value = q.RecyclingRatePercent;
            ws.Cell(row, 5).Value = q.ReusePreparationPercent;
            ws.Cell(row, 6).Value = q.CO2IntensityKgPerTon;
            row++;
        }

        ws.Columns().AdjustToContents();
    }

    // ── Utilidad ──────────────────────────────────────────────────────────────

    private static void WriteHeaderRow(IXLWorksheet ws, int row, string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(row, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold            = true;
            cell.Style.Fill.BackgroundColor  = XLColor.FromHtml("#2E7D32");
            cell.Style.Font.FontColor        = XLColor.White;
        }
    }
}
