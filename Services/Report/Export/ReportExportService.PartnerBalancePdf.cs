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
    private void ComposePartnerBalanceHeader(IContainer container, PartnerBalanceReportResponse report)
    {
        container.Column(col =>
        {
            col.Item().Text(_localizer["RptFmt_PartnerBalanceTitle", report.ProjectName].Value)
                .FontSize(16).Bold().FontColor(Colors.Blue.Darken3);

            col.Item().Row(row =>
            {
                row.RelativeItem().Text($"{_localizer["RptCommon_Currency"].Value}: {report.CurrencyCode}").FontSize(9);
                row.RelativeItem().Text($"{_localizer["RptCommon_Period"].Value}: {FormatDateRange(report.DateFrom, report.DateTo)}").FontSize(9);
                row.RelativeItem().AlignRight()
                    .Text($"{_localizer["RptCommon_Generated"].Value}: {report.GeneratedAt:yyyy-MM-dd HH:mm} UTC").FontSize(8);
            });

            col.Item().PaddingTop(5).Row(row =>
            {
                row.RelativeItem().Background(Colors.Blue.Lighten4).Padding(8).Column(c =>
                {
                    c.Item().Text(_localizer["RptCommon_Partner"].Value).FontSize(8).FontColor(Colors.Grey.Darken2);
                    c.Item().Text($"{report.Partners.Count}").FontSize(14).Bold();
                });
                row.ConstantItem(10);
                row.RelativeItem().Background(Colors.Grey.Lighten3).Padding(8).Column(c =>
                {
                    c.Item().Text(_localizer["RptCommon_Settlements"].Value).FontSize(8).FontColor(Colors.Grey.Darken2);
                    c.Item().Text($"{report.Settlements.Count}").FontSize(14).Bold();
                });
                row.ConstantItem(10);
                row.RelativeItem().Background(Colors.Grey.Lighten3).Padding(8).Column(c =>
                {
                    c.Item().Text(_localizer["RptPartnerBalance_PairsWithBalance"].Value).FontSize(8).FontColor(Colors.Grey.Darken2);
                    c.Item().Text($"{report.PairwiseBalances.Count}").FontSize(14).Bold();
                });
            });

            col.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        });
    }

    /// <summary>Composes the main body sections of the partner balance report.</summary>
    private void ComposePartnerBalanceContent(IContainer container, PartnerBalanceReportResponse report)
    {
        container.Column(col =>
        {
            if (report.Partners.Count == 0)
            {
                ComposePdfEmptyState(col, _localizer["RptPartnerBalance_NoActivity"].Value);
                return;
            }

            // ── Partner balance table ────────────────────────
            col.Item().Text(_localizer["RptPartnerBalance_PdfTitle"].Value)
                .FontSize(12).Bold().FontColor(Colors.Blue.Darken2);

            col.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(3);   // Partner
                    cols.ConstantColumn(55);  // Paid Physically
                    cols.ConstantColumn(55);  // Others Owe
                    cols.ConstantColumn(55);  // He Owes
                    cols.ConstantColumn(55);  // Stl. Paid
                    cols.ConstantColumn(55);  // Stl. Received
                    cols.ConstantColumn(60);  // Net Balance
                });

                table.Header(header =>
                {
                    PdfTableHeaderCell(header, _localizer["RptCommon_Partner"].Value);
                    PdfTableHeaderCell(header, _localizer["RptPartnerBalance_PaidPhysically"].Value, true);
                    PdfTableHeaderCell(header, _localizer["RptPartnerBalance_OthersOwe"].Value, true);
                    PdfTableHeaderCell(header, _localizer["RptPartnerBalance_HeOwes"].Value, true);
                    PdfTableHeaderCell(header, _localizer["RptPartnerBalance_StlPaid"].Value, true);
                    PdfTableHeaderCell(header, _localizer["RptPartnerBalance_StlReceived"].Value, true);
                    PdfTableHeaderCell(header, _localizer["RptCommon_Balance"].Value, true);
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
                col.Item().PaddingTop(15).Text(_localizer["RptCommon_Settlements"].Value)
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
                        PdfTableHeaderCell(header, _localizer["RptCommon_Date"].Value);
                        PdfTableHeaderCell(header, _localizer["RptPartnerBalance_From"].Value);
                        PdfTableHeaderCell(header, _localizer["RptPartnerBalance_To"].Value);
                        PdfTableHeaderCell(header, _localizer["RptCommon_Amount"].Value, true);
                        PdfTableHeaderCell(header, _localizer["RptExpense_ConvertedAmount"].Value, true);
                        PdfTableHeaderCell(header, _localizer["RptCommon_Description"].Value);
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
                col.Item().PaddingTop(15).Text(_localizer["RptPartnerBalance_PairwiseTitle"].Value)
                    .FontSize(12).Bold().FontColor(Colors.Blue.Darken2);

                col.Item().PaddingTop(4).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(2);  // Partner A
                        cols.RelativeColumn(2);  // Partner B
                        cols.ConstantColumn(60); // A owes B
                        cols.ConstantColumn(60); // B owes A
                        cols.ConstantColumn(60); // Stl. A→B
                        cols.ConstantColumn(60); // Stl. B→A
                        cols.ConstantColumn(65); // Net Balance
                    });

                    table.Header(header =>
                    {
                        PdfTableHeaderCell(header, "Partner A");
                        PdfTableHeaderCell(header, "Partner B");
                        PdfTableHeaderCell(header, _localizer["RptPartnerBalance_AOwesB"].Value, true);
                        PdfTableHeaderCell(header, _localizer["RptPartnerBalance_BOwesA"].Value, true);
                        PdfTableHeaderCell(header, _localizer["RptPartnerBalance_StlPaid"].Value, true);
                        PdfTableHeaderCell(header, _localizer["RptPartnerBalance_StlReceived"].Value, true);
                        PdfTableHeaderCell(header, _localizer["RptCommon_Balance"].Value, true);
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
                col.Item().PaddingTop(15).Text(_localizer["RptFmt_WarningsCount", report.Warnings.Count].Value)
                    .FontSize(11).Bold().FontColor(Colors.Orange.Darken2);

                col.Item().PaddingTop(2).Text(_localizer["RptPartnerBalance_WarningsNote"].Value)
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
                        PdfTableHeaderCell(header, _localizer["RptCommon_Type"].Value);
                        PdfTableHeaderCell(header, _localizer["RptCommon_Title"].Value);
                        PdfTableHeaderCell(header, _localizer["RptCommon_Date"].Value);
                        PdfTableHeaderCell(header, _localizer["RptCommon_Amount"].Value, true);
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
