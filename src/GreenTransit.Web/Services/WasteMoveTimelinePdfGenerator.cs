using GreenTransit.Application.Features.WasteMoves.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GreenTransit.Web.Services;

/// <summary>
/// Genera el expediente completo de un traslado en formato PDF usando QuestPDF.
/// </summary>
public static class WasteMoveTimelinePdfGenerator
{
    public static byte[] Generate(WasteMoveTimelineDto tl)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Element(header => ComposeHeader(header, tl));
                page.Content().Element(content => ComposeContent(content, tl));
                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("GreenTransit — Expediente de Traslado  ·  Página ");
                    x.CurrentPageNumber();
                    x.Span(" de ");
                    x.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    // ── Header ────────────────────────────────────────────────────────────────

    private static void ComposeHeader(IContainer container, WasteMoveTimelineDto tl)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("EXPEDIENTE DE TRASLADO DE RESIDUOS")
                        .FontSize(14).Bold().FontColor(Colors.Green.Darken2);
                    c.Item().Text(tl.WasteMoveReference ?? tl.Id.ToString())
                        .FontSize(11).SemiBold();
                });
                row.ConstantItem(120).AlignRight().Column(c =>
                {
                    c.Item().Text($"Estado: {tl.CurrentStatus}").SemiBold();
                    c.Item().Text($"Fecha generación: {DateTime.UtcNow:dd/MM/yyyy HH:mm}");
                });
            });
            col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Green.Medium);
        });
    }

    // ── Content ───────────────────────────────────────────────────────────────

    private static void ComposeContent(IContainer container, WasteMoveTimelineDto tl)
    {
        container.Column(col =>
        {
            // Datos del traslado
            col.Item().PaddingTop(8).Element(e => SectionTitle(e, "1. Datos del Traslado"));
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
                AddRow(table, "Referencia",   tl.WasteMoveReference ?? "—");
                AddRow(table, "Origen",       tl.SourceName ?? "—");
                AddRow(table, "Destino",      tl.DestinationName ?? "—");
                AddRow(table, "SCRAP",        tl.ScrapName ?? "—");
                AddRow(table, "Fecha solicitud", tl.RequestDate?.ToString("dd/MM/yyyy") ?? "—");
                AddRow(table, "Recogida real",   tl.ActualPickupStart?.ToString("dd/MM/yyyy HH:mm") ?? "—");
                AddRow(table, "Firma",        tl.SignatureStatus ?? "—");
            });

            // SO origen
            if (tl.ServiceOrder is { } so)
            {
                col.Item().PaddingTop(8).Element(e => SectionTitle(e, "2. Orden de Servicio"));
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
                    AddRow(table, "Nº SO",      so.ServiceOrderNumber);
                    AddRow(table, "Emisor",     so.IssuedByName ?? "—");
                    AddRow(table, "LER",        $"{so.LerCodeCode} — {so.LerCodeDescription}");
                    AddRow(table, "Peso est.",  $"{so.EstimatedWeight?.ToString("N0") ?? "—"} kg");
                    AddRow(table, "Flujo",      so.WasteStream ?? "—");
                });
            }

            // Residuos
            if (tl.Residues.Any())
            {
                col.Item().PaddingTop(8).Element(e => SectionTitle(e, "3. Líneas de Residuo"));
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(3);
                        c.RelativeColumn(1);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                        c.RelativeColumn(1);
                    });
                    table.Header(h =>
                    {
                        foreach (var hdr in new[] { "Residuo", "Kg", "DI", "NT", "CO₂ kg" })
                            h.Cell().Background(Colors.Green.Lighten4).Padding(3).Text(hdr).Bold();
                    });
                    foreach (var r in tl.Residues)
                    {
                        table.Cell().Padding(2).Text(r.ResidueName ?? "—");
                        table.Cell().Padding(2).Text(r.Weight?.ToString("N0") ?? "—");
                        table.Cell().Padding(2).Text(r.DINumber ?? "—");
                        table.Cell().Padding(2).Text(r.NTNumber ?? "—");
                        table.Cell().Padding(2).Text(r.TransportCarbonEmissions?.ToString("N2") ?? "—");
                    }
                });
            }

            // EntryCACs
            if (tl.EntryCACs.Any())
            {
                col.Item().PaddingTop(8).Element(e => SectionTitle(e, "4. Entradas en CAC"));
                foreach (var cac in tl.EntryCACs)
                    col.Item().Text($"Entrada {cac.CACEntryDate?.ToString("dd/MM/yyyy")} — " +
                        $"{cac.Residues.Count} línea(s), " +
                        $"{cac.Residues.Sum(r => r.Weight ?? 0m):N0} kg");
            }

            // EntryPlants
            if (tl.EntryPlants.Any())
            {
                col.Item().PaddingTop(8).Element(e => SectionTitle(e, "5. Entrada en Planta — Pesaje"));
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1);
                    });
                    table.Header(h =>
                    {
                        foreach (var hdr in new[] { "Ticket", "Fecha", "Bruto kg", "Tara kg", "Neto kg" })
                            h.Cell().Background(Colors.Green.Lighten4).Padding(3).Text(hdr).Bold();
                    });
                    foreach (var ep in tl.EntryPlants)
                    {
                        table.Cell().Padding(2).Text(ep.TicketScale ?? "—");
                        table.Cell().Padding(2).Text(ep.PlantEntryDate?.ToString("dd/MM/yyyy") ?? "—");
                        table.Cell().Padding(2).Text(ep.GrossWeight?.ToString("N0") ?? "—");
                        table.Cell().Padding(2).Text(ep.TareWeight?.ToString("N0") ?? "—");
                        table.Cell().Padding(2).Text(ep.NetWeight?.ToString("N0") ?? "—");
                    }
                });
            }

            // TreatmentPlants
            if (tl.TreatmentPlants.Any())
            {
                col.Item().PaddingTop(8).Element(e => SectionTitle(e, "6. Clasificación y Tratamiento"));
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1);
                    });
                    table.Header(h =>
                    {
                        foreach (var hdr in new[] { "Fecha", "Operación R/D", "Entrada kg", "Reutil. kg", "Valor. kg", "Rechazo kg" })
                            h.Cell().Background(Colors.Green.Lighten4).Padding(3).Text(hdr).Bold();
                    });
                    foreach (var tp in tl.TreatmentPlants)
                    {
                        table.Cell().Padding(2).Text(tp.PlantTreatmentDate?.ToString("dd/MM/yyyy") ?? "—");
                        table.Cell().Padding(2).Text($"{tp.TreatmentOperationCode} {tp.TreatmentOperationDescription}");
                        table.Cell().Padding(2).Text(tp.TotalWeightIn.ToString("N0"));
                        table.Cell().Padding(2).Text(tp.TotalWeightReused.ToString("N0"));
                        table.Cell().Padding(2).Text(tp.TotalWeightValued.ToString("N0"));
                        table.Cell().Padding(2).Text(tp.TotalWeightRemove.ToString("N0"));
                    }
                });
            }

            // Huella CO₂
            col.Item().PaddingTop(8).Element(e => SectionTitle(e, "7. Huella de CO₂"));
            col.Item().Text($"Total: {tl.TotalCO2EmissionsKg:N2} kgCO₂e");

            // Incidencias
            if (tl.Incidents.Any())
            {
                col.Item().PaddingTop(8).Element(e => SectionTitle(e, "8. Incidencias"));
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(1);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                        c.RelativeColumn(1);
                    });
                    table.Header(h =>
                    {
                        foreach (var hdr in new[] { "Severidad", "Tipo", "Descripción", "Estado" })
                            h.Cell().Background(Colors.Orange.Lighten4).Padding(3).Text(hdr).Bold();
                    });
                    foreach (var inc in tl.Incidents)
                    {
                        table.Cell().Padding(2).Text(inc.Severity);
                        table.Cell().Padding(2).Text(inc.Type);
                        table.Cell().Padding(2).Text(inc.Description ?? "—");
                        table.Cell().Padding(2).Text(inc.IsOpen ? "Abierta" : "Cerrada");
                    }
                });
            }

            // Liquidaciones
            if (tl.SettlementLines.Any())
            {
                col.Item().PaddingTop(8).Element(e => SectionTitle(e, "9. Liquidaciones"));
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(3);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1);
                    });
                    table.Header(h =>
                    {
                        foreach (var hdr in new[] { "Liquidación", "LER", "Kg", "Importe €" })
                            h.Cell().Background(Colors.Blue.Lighten4).Padding(3).Text(hdr).Bold();
                    });
                    foreach (var sl in tl.SettlementLines)
                    {
                        table.Cell().Padding(2).Text(sl.SettlementNumber ?? "—");
                        table.Cell().Padding(2).Text(sl.LerCodeCode ?? "—");
                        table.Cell().Padding(2).Text(sl.WeightKg.ToString("N0"));
                        table.Cell().Padding(2).Text(sl.Amount.ToString("N2"));
                    }
                });
            }
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void SectionTitle(IContainer container, string title)
    {
        container.Column(col =>
        {
            col.Item().Text(title).FontSize(10).Bold().FontColor(Colors.Green.Darken2);
            col.Item().PaddingBottom(4).LineHorizontal(0.5f).LineColor(Colors.Green.Lighten2);
        });
    }

    private static void AddRow(TableDescriptor table, string label, string value)
    {
        table.Cell().Padding(2).Text(label).SemiBold();
        table.Cell().Padding(2).Text(value);
    }
}
