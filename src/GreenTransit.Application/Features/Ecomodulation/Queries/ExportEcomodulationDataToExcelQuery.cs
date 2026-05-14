using ClosedXML.Excel;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Ecomodulation.DTOs;
using GreenTransit.Application.Features.Reporting.DTOs;
using MediatR;

namespace GreenTransit.Application.Features.Ecomodulation.Queries;

/// <summary>
/// Genera un fichero XLSX con los datos de ecomodulación (UC5-A — SCRAP Overview).
/// Patrón ClosedXML idéntico al de ExportKpisToExcelQuery.
/// </summary>
public sealed record ExportEcomodulationDataToExcelQuery(
    int     Year,
    string? ProductCategory        = null,
    Guid?   IdProducer             = null,
    Guid?   EcoModulationRuleSetId = null
) : IRequest<ExportKpisResultDto>;

public sealed class ExportEcomodulationDataToExcelQueryHandler
    : IRequestHandler<ExportEcomodulationDataToExcelQuery, ExportKpisResultDto>
{
    private readonly IMediator _mediator;

    public ExportEcomodulationDataToExcelQueryHandler(IMediator mediator) => _mediator = mediator;

    public async Task<ExportKpisResultDto> Handle(
        ExportEcomodulationDataToExcelQuery request, CancellationToken ct)
    {
        var dto = await _mediator.Send(new GetEcomodulationScrapOverviewQuery(
            request.Year, request.ProductCategory, request.IdProducer, request.EcoModulationRuleSetId), ct);

        using var wb = new XLWorkbook();
        BuildExportSheet(wb, dto);
        BuildCircularitySheet(wb, dto);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);

        return new ExportKpisResultDto(
            ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Ecomodulacion_{request.Year}.xlsx");
    }

    private static void BuildExportSheet(XLWorkbook wb, EcomodulationScrapOverviewDto dto)
    {
        var ws = wb.Worksheets.Add("Productos Ecomodulación");
        var headers = new[]
        {
            "Referencia", "Productor", "Categoría", "Código LER",
            "Índice Reparabilidad", "% Contenido Reciclado", "Facilidad Desmontaje",
            "Contiene Peligrosos", "Composición", "Regla Ecomodulación", "Ajuste Económico (€)"
        };
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
            ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#2D6A4F");
            ws.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
        }

        int row = 2;
        foreach (var r in dto.ExportRows)
        {
            ws.Cell(row, 1).Value  = r.ProductReference;
            ws.Cell(row, 2).Value  = r.ProducerName;
            ws.Cell(row, 3).Value  = r.Category;
            ws.Cell(row, 4).Value  = r.LerCode ?? "";
            ws.Cell(row, 5).Value  = r.ReparabilityIndex?.ToString("F1") ?? "";
            ws.Cell(row, 6).Value  = r.RecycledContentPercent?.ToString("F1") ?? "";
            ws.Cell(row, 7).Value  = r.DisassemblyEase ?? "";
            ws.Cell(row, 8).Value  = r.ContainsHazardous ? "Sí" : "No";
            ws.Cell(row, 9).Value  = r.Composition ?? "";
            ws.Cell(row, 10).Value = r.EcomodRuleName ?? "";
            ws.Cell(row, 11).Value = r.EconomicAdjustment?.ToString("F2") ?? "";
            row++;
        }
        ws.Columns().AdjustToContents();
    }

    private static void BuildCircularitySheet(XLWorkbook wb, EcomodulationScrapOverviewDto dto)
    {
        var ws = wb.Worksheets.Add("Índice Circularidad");
        var headers = new[] { "Categoría", "Índice Circularidad", "% Reciclado", "Reparabilidad", "Desmontaje", "% No Peligrosos", "% Con LER", "Semáforo" };
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
        }
        int row = 2;
        foreach (var c in dto.CircularityByCategory)
        {
            ws.Cell(row, 1).Value = c.Category;
            ws.Cell(row, 2).Value = c.CircularityIndex;
            ws.Cell(row, 3).Value = c.AvgRecycledContentPct;
            ws.Cell(row, 4).Value = c.AvgReparabilityIndex;
            ws.Cell(row, 5).Value = c.AvgDisassemblyEase;
            ws.Cell(row, 6).Value = c.PctNonHazardous;
            ws.Cell(row, 7).Value = c.PctWithLerCodes;
            ws.Cell(row, 8).Value = c.TrafficLight;
            row++;
        }
        ws.Columns().AdjustToContents();
    }
}
