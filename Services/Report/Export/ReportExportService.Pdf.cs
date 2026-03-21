using ProjectLedger.API.DTOs.Report;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ProjectLedger.API.Services.Report;

/// <summary>
/// Generación de reportes en formato PDF usando QuestPDF.
/// </summary>
public partial class ReportExportService
{
    // ════════════════════════════════════════════════════════
    //  EXPENSE REPORT — PDF
    // ════════════════════════════════════════════════════════

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

    private static void ComposeExpenseReportHeader(IContainer container, DetailedExpenseReportResponse report)
    {
        container.Column(col =>
        {
            col.Item().Text($"Reporte de Gastos — {report.ProjectName}")
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
                    c.Item().Text("Total Gastado").FontSize(8).FontColor(Colors.Grey.Darken2);
                    c.Item().Text($"{report.CurrencyCode} {report.TotalSpent:N2}").FontSize(14).Bold();
                });
                row.ConstantItem(10);
                row.RelativeItem().Background(Colors.Grey.Lighten3).Padding(8).Column(c =>
                {
                    c.Item().Text("Transacciones").FontSize(8).FontColor(Colors.Grey.Darken2);
                    c.Item().Text($"{report.TotalExpenseCount}").FontSize(14).Bold();
                });
                row.ConstantItem(10);
                row.RelativeItem().Background(Colors.Grey.Lighten3).Padding(8).Column(c =>
                {
                    c.Item().Text("Mes Pico").FontSize(8).FontColor(Colors.Grey.Darken2);
                    c.Item().Text(GetPeakExpenseMonthLabel(report)).FontSize(10).Bold();
                });
            });

            col.Item().PaddingTop(6)
                .Text($"Top categoría: {GetTopExpenseCategoryLabel(report)}")
                .FontSize(8).FontColor(Colors.Grey.Darken1);

            col.Item().PaddingTop(2)
                .Text($"Total ingresos: {FormatCurrency(report.CurrencyCode, report.TotalIncome)}" +
                      $"  ·  Balance neto: {FormatCurrency(report.CurrencyCode, report.NetBalance)}")
                .FontSize(8).FontColor(Colors.Grey.Darken1);

            // Alternative currency totals
            if (report.AlternativeCurrencies is { Count: > 0 })
            {
                foreach (var alt in report.AlternativeCurrencies)
                {
                    col.Item().PaddingTop(1)
                        .Text($"[{alt.CurrencyCode}]  Gastado: {alt.TotalSpent:N2}" +
                              $"  ·  Ingresos: {alt.TotalIncome:N2}" +
                              $"  ·  Balance: {alt.NetBalance:N2}")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                }
            }

            col.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        });
    }

    private static void ComposeExpenseReportContent(IContainer container, DetailedExpenseReportResponse report)
    {
        container.Column(col =>
        {
            if (report.Sections.Count == 0)
            {
                ComposePdfEmptyState(col, "No se encontraron gastos para el período seleccionado.");
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

    private static void ComposeExpenseSection(
        ColumnDescriptor col,
        MonthlyExpenseSection section,
        string currencyCode,
        List<string> altCodes)
    {
        col.Item().PaddingTop(8).Text($"{section.MonthLabel}")
            .FontSize(12).Bold().FontColor(Colors.Blue.Darken2);

        // Section subtitle with alternative currency subtotals
        var subtotalText = $"Subtotal: {FormatCurrency(currencyCode, section.SectionTotal)}  ·  {section.SectionCount} gastos";
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
                PdfTableHeaderCell(header, "Fecha");
                PdfTableHeaderCell(header, "Título");
                PdfTableHeaderCell(header, "Categoría");
                PdfTableHeaderCell(header, "Método de Pago");
                PdfTableHeaderCell(header, "Monto", true);
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

    private static void ComposeCategoryAnalysisSection(
        ColumnDescriptor col,
        DetailedExpenseReportResponse report)
    {
        col.Item().PaddingTop(15).Text("Análisis por Categoría")
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
                PdfTableHeaderCell(header, "Categoría");
                PdfTableHeaderCell(header, "Presupuesto", true);
                PdfTableHeaderCell(header, "Gastado", true);
                PdfTableHeaderCell(header, "%", true);
                PdfTableHeaderCell(header, "Restante", true);
                PdfTableHeaderCell(header, "Estado");
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
                    .Text(cat.BudgetExceeded == true ? "⚠ Excedido" : "OK")
                    .FontSize(8)
                    .FontColor(cat.BudgetExceeded == true ? Colors.Red.Darken1 : Colors.Green.Darken2);
            }
        });
    }

    private static void ComposeObligationsSummarySection(
        ColumnDescriptor col,
        DetailedExpenseReportResponse report)
    {
        var obl = report.ObligationSummary!;

        col.Item().PaddingTop(15).Text("Resumen de Obligaciones")
            .FontSize(14).Bold().FontColor(Colors.Blue.Darken3);

        col.Item().PaddingTop(4).Row(row =>
        {
            void SummaryBox(IContainer c, string label, string value, string color) =>
                c.Background(color).Padding(6).Column(inner =>
                {
                    inner.Item().Text(label).FontSize(7).FontColor(Colors.Grey.Darken2);
                    inner.Item().Text(value).FontSize(11).Bold();
                });

            row.RelativeItem().Element(c => SummaryBox(c, "Total",     FormatCurrency(report.CurrencyCode, obl.TotalAmount),   Colors.Grey.Lighten3));
            row.ConstantItem(6);
            row.RelativeItem().Element(c => SummaryBox(c, "Pagado",    FormatCurrency(report.CurrencyCode, obl.TotalPaid),     Colors.Green.Lighten4));
            row.ConstantItem(6);
            row.RelativeItem().Element(c => SummaryBox(c, "Pendiente", FormatCurrency(report.CurrencyCode, obl.TotalPending),  Colors.Orange.Lighten4));
            row.ConstantItem(6);
            row.RelativeItem().Element(c => SummaryBox(c, "Vencidas",  $"{obl.OverdueCount}",                                  Colors.Red.Lighten4));
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
                PdfTableHeaderCell(header, "Estado");
                PdfTableHeaderCell(header, "Título");
                PdfTableHeaderCell(header, "Total",    true);
                PdfTableHeaderCell(header, "Pagado",   true);
                PdfTableHeaderCell(header, "Restante", true);
                PdfTableHeaderCell(header, "Vence");
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