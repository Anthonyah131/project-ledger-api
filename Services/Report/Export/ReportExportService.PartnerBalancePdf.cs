using ProjectLedger.API.DTOs.Report;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ProjectLedger.API.Services.Report;

public partial class ReportExportService
{
    // ════════════════════════════════════════════════════════
    //  PARTNER BALANCE REPORT — PDF
    // ════════════════════════════════════════════════════════

    /// <inheritdoc />
    public byte[] GeneratePartnerBalanceReportPdf(PartnerBalanceReportResponse report)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Element(h => ComposePartnerBalanceHeader(h, report));
                page.Content().Element(c => ComposePartnerBalanceContent(c, report));
                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    /// <summary>Composes the visual header for the partner balance report.</summary>
    private static void ComposePartnerBalanceHeader(IContainer container, PartnerBalanceReportResponse report)
    {
        container.Column(col =>
        {
            col.Item().Text($"Balances de Partners — {report.ProjectName}")
                .FontSize(16).Bold().FontColor(Colors.Blue.Darken3);

            col.Item().Row(row =>
            {
                row.RelativeItem().Text($"Moneda: {report.CurrencyCode}").FontSize(9);
                row.RelativeItem().Text($"Período: {FormatDateRange(report.DateFrom, report.DateTo)}").FontSize(9);
                row.RelativeItem().AlignRight()
                    .Text($"Generado: {report.GeneratedAt:yyyy-MM-dd HH:mm} UTC").FontSize(8);
            });

            col.Item().PaddingTop(5).Row(row =>
            {
                row.RelativeItem().Background(Colors.Blue.Lighten4).Padding(8).Column(c =>
                {
                    c.Item().Text("Partners").FontSize(8).FontColor(Colors.Grey.Darken2);
                    c.Item().Text($"{report.Partners.Count}").FontSize(14).Bold();
                });
                row.ConstantItem(10);
                row.RelativeItem().Background(Colors.Grey.Lighten3).Padding(8).Column(c =>
                {
                    c.Item().Text("Settlements").FontSize(8).FontColor(Colors.Grey.Darken2);
                    c.Item().Text($"{report.Settlements.Count}").FontSize(14).Bold();
                });
                row.ConstantItem(10);
                row.RelativeItem().Background(Colors.Grey.Lighten3).Padding(8).Column(c =>
                {
                    c.Item().Text("Pares con Balance").FontSize(8).FontColor(Colors.Grey.Darken2);
                    c.Item().Text($"{report.PairwiseBalances.Count}").FontSize(14).Bold();
                });
            });

            col.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        });
    }

    /// <summary>Composes the main body sections of the partner balance report.</summary>
    private static void ComposePartnerBalanceContent(IContainer container, PartnerBalanceReportResponse report)
    {
        container.Column(col =>
        {
            if (report.Partners.Count == 0)
            {
                ComposePdfEmptyState(col, "No se encontraron partners con actividad en el período seleccionado.");
                return;
            }

            // ── Partner balance table ────────────────────────
            col.Item().Text("Balances por Partner")
                .FontSize(12).Bold().FontColor(Colors.Blue.Darken2);

            col.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(3);   // Partner
                    cols.ConstantColumn(60);  // Pagó Físicamente
                    cols.ConstantColumn(60);  // Otros le Deben
                    cols.ConstantColumn(60);  // Él Debe
                    cols.ConstantColumn(60);  // Stl. Pagados
                    cols.ConstantColumn(60);  // Stl. Recibidos
                    cols.ConstantColumn(65);  // Balance Neto
                });

                table.Header(header =>
                {
                    PdfTableHeaderCell(header, "Partner");
                    PdfTableHeaderCell(header, "Pagó Físic.", true);
                    PdfTableHeaderCell(header, "Otros Deben", true);
                    PdfTableHeaderCell(header, "Él Debe", true);
                    PdfTableHeaderCell(header, "Stl. Pagado", true);
                    PdfTableHeaderCell(header, "Stl. Recibido", true);
                    PdfTableHeaderCell(header, "Balance", true);
                });

                foreach (var p in report.Partners)
                {
                    PdfTableCell(table, p.PartnerName);
                    PdfTableCell(table, FormatCurrency(report.CurrencyCode, p.PaidPhysically), true);
                    PdfTableCell(table, FormatCurrency(report.CurrencyCode, p.OthersOweHim), true);
                    PdfTableCell(table, FormatCurrency(report.CurrencyCode, p.HeOwesOthers), true);
                    PdfTableCell(table, FormatCurrency(report.CurrencyCode, p.SettlementsPaid), true);
                    PdfTableCell(table, FormatCurrency(report.CurrencyCode, p.SettlementsReceived), true);

                    var balanceColor = p.NetBalance < 0 ? Colors.Red.Darken1
                        : p.NetBalance > 0 ? Colors.Green.Darken2
                        : Colors.Grey.Darken1;

                    table.Cell()
                        .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                        .PaddingVertical(3).PaddingHorizontal(4)
                        .AlignRight()
                        .Text(FormatCurrency(report.CurrencyCode, p.NetBalance))
                        .FontSize(8).Bold().FontColor(balanceColor);
                }
            });

            // ── Settlements ──────────────────────────────────
            if (report.Settlements.Count > 0)
            {
                col.Item().PaddingTop(15).Text("Settlements")
                    .FontSize(12).Bold().FontColor(Colors.Blue.Darken2);

                col.Item().PaddingTop(4).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(65);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(2);
                        cols.ConstantColumn(70);
                        cols.ConstantColumn(70);
                        cols.RelativeColumn(3);
                    });

                    table.Header(header =>
                    {
                        PdfTableHeaderCell(header, "Fecha");
                        PdfTableHeaderCell(header, "De");
                        PdfTableHeaderCell(header, "A");
                        PdfTableHeaderCell(header, "Monto", true);
                        PdfTableHeaderCell(header, "Convertido", true);
                        PdfTableHeaderCell(header, "Descripción");
                    });

                    foreach (var s in report.Settlements)
                    {
                        PdfTableCell(table, s.SettlementDate.ToString("yyyy-MM-dd"));
                        PdfTableCell(table, s.FromPartnerName);
                        PdfTableCell(table, s.ToPartnerName);
                        PdfTableCell(table, FormatCurrency(s.Currency, s.Amount), true);
                        PdfTableCell(table, FormatCurrency(report.CurrencyCode, s.ConvertedAmount), true);
                        PdfTableCell(table, s.Description ?? "—");
                    }
                });
            }

            // ── Pairwise balances ────────────────────────────
            if (report.PairwiseBalances.Count > 0)
            {
                col.Item().PaddingTop(15).Text("Balances entre Pares")
                    .FontSize(12).Bold().FontColor(Colors.Blue.Darken2);

                col.Item().PaddingTop(4).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(2);  // Partner A
                        cols.RelativeColumn(2);  // Partner B
                        cols.ConstantColumn(60); // A debe a B
                        cols.ConstantColumn(60); // B debe a A
                        cols.ConstantColumn(60); // Stl. A→B
                        cols.ConstantColumn(60); // Stl. B→A
                        cols.ConstantColumn(65); // Balance Neto
                    });

                    table.Header(header =>
                    {
                        PdfTableHeaderCell(header, "Partner A");
                        PdfTableHeaderCell(header, "Partner B");
                        PdfTableHeaderCell(header, "A debe B", true);
                        PdfTableHeaderCell(header, "B debe A", true);
                        PdfTableHeaderCell(header, "Stl. A→B", true);
                        PdfTableHeaderCell(header, "Stl. B→A", true);
                        PdfTableHeaderCell(header, "Balance", true);
                    });

                    foreach (var pw in report.PairwiseBalances)
                    {
                        PdfTableCell(table, pw.PartnerAName);
                        PdfTableCell(table, pw.PartnerBName);
                        PdfTableCell(table, FormatCurrency(report.CurrencyCode, pw.AOwesB), true);
                        PdfTableCell(table, FormatCurrency(report.CurrencyCode, pw.BOwesA), true);
                        PdfTableCell(table, FormatCurrency(report.CurrencyCode, pw.SettlementsAToB), true);
                        PdfTableCell(table, FormatCurrency(report.CurrencyCode, pw.SettlementsBToA), true);

                        var balanceColor = pw.NetBalance < 0 ? Colors.Red.Darken1
                            : pw.NetBalance > 0 ? Colors.Green.Darken2
                            : Colors.Grey.Darken1;

                        table.Cell()
                            .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                            .PaddingVertical(3).PaddingHorizontal(4)
                            .AlignRight()
                            .Text(FormatCurrency(report.CurrencyCode, pw.NetBalance))
                            .FontSize(8).Bold().FontColor(balanceColor);
                    }
                });
            }

            // ── Warnings ─────────────────────────────────────
            if (report.Warnings.Count > 0)
            {
                col.Item().PaddingTop(15).Text($"Advertencias ({report.Warnings.Count})")
                    .FontSize(11).Bold().FontColor(Colors.Orange.Darken2);

                col.Item().PaddingTop(2).Text(
                    "Las siguientes transacciones no tienen tipos de cambio configurados para todas las monedas del proyecto.")
                    .FontSize(8).Italic().FontColor(Colors.Grey.Darken2);

                col.Item().PaddingTop(4).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(55);
                        cols.RelativeColumn(4);
                        cols.ConstantColumn(65);
                        cols.ConstantColumn(75);
                    });

                    table.Header(header =>
                    {
                        PdfTableHeaderCell(header, "Tipo");
                        PdfTableHeaderCell(header, "Título");
                        PdfTableHeaderCell(header, "Fecha");
                        PdfTableHeaderCell(header, "Monto", true);
                    });

                    foreach (var w in report.Warnings)
                    {
                        PdfTableCell(table, w.TransactionType);
                        PdfTableCell(table, w.Title);
                        PdfTableCell(table, w.Date.ToString("yyyy-MM-dd"));
                        PdfTableCell(table, FormatCurrency(report.CurrencyCode, w.ConvertedAmount), true);
                    }
                });
            }
        });
    }
}
