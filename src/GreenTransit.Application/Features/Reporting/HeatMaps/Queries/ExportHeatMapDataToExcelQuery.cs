using ClosedXML.Excel;
using GreenTransit.Application.Features.Reporting.HeatMaps.DTOs;
using MediatR;

namespace GreenTransit.Application.Features.Reporting.HeatMaps.Queries;

/// <summary>
/// Genera un fichero XLSX con los datos del dashboard de Mapas de Calor.
/// Soporta HM-A (WasteDensity) y HM-C (PublicView).
/// El fichero se genera íntegramente en memoria (nunca se persiste en servidor).
/// </summary>
public sealed record ExportHeatMapDataToExcelQuery(
    int     Year,
    int?    Month,
    string  DashboardType,   // "WasteDensity" | "PublicView"
    string? AutonomousCommunity = null,
    string? ProvinceCode        = null,
    string? MunicipalityCode    = null,
    string? LerCodeFilter       = null,
    string? WasteStream         = null,
    Guid?   IdScrap             = null
) : IRequest<HeatMapExportResultDto>;

public sealed class ExportHeatMapDataToExcelQueryHandler
    : IRequestHandler<ExportHeatMapDataToExcelQuery, HeatMapExportResultDto>
{
    private readonly IMediator _mediator;

    public ExportHeatMapDataToExcelQueryHandler(IMediator mediator) => _mediator = mediator;

    public async Task<HeatMapExportResultDto> Handle(
        ExportHeatMapDataToExcelQuery request, CancellationToken ct)
    {
        IReadOnlyList<HeatMapExportRowDto> rows;
        string dashLabel;

        if (request.DashboardType == "PublicView")
        {
            var dto = await _mediator.Send(new GetPublicEntityHeatMapQuery(
                request.Year, request.Month, request.LerCodeFilter, request.WasteStream, request.IdScrap), ct);
            rows      = dto.ExportRows;
            dashLabel = "MapaCalor_VistaPublica";
        }
        else
        {
            var dto = await _mediator.Send(new GetWasteDensityHeatMapQuery(
                request.Year, request.Month, request.AutonomousCommunity, request.ProvinceCode,
                request.MunicipalityCode, request.LerCodeFilter, request.WasteStream, request.IdScrap), ct);
            rows      = dto.ExportRows;
            dashLabel = "MapaCalor_Densidad";
        }

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Datos");

        // Cabeceras
        ws.Cell(1, 1).Value = "Entidad / Punto de Recogida";
        ws.Cell(1, 2).Value = "Municipio";
        ws.Cell(1, 3).Value = "Provincia";
        ws.Cell(1, 4).Value = "Código LER";
        ws.Cell(1, 5).Value = "Descripción LER";
        ws.Cell(1, 6).Value = "¿Peligroso?";
        ws.Cell(1, 7).Value = "Peso Total (kg)";
        ws.Cell(1, 8).Value = "Nº Recogidas";
        ws.Cell(1, 9).Value = "Última Recogida";

        var header = ws.Range(1, 1, 1, 9);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#2d6a4f");
        header.Style.Font.FontColor       = XLColor.White;

        int row = 2;
        foreach (var r in rows)
        {
            ws.Cell(row, 1).Value = r.EntityName;
            ws.Cell(row, 2).Value = r.Municipality ?? "";
            ws.Cell(row, 3).Value = r.Province ?? "";
            ws.Cell(row, 4).Value = r.LerCode ?? "";
            ws.Cell(row, 5).Value = r.LerDescription ?? "";
            ws.Cell(row, 6).Value = r.IsDangerous ? "Sí" : "No";
            ws.Cell(row, 7).Value = (double)r.TotalKg;
            ws.Cell(row, 8).Value = r.PickupCount;
            ws.Cell(row, 9).Value = r.LastPickup?.ToString("dd/MM/yyyy") ?? "";
            row++;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);

        var periodo   = request.Month.HasValue ? $"{request.Month:D2}_{request.Year}" : $"{request.Year}";
        var fileName  = $"{dashLabel}_{periodo}.xlsx";

        return new HeatMapExportResultDto(
            ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }
}
