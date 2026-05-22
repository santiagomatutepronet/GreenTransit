using ClosedXML.Excel;
using GreenTransit.Application.Features.Reporting.RegulatoryCompliance.DTOs;
using MediatR;

namespace GreenTransit.Application.Features.Reporting.RegulatoryCompliance.Queries;

/// <summary>
/// Exportación a XLSX de los datos del módulo CN — Cumplimiento Normativo.
/// Soporta CN-B (MarketShareAudit), CN-D (PublicEntityCompliance) y CN-E (DispatchOfficeCompliance).
/// El fichero se genera íntegramente en memoria.
/// </summary>
public sealed record ExportComplianceDataToExcelQuery(
    int     Year,
    string  DashboardType,           // "MarketShareAudit" | "PublicEntityCompliance" | "DispatchOffice"
    Guid?   IdScrap             = null,
    string? AutonomousCommunity = null,
    string? FlowType            = null,
    string? Category            = null
) : IRequest<ComplianceExcelResultDto>;

public sealed record ComplianceExcelResultDto(
    byte[]  FileContent,
    string  FileName,
    string  ContentType
);

public sealed class ExportComplianceDataToExcelQueryHandler
    : IRequestHandler<ExportComplianceDataToExcelQuery, ComplianceExcelResultDto>
{
    private readonly IMediator _mediator;

    public ExportComplianceDataToExcelQueryHandler(IMediator mediator) => _mediator = mediator;

    public async Task<ComplianceExcelResultDto> Handle(
        ExportComplianceDataToExcelQuery request, CancellationToken ct)
    {
        using var wb = new XLWorkbook();
        string dashLabel;

        if (request.DashboardType == "MarketShareAudit")
        {
            var dto = await _mediator.Send(new GetMarketShareAuditQuery(
                request.Year, request.AutonomousCommunity, request.FlowType, request.Category, request.IdScrap), ct);

            dashLabel = $"Auditoria_Cuotas_{request.Year}";
            BuildMarketShareSheet(wb, dto);
        }
        else if (request.DashboardType == "PublicEntityCompliance")
        {
            var dto = await _mediator.Send(new GetPublicEntityComplianceViewQuery(
                request.Year, IdScrap: request.IdScrap, Category: request.Category), ct);

            dashLabel = $"Cumplimiento_EntidadPublica_{request.Year}";
            BuildPublicEntitySheet(wb, dto);
        }
        else
        {
            var dto = await _mediator.Send(new GetDispatchOfficeComplianceDataQuery(
                request.Year, request.IdScrap, request.AutonomousCommunity, request.FlowType, request.Category), ct);

            dashLabel = $"Cumplimiento_OfiAsignacion_{request.Year}";
            BuildDispatchOfficeSheet(wb, dto);
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return new ComplianceExcelResultDto(
            ms.ToArray(),
            $"{dashLabel}.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    private static void BuildMarketShareSheet(XLWorkbook wb, MarketShareAuditDto dto)
    {
        var ws = wb.Worksheets.Add("Auditoría Cuotas");
        string[] headers =
        [
            "SCRAP", "Objetivo Total (kg)", "Real Total (kg)", "% Cumplimiento", "Desviación (kg)", "% Desviación"
        ];
        WriteHeaders(ws, headers);

        var row = 2;
        foreach (var s in dto.ScrapSummaries)
        {
            ws.Cell(row, 1).Value = s.ScrapName;
            ws.Cell(row, 2).Value = (double)s.TargetWeightKg;
            ws.Cell(row, 3).Value = (double)s.RealWeightKg;
            ws.Cell(row, 4).Value = (double)s.CompliancePct;
            ws.Cell(row, 5).Value = (double)s.DeviationKg;
            ws.Cell(row, 6).Value = (double)s.DeviationPct;
            var color = s.CompliancePct >= 100m ? XLColor.FromHtml("#d8f3dc")
                      : s.CompliancePct >= 80m  ? XLColor.FromHtml("#fff3cd")
                                                : XLColor.FromHtml("#f8d7da");
            ws.Row(row).Style.Fill.BackgroundColor = color;
            row++;
        }

        var ws2 = wb.Worksheets.Add("Desglose Territorial");
        string[] headers2 =
        [
            "Comunidad Autónoma", "SCRAP", "Categoría", "Objetivo (kg)", "Real (kg)", "% Cumplimiento", "Estado"
        ];
        WriteHeaders(ws2, headers2);
        row = 2;
        foreach (var t in dto.TerritorialBreakdown)
        {
            ws2.Cell(row, 1).Value = t.AutonomousCommunity;
            ws2.Cell(row, 2).Value = t.ScrapName;
            ws2.Cell(row, 3).Value = t.Category;
            ws2.Cell(row, 4).Value = (double)t.TargetWeightKg;
            ws2.Cell(row, 5).Value = (double)t.RealWeightKg;
            ws2.Cell(row, 6).Value = (double)t.CompliancePct;
            ws2.Cell(row, 7).Value = t.Status;
            row++;
        }
        ws2.Columns().AdjustToContents();
    }

    private static void BuildDispatchOfficeSheet(XLWorkbook wb, DispatchOfficeComplianceDataDto dto)
    {
        var ws = wb.Worksheets.Add("Cumplimiento Completo");
        string[] headers =
        [
            "SCRAP", "Categoría", "Comunidad Autónoma", "Provincia", "Municipio",
            "Flujo", "Año", "Periodo", "Objetivo (kg)", "Real (kg)",
            "% Cumplimiento", "Tasa Reciclaje (%)", "Tasa Valorización (%)", "Convenios Activos", "Importe Aprobado"
        ];
        WriteHeaders(ws, headers);

        var row = 2;
        foreach (var r in dto.ExportRows)
        {
            ws.Cell(row,  1).Value = r.ScrapName;
            ws.Cell(row,  2).Value = r.Category;
            ws.Cell(row,  3).Value = r.AutonomousCommunity;
            ws.Cell(row,  4).Value = r.ProvinceName;
            ws.Cell(row,  5).Value = r.MunicipalityName;
            ws.Cell(row,  6).Value = r.FlowType;
            ws.Cell(row,  7).Value = r.Year;
            ws.Cell(row,  8).Value = r.Period;
            ws.Cell(row,  9).Value = (double)r.TargetWeightKg;
            ws.Cell(row, 10).Value = (double)r.RealWeightKg;
            ws.Cell(row, 11).Value = (double)r.CompliancePct;
            ws.Cell(row, 12).Value = (double)r.RecyclingPct;
            ws.Cell(row, 13).Value = (double)r.ValorizationPct;
            ws.Cell(row, 14).Value = r.ActiveAgreements;
            ws.Cell(row, 15).Value = (double)r.ApprovedAmount;
            row++;
        }
        ws.Columns().AdjustToContents();
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

    private static void BuildPublicEntitySheet(XLWorkbook wb, PublicEntityComplianceViewDto dto)
    {
        // Hoja 1: Cumplimiento por SCRAP
        var ws = wb.Worksheets.Add("Cumplimiento por SCRAP");
        WriteHeaders(ws, ["SCRAP", "Categoría", "Flujo", "Objetivo (kg)", "Real (kg)", "% Cumplimiento", "Estado"]);
        var row = 2;
        foreach (var s in dto.ScrapCompliance)
        {
            ws.Cell(row, 1).Value = s.ScrapName;
            ws.Cell(row, 2).Value = s.Category;
            ws.Cell(row, 3).Value = s.FlowType;
            ws.Cell(row, 4).Value = (double)s.TargetWeightKg;
            ws.Cell(row, 5).Value = (double)s.RealWeightKg;
            ws.Cell(row, 6).Value = (double)s.CompliancePct;
            ws.Cell(row, 7).Value = s.Status;
            var color = s.CompliancePct >= 100m ? XLColor.FromHtml("#d8f3dc")
                      : s.CompliancePct >= 80m  ? XLColor.FromHtml("#fff3cd")
                                                : XLColor.FromHtml("#f8d7da");
            ws.Row(row).Style.Fill.BackgroundColor = color;
            row++;
        }
        ws.Columns().AdjustToContents();

        // Hoja 2: Liquidaciones de compensación
        var ws2 = wb.Worksheets.Add("Liquidaciones");
        WriteHeaders(ws2, ["SCRAP", "Nº Liquidación", "Año", "Mes", "Importe Base", "Ajustes", "Total", "Estado", "Validado"]);
        row = 2;
        foreach (var c in dto.CompensationSettlements)
        {
            ws2.Cell(row, 1).Value = c.ScrapName;
            ws2.Cell(row, 2).Value = c.SettlementNumber;
            ws2.Cell(row, 3).Value = c.Year;
            ws2.Cell(row, 4).Value = c.Month;
            ws2.Cell(row, 5).Value = (double)c.BaseAmount;
            ws2.Cell(row, 6).Value = (double)c.AdjustmentsAmount;
            ws2.Cell(row, 7).Value = (double)c.TotalAmount;
            ws2.Cell(row, 8).Value = c.Status;
            ws2.Cell(row, 9).Value = c.ValidatedAt.HasValue ? c.ValidatedAt.Value.ToString("yyyy-MM-dd") : "";
            row++;
        }
        ws2.Columns().AdjustToContents();
    }
}
