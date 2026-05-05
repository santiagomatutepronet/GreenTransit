using GreenTransit.Application.Common.Interfaces;
using GreenTransit.Application.Features.ProductDeclarations.DTOs;
using GreenTransit.Application.Features.ProductDeclarations.Queries;
using MediatR;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GreenTransit.Web.Services;

/// <summary>Genera el PDF de una declaración de producción.</summary>
public sealed class ProductDeclarationPdfGenerator
{
    public static byte[] Generate(ProductDeclarationDetailDto d)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, QuestPDF.Infrastructure.Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Element(h => Header(h, d));
                page.Content().Element(c => Content(c, d));
                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("GreenTransit — Declaración de Producción  ·  Página ");
                    x.CurrentPageNumber();
                    x.Span(" de ");
                    x.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    private static void Header(IContainer container, ProductDeclarationDetailDto d)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("DECLARACIÓN DE PRODUCCIÓN")
                        .FontSize(14).Bold().FontColor(Colors.Green.Darken2);
                    c.Item().Text(d.Reference ?? d.Id.ToString())
                        .FontSize(11).SemiBold();
                });
                row.ConstantItem(140).AlignRight().Column(c =>
                {
                    c.Item().Text($"Estado: {d.State}").SemiBold();
                    c.Item().Text($"Fecha generación: {DateTime.UtcNow:dd/MM/yyyy HH:mm}");
                });
            });
            col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Green.Medium);
        });
    }

    private static void Content(IContainer container, ProductDeclarationDetailDto d)
    {
        container.Column(col =>
        {
            // Datos del productor
            col.Item().PaddingTop(8).Text("1. Datos del Productor")
                .FontSize(10).Bold().FontColor(Colors.Grey.Darken2);
            col.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text($"Nombre: {d.ProducerName ?? "—"}");
                    c.Item().Text($"NIF: {d.ProducerNationalId ?? "—"}");
                    c.Item().Text($"Centro: {d.ProducerCenterCode ?? "—"}");
                });
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text($"Año: {d.Year?.ToString() ?? "—"}");
                    c.Item().Text($"Periodo: T{d.Period?.ToString() ?? "—"}");
                    c.Item().Text($"Tipo: {d.Type ?? "—"}");
                    c.Item().Text($"Moneda: {d.Currency ?? "—"}");
                });
            });

            // Fechas
            col.Item().PaddingTop(8).Text("2. Fechas")
                .FontSize(10).Bold().FontColor(Colors.Grey.Darken2);
            col.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text($"Fecha creación: {d.DateCreate?.ToString("dd/MM/yyyy") ?? "—"}");
                row.RelativeItem().Text($"Fecha emisión: {d.DateEmit?.ToString("dd/MM/yyyy") ?? "—"}");
            });

            // Líneas de producto
            col.Item().PaddingTop(12).Text("3. Líneas de Producto")
                .FontSize(10).Bold().FontColor(Colors.Grey.Darken2);
            col.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(3); // Producto
                    cols.RelativeColumn(2); // Referencia
                    cols.RelativeColumn(2); // Fuente
                    cols.ConstantColumn(55);  // Cantidad
                    cols.ConstantColumn(40);  // Unidad
                    cols.ConstantColumn(50);  // Precio
                    cols.ConstantColumn(60);  // Subtotal
                });

                // Cabecera
                static IContainer CellStyle(IContainer c) =>
                    c.Background(Colors.Green.Lighten4).Padding(4);

                table.Header(h =>
                {
                    h.Cell().Element(CellStyle).Text("Producto").Bold();
                    h.Cell().Element(CellStyle).Text("Referencia").Bold();
                    h.Cell().Element(CellStyle).Text("Fuente").Bold();
                    h.Cell().Element(CellStyle).AlignRight().Text("Cantidad").Bold();
                    h.Cell().Element(CellStyle).Text("Unidad").Bold();
                    h.Cell().Element(CellStyle).AlignRight().Text("Precio").Bold();
                    h.Cell().Element(CellStyle).AlignRight().Text("Subtotal").Bold();
                });

                foreach (var p in d.Products)
                {
                    var subtotal = (p.Quantity ?? 0) * (p.Price ?? 0);
                    table.Cell().Padding(3).Text(p.ResidueName ?? "—");
                    table.Cell().Padding(3).Text(p.Reference ?? "—");
                    table.Cell().Padding(3).Text(p.Source ?? "—");
                    table.Cell().Padding(3).AlignRight().Text((p.Quantity ?? 0).ToString("N2"));
                    table.Cell().Padding(3).Text(p.MeasureUnit ?? "—");
                    table.Cell().Padding(3).AlignRight().Text((p.Price ?? 0).ToString("N2"));
                    table.Cell().Padding(3).AlignRight().Text(subtotal.ToString("N2"));
                }
            });

            // Total
            col.Item().PaddingTop(6).AlignRight()
                .Text($"TOTAL: {d.Amount?.ToString("N2") ?? "0.00"} {d.Currency}")
                .FontSize(11).Bold();

            // Firma
            col.Item().PaddingTop(30).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Firma del productor:").FontSize(9);
                    c.Item().PaddingTop(30).LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
                });
                row.ConstantItem(30);
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Validado por:").FontSize(9);
                    c.Item().PaddingTop(30).LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
                });
            });
        });
    }
}
