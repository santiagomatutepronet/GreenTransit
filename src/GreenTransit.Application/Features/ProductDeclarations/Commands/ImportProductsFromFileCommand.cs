using ClosedXML.Excel;
using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Domain.Authorization;
using GreenTransit.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace GreenTransit.Application.Features.ProductDeclarations.Commands;

// ── DTOs de resultado ─────────────────────────────────────────────────────────

public sealed record ImportRowErrorDto(int LineNumber, string Column, string ErrorMessage);

public sealed record ImportResultDto(
    int                       TotalRows,
    int                       SuccessRows,
    int                       ErrorRows,
    IReadOnlyList<ImportRowErrorDto> Errors
);

// ── Command ───────────────────────────────────────────────────────────────────

public sealed record ImportProductsFromFileCommand(
    Guid   IdProductDeclaration,
    byte[] FileContent,
    string FileName
) : IRequest<ImportResultDto>;

public sealed class ImportProductsFromFileCommandHandler
    : IRequestHandler<ImportProductsFromFileCommand, ImportResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService   _currentUser;

    public ImportProductsFromFileCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService   currentUser)
    {
        _context     = context;
        _currentUser = currentUser;
    }

    public async Task<ImportResultDto> Handle(
        ImportProductsFromFileCommand request, CancellationToken ct)
    {
        var declaration = await _context.ProductDeclarations
            .Include(pd => pd.Products)
            .FirstOrDefaultAsync(pd => pd.Id == request.IdProductDeclaration, ct)
            ?? throw new KeyNotFoundException(
                $"Declaración {request.IdProductDeclaration} no encontrada.");

        if (!ProductDeclaration.States.Editable.Contains(declaration.State ?? string.Empty))
            throw new InvalidOperationException(
                $"No se puede importar en una declaración en estado '{declaration.State}'.");

        if (_currentUser.IsInProfile(ProfileConstants.Producer)
            && declaration.IdProducer != _currentUser.LinkedEntityId)
            throw new UnauthorizedAccessException(
                "No tienes permiso para modificar esta declaración.");

        // ── Parsear filas ─────────────────────────────────────────────────────
        var rows = ParseFile(request.FileName, request.FileContent);
        var errors   = new List<ImportRowErrorDto>();
        var products = new List<Product>();

        // ── Cargar catálogo de residuos tipo Product para validación ──────────
        var residueMap = await _context.Residues
            .AsNoTracking()
            .Where(r => r.ResidueType == "Product")
            .ToDictionaryAsync(r => r.Reference ?? r.Id.ToString(), r => r, ct);

        for (int i = 0; i < rows.Count; i++)
        {
            var row      = rows[i];
            var lineNum  = i + 2; // +2: cabecera en fila 1, datos desde fila 2
            var rowErrors = new List<ImportRowErrorDto>();

            // Validar ProductReference
            if (string.IsNullOrWhiteSpace(row.ProductReference))
            {
                rowErrors.Add(new ImportRowErrorDto(lineNum, "ProductReference",
                    "El campo ProductReference es obligatorio."));
            }
            else if (!residueMap.TryGetValue(row.ProductReference, out var residue))
            {
                rowErrors.Add(new ImportRowErrorDto(lineNum, "ProductReference",
                    $"Residuo con referencia '{row.ProductReference}' no encontrado o no es de tipo Product."));
            }
            else if (row.Quantity <= 0)
            {
                rowErrors.Add(new ImportRowErrorDto(lineNum, "Quantity",
                    "La cantidad debe ser mayor que 0."));
            }
            else
            {
                products.Add(new Product
                {
                    Id                   = Guid.NewGuid(),
                    IdProductDeclaration = request.IdProductDeclaration,
                    IdResidue            = residue.Id,
                    Reference            = row.Reference,
                    Source               = row.Source,
                    Quantity             = row.Quantity,
                    MeasureUnit          = row.MeasureUnit,
                    Units                = row.Units,
                    Price                = row.Price
                });
            }

            errors.AddRange(rowErrors);
        }

        // ── Persistir filas válidas ───────────────────────────────────────────
        if (products.Count > 0)
        {
            _context.Products.AddRange(products);
            foreach (var p in products) declaration.Products.Add(p);
            declaration.Amount = declaration.Products.Sum(p => (p.Quantity ?? 0) * (p.Price ?? 0));
            declaration.DateModifiedSys = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
        }

        return new ImportResultDto(
            rows.Count,
            products.Count,
            errors.Count,
            errors);
    }

    // ── Parser ────────────────────────────────────────────────────────────────

    private sealed record ImportRow(
        string  ProductReference,
        string? Source,
        decimal Quantity,
        string? MeasureUnit,
        int?    Units,
        decimal? Price,
        string? Reference);

    private static List<ImportRow> ParseFile(string fileName, byte[] content)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".xlsx" or ".xls"
            ? ParseXlsx(content)
            : ParseCsv(content);
    }

    private static List<ImportRow> ParseXlsx(byte[] content)
    {
        var rows = new List<ImportRow>();
        using var ms = new MemoryStream(content);
        using var wb = new XLWorkbook(ms);
        var ws = wb.Worksheet(1);
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        for (int r = 2; r <= lastRow; r++)
        {
            rows.Add(new ImportRow(
                ProductReference: ws.Cell(r, 1).GetString(),
                Source:           ws.Cell(r, 2).GetString(),
                Quantity:         ParseDecimal(ws.Cell(r, 3).GetString()),
                MeasureUnit:      ws.Cell(r, 4).GetString(),
                Units:            ParseInt(ws.Cell(r, 5).GetString()),
                Price:            ParseNullableDecimal(ws.Cell(r, 6).GetString()),
                Reference:        ws.Cell(r, 7).GetString()));
        }
        return rows;
    }

    private static List<ImportRow> ParseCsv(byte[] content)
    {
        var rows  = new List<ImportRow>();
        var lines = System.Text.Encoding.UTF8.GetString(content)
                           .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 1; i < lines.Length; i++) // skip header
        {
            var cols = lines[i].Split(';');
            if (cols.Length < 3) continue;
            rows.Add(new ImportRow(
                ProductReference: cols[0].Trim(),
                Source:           cols.Length > 1 ? cols[1].Trim() : null,
                Quantity:         ParseDecimal(cols.Length > 2 ? cols[2].Trim() : "0"),
                MeasureUnit:      cols.Length > 3 ? cols[3].Trim() : null,
                Units:            cols.Length > 4 ? ParseInt(cols[4].Trim()) : null,
                Price:            cols.Length > 5 ? ParseNullableDecimal(cols[5].Trim()) : null,
                Reference:        cols.Length > 6 ? cols[6].Trim() : null));
        }
        return rows;
    }

    private static decimal  ParseDecimal(string s)
        => decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;
    private static decimal? ParseNullableDecimal(string s)
        => decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    private static int? ParseInt(string s)
        => int.TryParse(s, out var v) ? v : null;
}
