using ClosedXML.Excel;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.ProductDeclarations.DTOs;
using GreenTransit.Domain.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GreenTransit.Application.Features.ProductDeclarations.Queries;

// ── Exportar listado ──────────────────────────────────────────────────────────

public sealed record ExportProductDeclarationsToExcelQuery(
    int?      Year       = null,
    int?      Period     = null,
    string?   State      = null,
    Guid?     IdProducer = null,
    string?   Type       = null,
    DateTime? DateFrom   = null,
    DateTime? DateTo     = null
) : IRequest<(byte[] Content, string FileName)>;

public sealed class ExportProductDeclarationsToExcelQueryHandler
    : IRequestHandler<ExportProductDeclarationsToExcelQuery, (byte[] Content, string FileName)>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public ExportProductDeclarationsToExcelQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<(byte[] Content, string FileName)> Handle(
        ExportProductDeclarationsToExcelQuery request, CancellationToken ct)
    {
        var q = _context.ProductDeclarations
            .AsNoTracking()
            .Include(pd => pd.Producer)
            .AsQueryable();

        if (_currentUser.IsInProfile(ProfileConstants.Producer))
            q = q.Where(pd => pd.IdProducer == _currentUser.LinkedEntityId);

        if (request.Year.HasValue)        q = q.Where(pd => pd.Year == request.Year);
        if (request.Period.HasValue)      q = q.Where(pd => pd.Period == request.Period);
        if (!string.IsNullOrEmpty(request.State))  q = q.Where(pd => pd.State == request.State);
        if (request.IdProducer.HasValue)  q = q.Where(pd => pd.IdProducer == request.IdProducer);
        if (!string.IsNullOrEmpty(request.Type))   q = q.Where(pd => pd.Type == request.Type);
        if (request.DateFrom.HasValue)    q = q.Where(pd => pd.DateCreate >= request.DateFrom);
        if (request.DateTo.HasValue)      q = q.Where(pd => pd.DateCreate <= request.DateTo);

        var items = await q.OrderByDescending(pd => pd.DateCreateSys).ToListAsync(ct);

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Declaraciones");

        // Cabecera
        var headers = new[]
        {
            "Referencia","Productor","NIF","Año","Periodo","Tipo",
            "Estado","Importe","Moneda","Fecha Creación","Fecha Emisión"
        };
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
            ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGreen;
        }

        // Datos
        for (int r = 0; r < items.Count; r++)
        {
            var pd = items[r];
            ws.Cell(r + 2, 1).Value  = pd.Reference ?? pd.Id.ToString();
            ws.Cell(r + 2, 2).Value  = pd.Producer?.Name ?? "";
            ws.Cell(r + 2, 3).Value  = pd.Producer?.NationalId ?? "";
            ws.Cell(r + 2, 4).Value  = pd.Year?.ToString() ?? "";
            ws.Cell(r + 2, 5).Value  = pd.Period.HasValue ? $"T{pd.Period}" : "";
            ws.Cell(r + 2, 6).Value  = pd.Type ?? "";
            ws.Cell(r + 2, 7).Value  = pd.State ?? "";
            ws.Cell(r + 2, 8).Value  = pd.Amount ?? 0;
            ws.Cell(r + 2, 9).Value  = pd.Currency ?? "";
            ws.Cell(r + 2, 10).Value = pd.DateCreateSys?.ToString("dd/MM/yyyy") ?? "";
            ws.Cell(r + 2, 11).Value = pd.DateEmit?.ToString("dd/MM/yyyy") ?? "";
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var fileName = $"declaraciones_{DateTime.Today:yyyyMMdd}.xlsx";
        return (ms.ToArray(), fileName);
    }
}

// ── Exportar detalle ──────────────────────────────────────────────────────────

public sealed record ExportProductDeclarationDetailToExcelQuery(Guid Id)
    : IRequest<(byte[] Content, string FileName)>;

public sealed class ExportProductDeclarationDetailToExcelQueryHandler
    : IRequestHandler<ExportProductDeclarationDetailToExcelQuery, (byte[] Content, string FileName)>
{
    private readonly IApplicationDbContext _context;

    public ExportProductDeclarationDetailToExcelQueryHandler(IApplicationDbContext context)
        => _context = context;

    public async Task<(byte[] Content, string FileName)> Handle(
        ExportProductDeclarationDetailToExcelQuery request, CancellationToken ct)
    {
        var pd = await _context.ProductDeclarations
            .AsNoTracking()
            .Include(d => d.Producer)
            .Include(d => d.Products)
                .ThenInclude(p => p.Residue)
            .FirstOrDefaultAsync(d => d.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Declaración {request.Id} no encontrada.");

        using var wb = new XLWorkbook();

        // ── Hoja 1: Cabecera ─────────────────────────────────────────────────
        var wsHeader = wb.Worksheets.Add("Cabecera");
        var headerData = new (string Label, string Value)[]
        {
            ("Referencia",      pd.Reference ?? pd.Id.ToString()),
            ("Productor",       pd.Producer?.Name ?? ""),
            ("NIF Productor",   pd.Producer?.NationalId ?? ""),
            ("Centro",          pd.Producer?.CenterCode ?? ""),
            ("Año",             pd.Year?.ToString() ?? ""),
            ("Periodo",         pd.Period.HasValue ? $"T{pd.Period}" : ""),
            ("Tipo",            pd.Type ?? ""),
            ("Moneda",          pd.Currency ?? ""),
            ("Estado",          pd.State ?? ""),
            ("Importe Total",   (pd.Amount ?? 0).ToString("N2")),
            ("Fecha Creación",  pd.DateCreateSys?.ToString("dd/MM/yyyy") ?? ""),
            ("Fecha Emisión",   pd.DateEmit?.ToString("dd/MM/yyyy") ?? ""),
        };
        for (int i = 0; i < headerData.Length; i++)
        {
            wsHeader.Cell(i + 1, 1).Value = headerData[i].Label;
            wsHeader.Cell(i + 1, 1).Style.Font.Bold = true;
            wsHeader.Cell(i + 1, 2).Value = headerData[i].Value;
        }
        wsHeader.Columns().AdjustToContents();

        // ── Hoja 2: Líneas ───────────────────────────────────────────────────
        var wsLines = wb.Worksheets.Add("Líneas");
        var lineHeaders = new[]
        {
            "Producto","Categoría","Referencia","Fuente",
            "Cantidad","Unidad","Unidades","Precio","Subtotal"
        };
        for (int i = 0; i < lineHeaders.Length; i++)
        {
            wsLines.Cell(1, i + 1).Value = lineHeaders[i];
            wsLines.Cell(1, i + 1).Style.Font.Bold = true;
            wsLines.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGreen;
        }
        for (int r = 0; r < pd.Products.Count; r++)
        {
            var p = pd.Products.ElementAt(r);
            wsLines.Cell(r + 2, 1).Value = p.Residue?.Name ?? "";
            wsLines.Cell(r + 2, 2).Value = p.Residue?.ProductCategory ?? "";
            wsLines.Cell(r + 2, 3).Value = p.Reference ?? "";
            wsLines.Cell(r + 2, 4).Value = p.Source ?? "";
            wsLines.Cell(r + 2, 5).Value = p.Quantity ?? 0;
            wsLines.Cell(r + 2, 6).Value = p.MeasureUnit ?? "";
            wsLines.Cell(r + 2, 7).Value = p.Units ?? 0;
            wsLines.Cell(r + 2, 8).Value = p.Price ?? 0;
            wsLines.Cell(r + 2, 9).Value = (p.Quantity ?? 0) * (p.Price ?? 0);
        }
        wsLines.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var fileName = $"declaracion_{pd.Reference ?? pd.Id.ToString()[..8]}_{DateTime.Today:yyyyMMdd}.xlsx";
        return (ms.ToArray(), fileName);
    }
}
