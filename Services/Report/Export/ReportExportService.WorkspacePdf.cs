using ProjectLedger.API.DTOs.Report;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ProjectLedger.API.Services.Report;

public partial class ReportExportService
{
    // ════════════════════════════════════════════════════════
    //  WORKSPACE REPORT — PDF
    // ════════════════════════════════════════════════════════

    /// <inheritdoc />
    public byte[] GenerateWorkspaceReportPdf(WorkspaceReportResponse report)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Element(h => ComposeWorkspaceReportHeader(h, report));
                page.Content().Element(c => ComposeWorkspaceReportContent(c, report));
                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    /// <summary>Composes the visual header for the workspace report.</summary>
    private void ComposeWorkspaceReportHeader(IContainer container, WorkspaceReportResponse report)
    {
        container.Column(col =>
        {
            col.Item().Text(_localizer["RptFmt_WorkspaceReportTitle", report.WorkspaceName].Value)
                .FontSize(16).Bold().FontColor(Colors.Blue.Darken3);

            col.Item().Row(row =>
            {
                row.RelativeItem().Text($"{_localizer["RptCommon_Period"].Value}: {FormatDateRange(report.DateFrom, report.DateTo)}").FontSize(9);
                row.RelativeItem().AlignRight()
                    .Text($"{_localizer["RptCommon_Generated"].Value}: {report.GeneratedAt:yyyy-MM-dd HH:mm} UTC").FontSize(8);
            });

            col.Item().PaddingTop(5).Row(row =>
            {
                if (report.ConsolidatedTotals is not null)
                {
                    row.RelativeItem().Background(Colors.Blue.Lighten4).Padding(8).Column(c =>
                    {
                        c.Item().Text(_localizer["RptCommon_TotalSpent"].Value).FontSize(8).FontColor(Colors.Grey.Darken2);
                        c.Item().Text(FormatCurrency(report.ReferenceCurrency ?? "", report.ConsolidatedTotals.TotalSpent))
                            .FontSize(14).Bold();
                    });
                    row.ConstantItem(10);
                    row.RelativeItem().Background(Colors.Green.Lighten4).Padding(8).Column(c =>
                    {
                        c.Item().Text(_localizer["RptCommon_TotalIncome"].Value).FontSize(8).FontColor(Colors.Grey.Darken2);
                        c.Item().Text(FormatCurrency(report.ReferenceCurrency ?? "", report.ConsolidatedTotals.TotalIncome))
                            .FontSize(14).Bold();
                    });
                    row.ConstantItem(10);
                    row.RelativeItem().Background(Colors.Orange.Lighten4).Padding(8).Column(c =>
                    {
                        c.Item().Text(_localizer["RptCommon_NetBalance"].Value).FontSize(8).FontColor(Colors.Grey.Darken2);
                        c.Item().Text(FormatCurrency(report.ReferenceCurrency ?? "", report.ConsolidatedTotals.NetBalance))
                            .FontSize(14).Bold();
                    });
                }
                else
                {
                    row.RelativeItem().Background(Colors.Grey.Lighten3).Padding(8).Column(c =>
                    {
                        c.Item().Text(_localizer["RptCommon_Projects"].Value).FontSize(8).FontColor(Colors.Grey.Darken2);
                        c.Item().Text($"{report.ProjectCount}").FontSize(14).Bold();
                    });
                    row.ConstantItem(10);
                    row.RelativeItem().Background(Colors.Orange.Lighten4).Padding(8).Column(c =>
                    {
                        c.Item().Text(_localizer["RptWorkspace_MultiCurrencies"].Value).FontSize(8).FontColor(Colors.Grey.Darken2);
                        c.Item().Text(_localizer["RptWorkspace_NotConsolidated"].Value).FontSize(10).Bold();
                    });
                }

                row.ConstantItem(10);
                row.RelativeItem().Background(Colors.Grey.Lighten3).Padding(8).Column(c =>
                {
                    c.Item().Text(_localizer["RptCommon_Projects"].Value).FontSize(8).FontColor(Colors.Grey.Darken2);
                    c.Item().Text($"{report.ProjectCount}").FontSize(14).Bold();
                });
            });

            col.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        });
    }

    /// <summary>Composes the main body sections of the workspace report.</summary>
    private void ComposeWorkspaceReportContent(IContainer container, WorkspaceReportResponse report)
    {
        container.Column(col =>
        {
            if (report.Projects.Count == 0)
            {
                ComposePdfEmptyState(col, _localizer["RptWorkspace_NoProjects"].Value);
                return;
            }

            // ── Projects table ───────────────────────────────
            col.Item().Text(_localizer["RptWorkspace_ProjectSummary"].Value)
                .FontSize(12).Bold().FontColor(Colors.Blue.Darken2);

            col.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(3);
                    cols.ConstantColumn(45);
                    cols.ConstantColumn(70);
                    cols.ConstantColumn(70);
                    cols.ConstantColumn(70);
                    cols.ConstantColumn(40);
                    cols.ConstantColumn(40);
                });

                table.Header(header =>
                {
                    PdfTableHeaderCell(header, _localizer["RptCommon_Projects"].Value);
                    PdfTableHeaderCell(header, _localizer["RptCommon_Currency"].Value);
                    PdfTableHeaderCell(header, _localizer["RptCommon_TotalSpent"].Value, true);
                    PdfTableHeaderCell(header, _localizer["RptCommon_TotalIncome"].Value, true);
                    PdfTableHeaderCell(header, _localizer["RptCommon_Balance"].Value, true);
                    PdfTableHeaderCell(header, _localizer["RptWorkspace_AbbrevExpenses"].Value, true);
                    PdfTableHeaderCell(header, _localizer["RptWorkspace_AbbrevIncomes"].Value, true);
                });

                foreach (var p in report.Projects)
                {
                    PdfTableCell(table, p.ProjectName);
                    PdfTableCell(table, p.CurrencyCode);
                    PdfTableCell(table, FormatCurrency(p.CurrencyCode, p.TotalSpent), true);
                    PdfTableCell(table, FormatCurrency(p.CurrencyCode, p.TotalIncome), true);
                    PdfTableCell(table, FormatCurrency(p.CurrencyCode, p.NetBalance), true);
                    PdfTableCell(table, $"{p.ExpenseCount}", true);
                    PdfTableCell(table, $"{p.IncomeCount}", true);
                }
            });

            // ── Categories ───────────────────────────────────
            if (report.ConsolidatedByCategory.Count > 0)
            {
                col.Item().PaddingTop(15).Text(_localizer["RptWorkspace_CrossCategories"].Value)
                    .FontSize(12).Bold().FontColor(Colors.Blue.Darken2);

                col.Item().PaddingTop(4).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(3);
                        cols.ConstantColumn(70);
                        cols.ConstantColumn(50);
                        cols.ConstantColumn(50);
                        cols.ConstantColumn(50);
                    });

                    table.Header(header =>
                    {
                        PdfTableHeaderCell(header, _localizer["RptCommon_Category"].Value);
                        PdfTableHeaderCell(header, _localizer["RptCommon_Total"].Value, true);
                        PdfTableHeaderCell(header, "%", true);
                        PdfTableHeaderCell(header, _localizer["RptCommon_Projects"].Value, true);
                        PdfTableHeaderCell(header, _localizer["RptCommon_ExpenseCount"].Value, true);
                    });

                    foreach (var cat in report.ConsolidatedByCategory)
                    {
                        PdfTableCell(table, cat.CategoryName);
                        PdfTableCell(table, $"{cat.TotalAmount:N2}", true);
                        PdfTableCell(table, $"{cat.Percentage:N1}%", true);
                        PdfTableCell(table, $"{cat.ProjectCount}", true);
                        PdfTableCell(table, $"{cat.ExpenseCount}", true);
                    }
                });
            }

            // ── Monthly trend ────────────────────────────────
            if (report.MonthlyTrend.Count > 0)
            {
                col.Item().PaddingTop(15).Text(_localizer["RptWorkspace_MonthlyTrend"].Value)
                    .FontSize(12).Bold().FontColor(Colors.Blue.Darken2);

                col.Item().PaddingTop(4).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(3);
                        cols.ConstantColumn(70);
                        cols.ConstantColumn(70);
                        cols.ConstantColumn(70);
                        cols.ConstantColumn(40);
                        cols.ConstantColumn(40);
                    });

                    table.Header(header =>
                    {
                        PdfTableHeaderCell(header, _localizer["RptCommon_Month"].Value);
                        PdfTableHeaderCell(header, _localizer["RptCommon_TotalSpent"].Value, true);
                        PdfTableHeaderCell(header, _localizer["RptCommon_TotalIncome"].Value, true);
                        PdfTableHeaderCell(header, _localizer["RptCommon_Balance"].Value, true);
                        PdfTableHeaderCell(header, _localizer["RptWorkspace_AbbrevExpenses"].Value, true);
                        PdfTableHeaderCell(header, _localizer["RptWorkspace_AbbrevIncomes"].Value, true);
                    });

                    foreach (var m in report.MonthlyTrend)
                    {
                        PdfTableCell(table, m.MonthLabel);
                        PdfTableCell(table, $"{m.TotalSpent:N2}", true);
                        PdfTableCell(table, $"{m.TotalIncome:N2}", true);
                        PdfTableCell(table, $"{m.NetBalance:N2}", true);
                        PdfTableCell(table, $"{m.ExpenseCount}", true);
                        PdfTableCell(table, $"{m.IncomeCount}", true);
                    }
                });
            }
        });
    }
}
