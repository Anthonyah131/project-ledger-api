using ProjectLedger.API.DTOs.Report;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ProjectLedger.API.Services.Report;

public partial class ReportExportService
{
    // ════════════════════════════════════════════════════════
    //  PAYMENT METHOD REPORT — PDF
    // ════════════════════════════════════════════════════════

    /// <inheritdoc />
    public byte[] GeneratePaymentMethodReportPdf(PaymentMethodReportResponse report)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Element(h => ComposePaymentMethodHeader(h, report));
                page.Content().Element(c => ComposePaymentMethodContent(c, report));
                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    /// <summary>Composes the visual header for the payment method report.</summary>
    private void ComposePaymentMethodHeader(IContainer container, PaymentMethodReportResponse report)
    {
        container.Column(col =>
        {
            col.Item().Text(_localizer["RptPaymentMethod_ReportTitle"].Value)
                .FontSize(16).Bold().FontColor(Colors.Blue.Darken3);

            col.Item().Row(row =>
            {
                row.RelativeItem().Text($"{_localizer["RptCommon_Period"].Value}: {FormatDateRange(report.DateFrom, report.DateTo)}").FontSize(9);
                row.RelativeItem().AlignRight()
                    .Text($"{_localizer["RptCommon_Generated"].Value}: {report.GeneratedAt:yyyy-MM-dd HH:mm} UTC").FontSize(8);
            });

            col.Item().PaddingTop(4)
                .Text(_localizer["RptFmt_PaymentMethodCount",
                    report.PaymentMethods.Count,
                    report.PaymentMethods.Count(pm => !pm.IsInactive)].Value)
                .FontSize(9).FontColor(Colors.Grey.Darken1);

            col.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        });
    }

    /// <summary>Composes the main body sections of the payment method report.</summary>
    private void ComposePaymentMethodContent(IContainer container, PaymentMethodReportResponse report)
    {
        container.Column(col =>
        {
            if (report.PaymentMethods.Count == 0)
            {
                ComposePdfEmptyState(col, _localizer["RptPaymentMethod_NoActivity"].Value);
                return;
            }

            foreach (var pm in report.PaymentMethods)
                ComposePaymentMethodSection(col, pm);

            if (report.MonthlyTrend.Count > 0)
                ComposePaymentMethodMonthlyTrendSection(col, report);
        });
    }

    /// <summary>Composes a detailed summary section for a specific payment method.</summary>
    private void ComposePaymentMethodSection(ColumnDescriptor col, PaymentMethodReportRow pm)
    {
        // ── Method header ──
        col.Item().PaddingTop(10).Text($"{pm.Name}")
            .FontSize(13).Bold().FontColor(Colors.Blue.Darken2);

        col.Item().Text($"{pm.Type}  ·  {pm.Currency}" +
                        (pm.BankName is not null ? $"  ·  {pm.BankName}" : "") +
                        (pm.OwnerPartnerName is not null
                            ? _localizer["RptFmt_OwnerSuffix", pm.OwnerPartnerName].Value
                            : ""))
            .FontSize(8).FontColor(Colors.Grey.Darken1);

        // ── Method metrics ──
        col.Item().PaddingTop(4).Row(row =>
        {
            row.RelativeItem().Background(Colors.Blue.Lighten4).Padding(6).Column(c =>
            {
                c.Item().Text(_localizer["RptCommon_TotalSpent"].Value).FontSize(7).FontColor(Colors.Grey.Darken2);
                c.Item().Text(FormatCurrency(pm.Currency, pm.TotalSpent)).FontSize(11).Bold();
            });
            row.ConstantItem(6);
            row.RelativeItem().Background(Colors.Green.Lighten4).Padding(6).Column(c =>
            {
                c.Item().Text(_localizer["RptCommon_TotalIncome"].Value).FontSize(7).FontColor(Colors.Grey.Darken2);
                c.Item().Text(FormatCurrency(pm.Currency, pm.TotalIncome)).FontSize(11).Bold();
            });
            row.ConstantItem(6);
            row.RelativeItem().Background(Colors.Orange.Lighten4).Padding(6).Column(c =>
            {
                c.Item().Text(_localizer["RptCommon_NetBalance"].Value).FontSize(7).FontColor(Colors.Grey.Darken2);
                c.Item().Text(FormatCurrency(pm.Currency, pm.NetFlow)).FontSize(11).Bold();
            });
            row.ConstantItem(6);
            row.RelativeItem().Background(Colors.Grey.Lighten3).Padding(6).Column(c =>
            {
                c.Item().Text(_localizer["RptCommon_Transactions"].Value).FontSize(7).FontColor(Colors.Grey.Darken2);
                c.Item().Text(_localizer["RptFmt_ExpenseIncomeCount", pm.ExpenseCount, pm.IncomeCount].Value).FontSize(9).Bold();
            });
        });

        // ── Top categories ──
        if (pm.TopCategories.Count > 0)
        {
            col.Item().PaddingTop(6).Text(_localizer["RptPaymentMethod_TopCategories"].Value).FontSize(9).Bold();
            col.Item().PaddingTop(2).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(3);
                    cols.ConstantColumn(70);
                    cols.ConstantColumn(40);
                    cols.ConstantColumn(40);
                });

                table.Header(header =>
                {
                    PdfTableHeaderCell(header, _localizer["RptCommon_Category"].Value);
                    PdfTableHeaderCell(header, _localizer["RptCommon_Total"].Value, true);
                    PdfTableHeaderCell(header, _localizer["RptCommon_ExpenseCount"].Value, true);
                    PdfTableHeaderCell(header, "%", true);
                });

                foreach (var cat in pm.TopCategories)
                {
                    PdfTableCell(table, cat.CategoryName);
                    PdfTableCell(table, FormatCurrency(pm.Currency, cat.TotalAmount), true);
                    PdfTableCell(table, $"{cat.ExpenseCount}", true);
                    PdfTableCell(table, $"{cat.Percentage:N1}%", true);
                }
            });
        }

        // ── Projects breakdown ──
        if (pm.Projects.Count > 0)
        {
            col.Item().PaddingTop(6).Text(_localizer["RptCommon_Projects"].Value).FontSize(9).Bold();
            col.Item().PaddingTop(2).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(3);
                    cols.ConstantColumn(50);
                    cols.ConstantColumn(70);
                    cols.ConstantColumn(40);
                    cols.ConstantColumn(40);
                });

                table.Header(header =>
                {
                    PdfTableHeaderCell(header, _localizer["RptCommon_Projects"].Value);
                    PdfTableHeaderCell(header, _localizer["RptPaymentMethod_AbbrevCurrency"].Value);
                    PdfTableHeaderCell(header, _localizer["RptCommon_Total"].Value, true);
                    PdfTableHeaderCell(header, _localizer["RptPaymentMethod_AbbrevExpenses"].Value, true);
                    PdfTableHeaderCell(header, "%", true);
                });

                foreach (var proj in pm.Projects)
                {
                    PdfTableCell(table, proj.ProjectName);
                    PdfTableCell(table, proj.ProjectCurrency);
                    PdfTableCell(table, FormatCurrency(proj.ProjectCurrency, proj.TotalSpent), true);
                    PdfTableCell(table, $"{proj.ExpenseCount}", true);
                    PdfTableCell(table, $"{proj.Percentage:N1}%", true);
                }
            });
        }

        // ── Expenses ──
        if (pm.Expenses.Count > 0)
        {
            col.Item().PaddingTop(6).Text(_localizer["RptFmt_GastosShown", pm.ExpensesShown, pm.TotalExpensesInPeriod].Value)
                .FontSize(9).Bold();
            col.Item().PaddingTop(2).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(60);
                    cols.RelativeColumn(3);
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(2);
                    cols.ConstantColumn(70);
                });

                table.Header(header =>
                {
                    PdfTableHeaderCell(header, _localizer["RptCommon_Date"].Value);
                    PdfTableHeaderCell(header, _localizer["RptCommon_Title"].Value);
                    PdfTableHeaderCell(header, _localizer["RptCommon_Projects"].Value);
                    PdfTableHeaderCell(header, _localizer["RptCommon_Category"].Value);
                    PdfTableHeaderCell(header, _localizer["RptCommon_Amount"].Value, true);
                });

                foreach (var exp in pm.Expenses)
                {
                    PdfTableCell(table, exp.ExpenseDate.ToString("yyyy-MM-dd"));
                    PdfTableCell(table, exp.Title);
                    PdfTableCell(table, exp.ProjectName);
                    PdfTableCell(table, exp.CategoryName);
                    PdfTableCell(table, FormatCurrency(pm.Currency, exp.Amount), true);
                }
            });
        }

        // ── Incomes ──
        if (pm.Incomes.Count > 0)
        {
            col.Item().PaddingTop(6).Text(_localizer["RptFmt_IncomesShown", pm.IncomesShown, pm.TotalIncomesInPeriod].Value)
                .FontSize(9).Bold();
            col.Item().PaddingTop(2).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(60);
                    cols.RelativeColumn(3);
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(2);
                    cols.ConstantColumn(70);
                });

                table.Header(header =>
                {
                    PdfTableHeaderCell(header, _localizer["RptCommon_Date"].Value);
                    PdfTableHeaderCell(header, _localizer["RptCommon_Title"].Value);
                    PdfTableHeaderCell(header, _localizer["RptCommon_Projects"].Value);
                    PdfTableHeaderCell(header, _localizer["RptCommon_Category"].Value);
                    PdfTableHeaderCell(header, _localizer["RptCommon_Amount"].Value, true);
                });

                foreach (var inc in pm.Incomes)
                {
                    PdfTableCell(table, inc.IncomeDate.ToString("yyyy-MM-dd"));
                    PdfTableCell(table, inc.Title);
                    PdfTableCell(table, inc.ProjectName);
                    PdfTableCell(table, inc.CategoryName);
                    PdfTableCell(table, FormatCurrency(pm.Currency, inc.Amount), true);
                }
            });
        }

        col.Item().PaddingTop(8).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
    }

    /// <summary>Composes the monthly trend analysis section for payment methods.</summary>
    private void ComposePaymentMethodMonthlyTrendSection(ColumnDescriptor col, PaymentMethodReportResponse report)
    {
        col.Item().PaddingTop(12).Text(_localizer["RptPaymentMethod_MonthlyTrend"].Value)
            .FontSize(12).Bold().FontColor(Colors.Blue.Darken3);

        col.Item().PaddingTop(4).Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn(2);
                cols.RelativeColumn(2);
                cols.ConstantColumn(40);
                cols.ConstantColumn(65);
                cols.ConstantColumn(40);
                cols.ConstantColumn(65);
                cols.ConstantColumn(40);
                cols.ConstantColumn(65);
            });

            table.Header(header =>
            {
                PdfTableHeaderCell(header, _localizer["RptCommon_Month"].Value);
                PdfTableHeaderCell(header, _localizer["RptCommon_PaymentMethod"].Value);
                PdfTableHeaderCell(header, _localizer["RptPaymentMethod_AbbrevCurrency"].Value);
                PdfTableHeaderCell(header, _localizer["RptCommon_TotalSpent"].Value, true);
                PdfTableHeaderCell(header, _localizer["RptPaymentMethod_AbbrevExpenses"].Value, true);
                PdfTableHeaderCell(header, _localizer["RptCommon_TotalIncome"].Value, true);
                PdfTableHeaderCell(header, _localizer["RptPaymentMethod_AbbrevIncomes"].Value, true);
                PdfTableHeaderCell(header, _localizer["RptCommon_Balance"].Value, true);
            });

            foreach (var m in report.MonthlyTrend)
            {
                foreach (var bm in m.ByMethod)
                {
                    PdfTableCell(table, m.MonthLabel);
                    PdfTableCell(table, bm.Name);
                    PdfTableCell(table, bm.Currency);
                    PdfTableCell(table, $"{bm.TotalSpent:N2}", true);
                    PdfTableCell(table, $"{bm.ExpenseCount}", true);
                    PdfTableCell(table, $"{bm.TotalIncome:N2}", true);
                    PdfTableCell(table, $"{bm.IncomeCount}", true);
                    PdfTableCell(table, $"{bm.NetFlow:N2}", true);
                }
            }
        });
    }
}
