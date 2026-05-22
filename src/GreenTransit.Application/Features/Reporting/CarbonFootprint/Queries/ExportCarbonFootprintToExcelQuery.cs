using ClosedXML.Excel;
using GreenTransit.Application.Features.Reporting.CarbonFootprint.DTOs;
using MediatR;

namespace GreenTransit.Application.Features.Reporting.CarbonFootprint.Queries;

/// <summary>
/// Genera un fichero XLSX con los datos del módulo HC — Huella de Carbono.
/// Soporta HC-A (CarbonOverview) y HC-C (PlantEnergyFootprint).
/// El fichero se genera íntegramente en memoria (nunca se persiste en servidor).
/// </summary>
public sealed record ExportCarbonFootprintToExcelQuery(
    string    DashboardType,         // "CarbonOverview" | "PlantEnergy"
    DateTime  DateFrom,
    DateTime  DateTo,
    Guid?     IdScrap            = null,
    string?   PlantCenterCode    = null,
    string?   PlantName          = null,
    string?   Source             = null,
    string?   ProvinceCode       = null
) : IRequest<CarbonExcelResultDto>;

public sealed record CarbonExcelResultDto(
    byte[]  FileContent,
    string  FileName,
    string  ContentType
);

public sealed class ExportCarbonFootprintToExcelQueryHandler
    : IRequestHandler<ExportCarbonFootprintToExcelQuery, CarbonExcelResultDto>
{
    private readonly IMediator _mediator;

    public ExportCarbonFootprintToExcelQueryHandler(IMediator mediator) => _mediator = mediator;

    public async Task<CarbonExcelResultDto> Handle(
        ExportCarbonFootprintToExcelQuery request, CancellationToken ct)
    {
        using var wb = new XLWorkbook();
        string dashLabel;

        if (request.DashboardType == "PlantEnergy")
        {
            var dto = await _mediator.Send(new GetPlantEnergyFootprintQuery(
                request.DateFrom, request.DateTo, request.PlantName, request.PlantCenterCode, request.Source), ct);
            dashLabel = $"HuellaEnergeticaPlantas_{request.DateFrom:yyyy}";
            BuildPlantEnergySheet(wb, dto);
        }
        else
        {
            var dto = await _mediator.Send(new GetCarbonOverviewQuery(
                request.DateFrom, request.DateTo, request.ProvinceCode, IdScrap: request.IdScrap), ct);
            dashLabel = $"HuellaCarbono_Consolidado_{request.DateFrom:yyyy}";
            BuildCarbonOverviewSheet(wb, dto);
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return new CarbonExcelResultDto(
            ms.ToArray(),
            $"{dashLabel}.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    // ── HC-A: Visión consolidada ───────────────────────────────────────────────

    private static void BuildCarbonOverviewSheet(XLWorkbook wb, CarbonOverviewDto dto)
    {
        // Hoja 1: Evolución mensual
        var ws1 = wb.Worksheets.Add("Evolución Mensual");
        WriteHeaders(ws1, ["Año", "Mes", "CO₂e (t)"]);
        var row = 2;
        foreach (var m in dto.MonthlyEvolution)
        {
            ws1.Cell(row, 1).Value = m.Year;
            ws1.Cell(row, 2).Value = m.Month;
            ws1.Cell(row, 3).Value = (double)m.CO2eTonnes;
            row++;
        }
        ws1.Columns().AdjustToContents();

        // Hoja 2: Desglose por combustible
        var ws2 = wb.Worksheets.Add("Por Combustible");
        WriteHeaders(ws2, ["Tipo Combustible", "CO₂e (t)", "% Total"]);
        row = 2;
        foreach (var f in dto.ByFuelType)
        {
            ws2.Cell(row, 1).Value = f.FuelType;
            ws2.Cell(row, 2).Value = (double)f.CO2eTonnes;
            ws2.Cell(row, 3).Value = f.Pct;
            row++;
        }
        ws2.Columns().AdjustToContents();

        // Hoja 3: Top 10 operaciones más emisoras
        var ws3 = wb.Worksheets.Add("Top 10 Operaciones");
        WriteHeaders(ws3, ["Referencia", "Origen", "Destino", "Dist. (km)", "Vehículo", "Combustible", "Peso (kg)", "CO₂e (kg)"]);
        row = 2;
        foreach (var op in dto.Top10Operations)
        {
            ws3.Cell(row, 1).Value = op.WasteMoveReference;
            ws3.Cell(row, 2).Value = op.OriginName ?? "";
            ws3.Cell(row, 3).Value = op.DestinationName ?? "";
            ws3.Cell(row, 4).Value = (double)op.DistanceKm;
            ws3.Cell(row, 5).Value = op.VehicleType ?? "";
            ws3.Cell(row, 6).Value = op.FuelType ?? "";
            ws3.Cell(row, 7).Value = (double)op.WeightKg;
            ws3.Cell(row, 8).Value = (double)op.CO2eKg;
            row++;
        }
        ws3.Columns().AdjustToContents();
    }

    // ── HC-C: Huella energética de plantas ────────────────────────────────────

    private static void BuildPlantEnergySheet(XLWorkbook wb, CarbonPlantEnergyDto dto)
    {
        // Hoja 1: Comparativa de plantas
        var ws = wb.Worksheets.Add("Comparativa Plantas");
        WriteHeaders(ws, ["Instalación", "kWh Total", "Scope 2 CO₂e (kg)", "Peso Tratado (kg)", "CO₂e/t"]);
        var row = 2;
        foreach (var p in dto.PlantComparison)
        {
            ws.Cell(row, 1).Value = p.PlantName;
            ws.Cell(row, 2).Value = (double)p.KwhTotal;
            ws.Cell(row, 3).Value = (double)p.Scope2CO2eKg;
            ws.Cell(row, 4).Value = (double)p.TreatmentWeightKg;
            ws.Cell(row, 5).Value = (double)p.CO2ePerTonneKg;
            row++;
        }
        ws.Columns().AdjustToContents();

        // Hoja 2: Desglose por fuente de energía
        var ws2 = wb.Worksheets.Add("Por Fuente Energía");
        WriteHeaders(ws2, ["Fuente", "kWh Total", "% Total"]);
        row = 2;
        foreach (var s in dto.BySource)
        {
            ws2.Cell(row, 1).Value = s.Source;
            ws2.Cell(row, 2).Value = (double)s.KwhTotal;
            ws2.Cell(row, 3).Value = s.Pct;
            row++;
        }
        ws2.Columns().AdjustToContents();

        // Hoja 3: Detalle mensual por planta
        var ws3 = wb.Worksheets.Add("Detalle Mensual");
        WriteHeaders(ws3, ["Instalación", "Código Centro", "Año", "Mes", "kWh", "Fuente", "Peso Tratado (kg)", "Scope 2 CO₂e (kg)", "CO₂e/t"]);
        row = 2;
        foreach (var d in dto.Details)
        {
            ws3.Cell(row, 1).Value = d.PlantName;
            ws3.Cell(row, 2).Value = d.PlantCenterCode ?? "";
            ws3.Cell(row, 3).Value = d.Year;
            ws3.Cell(row, 4).Value = d.Month;
            ws3.Cell(row, 5).Value = (double)d.KwhTotal;
            ws3.Cell(row, 6).Value = d.Source ?? "";
            ws3.Cell(row, 7).Value = (double)d.TreatmentWeightKg;
            ws3.Cell(row, 8).Value = (double)d.Scope2CO2eKg;
            ws3.Cell(row, 9).Value = (double)d.CO2ePerTonneKg;
            row++;
        }
        ws3.Columns().AdjustToContents();
    }

    private static void WriteHeaders(IXLWorksheet ws, string[] headers)
    {
        for (var i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];
        var header = ws.Range(1, 1, 1, headers.Length);
        header.Style.Font.Bold            = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#2d6a4f");
        header.Style.Font.FontColor       = XLColor.White;
        ws.Columns().AdjustToContents();
    }
}
