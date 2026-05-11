using ClosedXML.Excel;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.Mobility.DTOs;
using MediatR;

namespace GreenTransit.Application.Features.Mobility.Queries;

/// <summary>
/// Genera un XLSX en memoria con el dataset de impacto en movilidad para la vista UC3-C.
/// Reutiliza el patrón ClosedXML ya establecido en el proyecto.
/// </summary>
public sealed record ExportMobilityDataToExcelQuery(
    int     Year,
    int?    Month            = null,
    Guid?   IdScrap          = null,
    string? ProvinceCode     = null,
    string? MunicipalityCode = null
) : IRequest<ExportMobilityDataResult>;

public sealed record ExportMobilityDataResult(byte[] Content, string FileName);

public sealed class ExportMobilityDataToExcelQueryHandler
    : IRequestHandler<ExportMobilityDataToExcelQuery, ExportMobilityDataResult>
{
    private readonly IMediator _mediator;

    public ExportMobilityDataToExcelQueryHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<ExportMobilityDataResult> Handle(
        ExportMobilityDataToExcelQuery request, CancellationToken ct)
    {
        var data = await _mediator.Send(new GetMobilityDispatchDataQuery(
            request.Year, request.Month,
            request.IdScrap, request.ProvinceCode, request.MunicipalityCode), ct);

        using var wb = new XLWorkbook();

        // ── Hoja 1: Dataset completo ─────────────────────────────────────────
        var ws = wb.Worksheets.Add("Dataset Movilidad");
        var headers = new[]
        {
            "Fecha recogida", "Municipio", "Provincia", "SCRAP",
            "Tipo vehículo", "Peso (kg)", "Distancia (km)", "Duración (min)",
            "Emisiones CO₂e (kg)", "Zona DUM", "Cumple DUM", "Hora pico"
        };
        for (int col = 1; col <= headers.Length; col++)
        {
            ws.Cell(1, col).Value = headers[col - 1];
            ws.Cell(1, col).Style.Font.Bold = true;
        }

        int row = 2;
        foreach (var r in data.ExportDataset)
        {
            ws.Cell(row, 1).Value  = r.PickupDate?.ToString("dd/MM/yyyy HH:mm") ?? "";
            ws.Cell(row, 2).Value  = r.MunicipalityName ?? "";
            ws.Cell(row, 3).Value  = r.ProvinceCode ?? "";
            ws.Cell(row, 4).Value  = r.ScrapName ?? "";
            ws.Cell(row, 5).Value  = r.VehicleType ?? "";
            ws.Cell(row, 6).Value  = (double)r.WeightKg;
            ws.Cell(row, 7).Value  = (double)r.DistanceKm;
            ws.Cell(row, 8).Value  = (double)r.DurationMin;
            ws.Cell(row, 9).Value  = (double)r.CO2eKg;
            ws.Cell(row, 10).Value = r.DumZoneCode ?? "";
            ws.Cell(row, 11).Value = r.DumCompliant ? "Sí" : "No";
            ws.Cell(row, 12).Value = r.InPeakHour  ? "Sí" : "No";
            row++;
        }
        ws.Columns().AdjustToContents();

        // ── Hoja 2: Resumen por SCRAP ────────────────────────────────────────
        var ws2 = wb.Worksheets.Add("Resumen SCRAP");
        ws2.Cell(1, 1).Value = "SCRAP";           ws2.Cell(1, 1).Style.Font.Bold = true;
        ws2.Cell(1, 2).Value = "Recogidas";        ws2.Cell(1, 2).Style.Font.Bold = true;
        ws2.Cell(1, 3).Value = "% Hora pico";      ws2.Cell(1, 3).Style.Font.Bold = true;
        ws2.Cell(1, 4).Value = "% Cumple DUM";     ws2.Cell(1, 4).Style.Font.Bold = true;
        ws2.Cell(1, 5).Value = "Incidencias abiertas"; ws2.Cell(1, 5).Style.Font.Bold = true;

        row = 2;
        foreach (var s in data.ScrapSummaries)
        {
            ws2.Cell(row, 1).Value = s.ScrapName;
            ws2.Cell(row, 2).Value = s.TotalPickups;
            ws2.Cell(row, 3).Value = s.PeakHourPercent;
            ws2.Cell(row, 4).Value = s.DumCompliancePercent;
            ws2.Cell(row, 5).Value = s.OpenIncidents;
            row++;
        }
        ws2.Columns().AdjustToContents();

        // ── Hoja 3: Serie mensual ────────────────────────────────────────────
        var ws3 = wb.Worksheets.Add("Tendencia mensual");
        ws3.Cell(1, 1).Value = "Periodo";             ws3.Cell(1, 1).Style.Font.Bold = true;
        ws3.Cell(1, 2).Value = "% Hora pico";          ws3.Cell(1, 2).Style.Font.Bold = true;
        ws3.Cell(1, 3).Value = "% Cumplimiento DUM";   ws3.Cell(1, 3).Style.Font.Bold = true;
        ws3.Cell(1, 4).Value = "Índice conflicto prom"; ws3.Cell(1, 4).Style.Font.Bold = true;

        row = 2;
        foreach (var m in data.MonthlySeries)
        {
            ws3.Cell(row, 1).Value = m.Period;
            ws3.Cell(row, 2).Value = m.PeakHourPercent;
            ws3.Cell(row, 3).Value = m.DumCompliancePercent;
            ws3.Cell(row, 4).Value = m.AvgConflictIndex;
            row++;
        }
        ws3.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);

        var fileName = request.Month.HasValue
            ? $"movilidad_{request.Year}_{request.Month:00}.xlsx"
            : $"movilidad_{request.Year}.xlsx";

        return new ExportMobilityDataResult(ms.ToArray(), fileName);
    }
}
