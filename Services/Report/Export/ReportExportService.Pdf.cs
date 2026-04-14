using ProjectLedger.API.DTOs.Report;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ProjectLedger.API.Services.Report;

/// <summary>
/// PDF report generation using QuestPDF.
/// </summary>
public partial class ReportExportService
{
    // ════════════════════════════════════════════════════════
    //  EXPENSE REPORT — PDF
    // ════════════════════════════════════════════════════════

    /// <inheritdoc />
    public byte[] GenerateExpenseReportPdf(DetailedExpenseReportResponse report)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Element(h => ComposeExpenseReportHeader(h, report));
                page.Content().Element(c => ComposeExpenseReportContent(c, report));
                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    /// <summary>Composes the visual header for the expense report.</summary>
    private void ComposeExpenseReportHeader(IContainer container, DetailedExpenseReportResponse report)
    {
        container.Column(col =>
        {
            col.Item().Text(_localizer["RptFmt_ExpenseReportTitle", report.ProjectName].Value)
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
                    c.Item().Text(_localizer["RptCommon_TotalSpent"].Value).FontSize(8).FontColor(Colors.Grey.Darken2);
                    c.Item().Text($"{report.CurrencyCode} {report.TotalSpent:N2}").FontSize(14).Bold();
                });
                row.ConstantItem(10);
                row.RelativeItem().Background(Colors.Grey.Lighten3).Padding(8).Column(c =>
                {
                    c.Item().Text(_localizer["RptCommon_Transactions"].Value).FontSize(8).FontColor(Colors.Grey.Darken2);
                    c.Item().Text($"{report.TotalExpenseCount}").FontSize(14).Bold();
                });
                row.ConstantItem(10);
                row.RelativeItem().Background(Colors.Grey.Lighten3).Padding(8).Column(c =>
                {
                    c.Item().Text(_localizer["RptCommon_PeakMonth"].Value).FontSize(8).FontColor(Colors.Grey.Darken2);
                    c.Item().Text(GetPeakExpenseMonthLabel(report)).FontSize(10).Bold();
                });
            });

            col.Item().PaddingTop(6)
                .Text(_localizer["RptFmt_TopCategoryLine", GetTopExpenseCategoryLabel(report)].Value)
                .FontSize(8).FontColor(Colors.Grey.Darken1);

            col.Item().PaddingTop(2)
                .Text(_localizer["RptFmt_TotalIncomeLine",
                    FormatCurrency(report.CurrencyCode, report.TotalIncome),
                    FormatCurrency(report.CurrencyCode, report.NetBalance)].Value)
                .FontSize(8).FontColor(Colors.Grey.Darken1);

            // Alternative currency totals
            if (report.AlternativeCurrencies is { Count: > 0 })
            {
                foreach (var alt in report.AlternativeCurrencies)
                {
                    col.Item().PaddingTop(1)
                        .Text(_localizer["RptFmt_AltCurrencyExpenseLine",
                            alt.CurrencyCode, alt.TotalSpent, alt.TotalIncome, alt.NetBalance].Value)
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                }
            }

            col.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        });
    }

    /// <summary>Composes the main body sections of the expense report.</summary>
    private void ComposeExpenseReportContent(IContainer container, DetailedExpenseReportResponse report)
    {
        container.Column(col =>
        {
            if (report.Sections.Count == 0)
            {
                ComposePdfEmptyState(col, _localizer["RptExpense_NoDataMessage"].Value);
                return;
            }

            var altCodes = report.AlternativeCurrencies?.Select(a => a.CurrencyCode).ToList() ?? [];

            foreach (var section in report.Sections)
                ComposeExpenseSection(col, section, report.CurrencyCode, altCodes);

            if (report.CategoryAnalysis is { Count: > 0 })
                ComposeCategoryAnalysisSection(col, report);

            if (report.ObligationSummary is not null)
                ComposeObligationsSummarySection(col, report);
        });
    }

    /// <summary>Composes a monthly breakdown table for expenses.</summary>
    private void ComposeExpenseSection(
        ColumnDescriptor col,
        MonthlyExpenseSection section,
        string currencyCode,
        List<string> altCodes)
    {
        col.Item().PaddingTop(8).Text($"{section.MonthLabel}")
            .FontSize(12).Bold().FontColor(Colors.Blue.Darken2);

        // Section subtitle with alternative currency subtotals
        var subtotalText = _localizer["RptFmt_SubtotalExpenses",
            FormatCurrency(currencyCode, section.SectionTotal), section.SectionCount].Value;
        if (section.AlternativeCurrencies is { Count: > 0 })
        {
            foreach (var alt in section.AlternativeCurrencies)
                subtotalText += $"  ·  {alt.CurrencyCode}: {alt.TotalSpent:N2}";
        }
        col.Item().Text(subtotalText).FontSize(8).FontColor(Colors.Grey.Darken1);

        col.Item().PaddingTop(4).Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(65);
                cols.RelativeColumn(3);
                cols.RelativeColumn(2);
                cols.RelativeColumn(2);
                cols.ConstantColumn(70);
                foreach (var _ in altCodes)
                    cols.ConstantColumn(60);
            });

            table.Header(header =>
            {
                PdfTableHeaderCell(header, _localizer["RptCommon_Date"].Value);
                PdfTableHeaderCell(header, _localizer["RptCommon_Title"].Value);
                PdfTableHeaderCell(header, _localizer["RptCommon_Category"].Value);
                PdfTableHeaderCell(header, _localizer["RptCommon_PaymentMethod"].Value);
                PdfTableHeaderCell(header, _localizer["RptCommon_Amount"].Value, true);
                foreach (var code in altCodes)
                    PdfTableHeaderCell(header, code, true);
            });

            foreach (var exp in section.Expenses)
            {
                PdfTableCell(table, exp.ExpenseDate.ToString("yyyy-MM-dd"));
                PdfTableCell(table, exp.Title);
                PdfTableCell(table, exp.CategoryName);
                PdfTableCell(table, exp.PaymentMethodName);
                PdfTableCell(table, FormatCurrency(currencyCode, exp.ConvertedAmount), true);

                foreach (var code in altCodes)
                {
                    var altExchange = exp.CurrencyExchanges?
                        .FirstOrDefault(ce => ce.CurrencyCode == code);
                    PdfTableCell(table, altExchange is not null
                        ? $"{altExchange.ConvertedAmount:N2}" : "—", true);
                }
            }
        });
    }

    /// <summary>Composes the category-based budgetary analysis section.</summary>
    private void ComposeCategoryAnalysisSection(
        ColumnDescriptor col,
        DetailedExpenseReportResponse report)
    {
        col.Item().PaddingTop(15).Text(_localizer["RptExpense_CategoryAnalysis"].Value)
            .FontSize(14).Bold().FontColor(Colors.Blue.Darken3);

        col.Item().PaddingTop(4).Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn(3);
                cols.ConstantColumn(70);
                cols.ConstantColumn(70);
                cols.ConstantColumn(50);
                cols.ConstantColumn(70);
                cols.ConstantColumn(55);
            });

            table.Header(header =>
            {
                PdfTableHeaderCell(header, _localizer["RptCommon_Category"].Value);
                PdfTableHeaderCell(header, _localizer["RptExpense_Budget"].Value, true);
                PdfTableHeaderCell(header, _localizer["RptExpense_Spent"].Value, true);
                PdfTableHeaderCell(header, "%", true);
                PdfTableHeaderCell(header, _localizer["RptExpense_Remaining"].Value, true);
                PdfTableHeaderCell(header, _localizer["RptCommon_Status"].Value);
            });

            foreach (var cat in report.CategoryAnalysis)
            {
                PdfTableCell(table, cat.CategoryName);
                PdfTableCell(table, cat.BudgetAmount.HasValue
                    ? FormatCurrency(report.CurrencyCode, cat.BudgetAmount.Value) : "—", true);
                PdfTableCell(table, FormatCurrency(report.CurrencyCode, cat.SpentAmount), true);
                PdfTableCell(table, $"{cat.Percentage:N1}%", true);
                PdfTableCell(table, cat.BudgetRemaining.HasValue
                    ? FormatCurrency(report.CurrencyCode, cat.BudgetRemaining.Value) : "—", true);

                table.Cell()
                    .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                    .PaddingVertical(3).PaddingHorizontal(4)
                    .Text(cat.BudgetExceeded == true ? _localizer["RptExpense_ExceededMark"].Value : "OK")
                    .FontSize(8)
                    .FontColor(cat.BudgetExceeded == true ? Colors.Red.Darken1 : Colors.Green.Darken2);
            }
        });
    }

    /// <summary>Composes the summary and detailed breakdown of obligations related to expenses.</summary>
    private void ComposeObligationsSummarySection(
        ColumnDescriptor col,
        DetailedExpenseReportResponse report)
    {
        var obl = report.ObligationSummary!;

        col.Item().PaddingTop(15).Text(_localizer["RptExpense_ObligationSummary"].Value)
            .FontSize(14).Bold().FontColor(Colors.Blue.Darken3);

        col.Item().PaddingTop(4).Row(row =>
        {
            void SummaryBox(IContainer c, string label, string value, string color) =>
                c.Background(color).Padding(6).Column(inner =>
                {
                    inner.Item().Text(label).FontSize(7).FontColor(Colors.Grey.Darken2);
                    inner.Item().Text(value).FontSize(11).Bold();
                });

            row.RelativeItem().Element(c => SummaryBox(c, _localizer["RptCommon_Total"].Value,   FormatCurrency(report.CurrencyCode, obl.TotalAmount),   Colors.Grey.Lighten3));
            row.ConstantItem(6);
            row.RelativeItem().Element(c => SummaryBox(c, _localizer["RptCommon_Paid"].Value,    FormatCurrency(report.CurrencyCode, obl.TotalPaid),     Colors.Green.Lighten4));
            row.ConstantItem(6);
            row.RelativeItem().Element(c => SummaryBox(c, _localizer["RptCommon_Pending"].Value, FormatCurrency(report.CurrencyCode, obl.TotalPending),  Colors.Orange.Lighten4));
            row.ConstantItem(6);
            row.RelativeItem().Element(c => SummaryBox(c, _localizer["RptCommon_Overdue"].Value, $"{obl.OverdueCount}",                                  Colors.Red.Lighten4));
        });

        col.Item().PaddingTop(6).Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(60);
                cols.RelativeColumn(3);
                cols.ConstantColumn(70);
                cols.ConstantColumn(70);
                cols.ConstantColumn(70);
                cols.ConstantColumn(65);
            });

            table.Header(header =>
            {
                PdfTableHeaderCell(header, _localizer["RptCommon_Status"].Value);
                PdfTableHeaderCell(header, _localizer["RptCommon_Title"].Value);
                PdfTableHeaderCell(header, _localizer["RptCommon_Total"].Value,    true);
                PdfTableHeaderCell(header, _localizer["RptCommon_Paid"].Value,     true);
                PdfTableHeaderCell(header, _localizer["RptExpense_Remaining"].Value, true);
                PdfTableHeaderCell(header, _localizer["RptExpense_DueDate"].Value);
            });

            foreach (var group in obl.ByStatus)
            {
                foreach (var item in group.Obligations)
                {
                    var statusColor = item.Status switch
                    {
                        "overdue"        => Colors.Red.Darken1,
                        "paid"           => Colors.Green.Darken2,
                        "partially_paid" => Colors.Orange.Darken2,
                        _                => Colors.Grey.Darken1
                    };

                    table.Cell()
                        .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                        .PaddingVertical(3).PaddingHorizontal(4)
                        .Text(FormatStatus(item.Status)).FontSize(8).FontColor(statusColor);

                    PdfTableCell(table, item.Title);
                    PdfTableCell(table, FormatCurrency(item.Currency, item.TotalAmount),     true);
                    PdfTableCell(table, FormatCurrency(item.Currency, item.PaidAmount),      true);
                    PdfTableCell(table, FormatCurrency(item.Currency, item.RemainingAmount), true);
                    PdfTableCell(table, item.DueDate?.ToString("yyyy-MM-dd") ?? "—");
                }
            }
        });
    }

}
