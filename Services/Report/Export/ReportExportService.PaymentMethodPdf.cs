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

            col.Item().PaddingTop(4)
                .Text($"{report.PaymentMethods.Count} método(s) de pago  ·  " +
                      $"{report.PaymentMethods.Count(pm => !pm.IsInactive)} activo(s)")
                .FontSize(9).FontColor(Colors.Grey.Darken1);

            col.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        });
    }

    /// <summary>Composes the main body sections of the payment method report.</summary>
    private static void ComposePaymentMethodContent(IContainer container, PaymentMethodReportResponse report)
    {
        container.Column(col =>
        {
            if (report.PaymentMethods.Count == 0)
            {
                ComposePdfEmptyState(col, "No se encontraron movimientos para métodos de pago en el período seleccionado.");
                return;
            }

            // Una sección por cada método de pago
            foreach (var pm in report.PaymentMethods)
                ComposePaymentMethodSection(col, pm);

            if (report.MonthlyTrend.Count > 0)
                ComposePaymentMethodMonthlyTrendSection(col, report);
        });
    }

    /// <summary>Composes a detailed summary section for a specific payment method.</summary>
    private static void ComposePaymentMethodSection(ColumnDescriptor col, PaymentMethodReportRow pm)
    {
        // ── Encabezado del método ──
        col.Item().PaddingTop(10).Text($"{pm.Name}")
            .FontSize(13).Bold().FontColor(Colors.Blue.Darken2);

        col.Item().Text($"{pm.Type}  ·  {pm.Currency}" +
                        (pm.BankName is not null ? $"  ·  {pm.BankName}" : "") +
                        (pm.OwnerPartnerName is not null ? $"  ·  Dueño: {pm.OwnerPartnerName}" : ""))
            .FontSize(8).FontColor(Colors.Grey.Darken1);

        // ── Métricas del método ──
        col.Item().PaddingTop(4).Row(row =>
        {
            row.RelativeItem().Background(Colors.Blue.Lighten4).Padding(6).Column(c =>
            {
                c.Item().Text("Total Gastado").FontSize(7).FontColor(Colors.Grey.Darken2);
                c.Item().Text(FormatCurrency(pm.Currency, pm.TotalSpent)).FontSize(11).Bold();
            });
            row.ConstantItem(6);
            row.RelativeItem().Background(Colors.Green.Lighten4).Padding(6).Column(c =>
            {
                c.Item().Text("Total Ingresos").FontSize(7).FontColor(Colors.Grey.Darken2);
                c.Item().Text(FormatCurrency(pm.Currency, pm.TotalIncome)).FontSize(11).Bold();
            });
            row.ConstantItem(6);
            row.RelativeItem().Background(Colors.Orange.Lighten4).Padding(6).Column(c =>
            {
                c.Item().Text("Balance Neto").FontSize(7).FontColor(Colors.Grey.Darken2);
                c.Item().Text(FormatCurrency(pm.Currency, pm.NetFlow)).FontSize(11).Bold();
            });
            row.ConstantItem(6);
            row.RelativeItem().Background(Colors.Grey.Lighten3).Padding(6).Column(c =>
            {
                c.Item().Text("Transacciones").FontSize(7).FontColor(Colors.Grey.Darken2);
                c.Item().Text($"{pm.ExpenseCount} gastos · {pm.IncomeCount} ingresos").FontSize(9).Bold();
            });
        });

        // ── Top categories ──
        if (pm.TopCategories.Count > 0)
        {
            col.Item().PaddingTop(6).Text("Top Categorías").FontSize(9).Bold();
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
                    PdfTableHeaderCell(header, "Categoría");
                    PdfTableHeaderCell(header, "Total", true);
                    PdfTableHeaderCell(header, "Gastos", true);
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

        // ── Desglose por proyecto ──
        if (pm.Projects.Count > 0)
        {
            col.Item().PaddingTop(6).Text("Proyectos").FontSize(9).Bold();
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
                    PdfTableHeaderCell(header, "Total", true);
                    PdfTableHeaderCell(header, "Gastos", true);
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

        // ── Gastos ──
        if (pm.Expenses.Count > 0)
        {
            col.Item().PaddingTop(6).Text($"Gastos ({pm.ExpensesShown} de {pm.TotalExpensesInPeriod})")
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
                    PdfTableHeaderCell(header, "Fecha");
                    PdfTableHeaderCell(header, "Título");
                    PdfTableHeaderCell(header, "Proyecto");
                    PdfTableHeaderCell(header, "Categoría");
                    PdfTableHeaderCell(header, "Monto", true);
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

        // ── Ingresos ──
        if (pm.Incomes.Count > 0)
        {
            col.Item().PaddingTop(6).Text($"Ingresos ({pm.IncomesShown} de {pm.TotalIncomesInPeriod})")
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
                    PdfTableHeaderCell(header, "Fecha");
                    PdfTableHeaderCell(header, "Título");
                    PdfTableHeaderCell(header, "Proyecto");
                    PdfTableHeaderCell(header, "Categoría");
                    PdfTableHeaderCell(header, "Monto", true);
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
    private static void ComposePaymentMethodMonthlyTrendSection(ColumnDescriptor col, PaymentMethodReportResponse report)
    {
        col.Item().PaddingTop(12).Text("Tendencia Mensual por Método")
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
                PdfTableHeaderCell(header, "Mes");
                PdfTableHeaderCell(header, "Método");
                PdfTableHeaderCell(header, "Mon.");
                PdfTableHeaderCell(header, "Gastado", true);
                PdfTableHeaderCell(header, "# G.", true);
                PdfTableHeaderCell(header, "Ingresos", true);
                PdfTableHeaderCell(header, "# I.", true);
                PdfTableHeaderCell(header, "Balance", true);
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
