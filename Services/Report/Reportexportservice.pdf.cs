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

            foreach (var section in report.Sections)
                ComposeExpenseSection(col, section, report.CurrencyCode);

            if (report.CategoryAnalysis is { Count: > 0 })
                ComposeCategoryAnalysisSection(col, report);

            if (report.ObligationSummary is not null)
                ComposeObligationsSummarySection(col, report);
        });
    }

    private static void ComposeExpenseSection(
        ColumnDescriptor col,
        dynamic section,
        string currencyCode)
    {
        col.Item().PaddingTop(8).Text($"{section.MonthLabel}")
            .FontSize(12).Bold().FontColor(Colors.Blue.Darken2);

        col.Item().Text($"Subtotal: {FormatCurrency(currencyCode, section.SectionTotal)}  ·  {section.SectionCount} gastos")
            .FontSize(8).FontColor(Colors.Grey.Darken1);

        col.Item().PaddingTop(4).Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(65);
                cols.RelativeColumn(3);
                cols.RelativeColumn(2);
                cols.RelativeColumn(2);
                cols.ConstantColumn(70);
            });

            table.Header(header =>
            {
                PdfTableHeaderCell(header, "Fecha");
                PdfTableHeaderCell(header, "Título");
                PdfTableHeaderCell(header, "Categoría");
                PdfTableHeaderCell(header, "Método de Pago");
                PdfTableHeaderCell(header, "Monto", true);
            });

            foreach (var exp in section.Expenses)
            {
                PdfTableCell(table, exp.ExpenseDate.ToString("yyyy-MM-dd"));
                PdfTableCell(table, exp.Title);
                PdfTableCell(table, exp.CategoryName);
                PdfTableCell(table, exp.PaymentMethodName);
                PdfTableCell(table, FormatCurrency(currencyCode, exp.ConvertedAmount), true);
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

    // ════════════════════════════════════════════════════════
    //  PAYMENT METHOD REPORT — PDF
    // ════════════════════════════════════════════════════════

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

    private static void ComposePaymentMethodHeader(IContainer container, PaymentMethodReportResponse report)
    {
        container.Column(col =>
        {
            col.Item().Text("Reporte de Métodos de Pago")
                .FontSize(16).Bold().FontColor(Colors.Blue.Darken3);

            col.Item().Row(row =>
            {
                row.RelativeItem().Text($"Período: {FormatDateRange(report.DateFrom, report.DateTo)}").FontSize(9);
                row.RelativeItem().AlignRight()
                    .Text($"Generado: {report.GeneratedAt:yyyy-MM-dd HH:mm} UTC").FontSize(8);
            });

            col.Item().PaddingTop(5).Row(row =>
            {
                row.RelativeItem().Background(Colors.Blue.Lighten4).Padding(8).Column(c =>
                {
                    c.Item().Text("Total Gastado").FontSize(8).FontColor(Colors.Grey.Darken2);
                    c.Item().Text($"{report.GrandTotalSpent:N2}").FontSize(14).Bold();
                });
                row.ConstantItem(10);
                row.RelativeItem().Background(Colors.Grey.Lighten3).Padding(8).Column(c =>
                {
                    c.Item().Text("Transacciones").FontSize(8).FontColor(Colors.Grey.Darken2);
                    c.Item().Text($"{report.GrandTotalExpenseCount}").FontSize(14).Bold();
                });
                row.ConstantItem(10);
                row.RelativeItem().Background(Colors.Grey.Lighten3).Padding(8).Column(c =>
                {
                    c.Item().Text("Método Líder").FontSize(8).FontColor(Colors.Grey.Darken2);
                    c.Item().Text(GetTopPaymentMethodLabel(report)).FontSize(10).Bold();
                });
            });

            col.Item().PaddingTop(6).Row(row =>
            {
                row.RelativeItem().Background(Colors.Green.Lighten4).Padding(8).Column(c =>
                {
                    c.Item().Text("Total Ingresos").FontSize(8).FontColor(Colors.Grey.Darken2);
                    c.Item().Text($"{report.GrandTotalIncome:N2}").FontSize(12).Bold();
                });
                row.ConstantItem(10);
                row.RelativeItem().Background(Colors.Orange.Lighten4).Padding(8).Column(c =>
                {
                    c.Item().Text("Balance Neto").FontSize(8).FontColor(Colors.Grey.Darken2);
                    c.Item().Text($"{report.GrandNetFlow:N2}").FontSize(12).Bold();
                });
            });

            col.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        });
    }

    private static void ComposePaymentMethodContent(IContainer container, PaymentMethodReportResponse report)
    {
        container.Column(col =>
        {
            if (report.PaymentMethods.Count == 0)
            {
                ComposePdfEmptyState(col, "No se encontraron gastos para métodos de pago en el período seleccionado.");
                return;
            }

            ComposePaymentMethodSummaryTable(col, report);

            if (report.PaymentMethods.Any(pm => pm.Projects.Count > 0))
                ComposePaymentMethodByProjectSection(col, report);

            if (report.MonthlyTrend.Count > 0)
                ComposeMonthlyTrendSection(col, report);
        });
    }

    private static void ComposePaymentMethodSummaryTable(ColumnDescriptor col, PaymentMethodReportResponse report)
    {
        col.Item().Text("Resumen por Método de Pago")
            .FontSize(12).Bold().FontColor(Colors.Blue.Darken2);

        col.Item().PaddingTop(4).Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn(3);
                cols.ConstantColumn(45);
                cols.ConstantColumn(40);
                cols.ConstantColumn(70);
                cols.ConstantColumn(70);
                cols.ConstantColumn(70);
                cols.ConstantColumn(40);
                cols.ConstantColumn(40);
            });

            table.Header(header =>
            {
                PdfTableHeaderCell(header, "Método");
                PdfTableHeaderCell(header, "Tipo");
                PdfTableHeaderCell(header, "Moneda");
                PdfTableHeaderCell(header, "Total",    true);
                PdfTableHeaderCell(header, "Ingresos", true);
                PdfTableHeaderCell(header, "Balance",  true);
                PdfTableHeaderCell(header, "Gastos",   true);
                PdfTableHeaderCell(header, "Ingr.",    true);
            });

            foreach (var pm in report.PaymentMethods)
            {
                PdfTableCell(table, pm.Name);
                PdfTableCell(table, pm.Type);
                PdfTableCell(table, pm.Currency);
                PdfTableCell(table, FormatCurrency(pm.Currency, pm.TotalSpent),  true);
                PdfTableCell(table, FormatCurrency(pm.Currency, pm.TotalIncome), true);
                PdfTableCell(table, FormatCurrency(pm.Currency, pm.NetFlow),     true);
                PdfTableCell(table, $"{pm.ExpenseCount}", true);
                PdfTableCell(table, $"{pm.IncomeCount}",  true);
            }
        });
    }

    private static void ComposePaymentMethodByProjectSection(ColumnDescriptor col, PaymentMethodReportResponse report)
    {
        col.Item().PaddingTop(12).Text("Desglose por Proyecto")
            .FontSize(12).Bold().FontColor(Colors.Blue.Darken2);

        foreach (var pm in report.PaymentMethods.Where(pm => pm.Projects.Count > 0))
        {
            col.Item().PaddingTop(6).Text($"{pm.Name} ({pm.Type})").FontSize(10).Bold();

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
                    PdfTableHeaderCell(header, "Proyecto");
                    PdfTableHeaderCell(header, "Moneda");
                    PdfTableHeaderCell(header, "Total",  true);
                    PdfTableHeaderCell(header, "Gastos", true);
                    PdfTableHeaderCell(header, "%",      true);
                });

                foreach (var proj in pm.Projects)
                {
                    PdfTableCell(table, proj.ProjectName);
                    PdfTableCell(table, proj.ProjectCurrency);
                    PdfTableCell(table, FormatCurrency(proj.ProjectCurrency, proj.TotalSpent), true);
                    PdfTableCell(table, $"{proj.ExpenseCount}",        true);
                    PdfTableCell(table, $"{proj.Percentage:N1}%",      true);
                }
            });
        }
    }

    private static void ComposeMonthlyTrendSection(ColumnDescriptor col, PaymentMethodReportResponse report)
    {
        col.Item().PaddingTop(12).Text("Tendencia Mensual")
            .FontSize(12).Bold().FontColor(Colors.Blue.Darken2);

        col.Item().PaddingTop(4).Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn(3);
                cols.ConstantColumn(70);
                cols.ConstantColumn(50);
                cols.ConstantColumn(70);
                cols.ConstantColumn(50);
                cols.ConstantColumn(70);
            });

            table.Header(header =>
            {
                PdfTableHeaderCell(header, "Mes");
                PdfTableHeaderCell(header, "Total",    true);
                PdfTableHeaderCell(header, "Gastos",   true);
                PdfTableHeaderCell(header, "Ingresos", true);
                PdfTableHeaderCell(header, "Ingr.",    true);
                PdfTableHeaderCell(header, "Balance",  true);
            });

            foreach (var m in report.MonthlyTrend)
            {
                PdfTableCell(table, m.MonthLabel);
                PdfTableCell(table, $"{m.TotalSpent:N2}",  true);
                PdfTableCell(table, $"{m.ExpenseCount}",   true);
                PdfTableCell(table, $"{m.TotalIncome:N2}", true);
                PdfTableCell(table, $"{m.IncomeCount}",    true);
                PdfTableCell(table, $"{m.NetBalance:N2}",  true);
            }
        });
    }
}