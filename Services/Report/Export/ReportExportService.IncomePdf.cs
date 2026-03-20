using ProjectLedger.API.DTOs.Report;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ProjectLedger.API.Services.Report;

public partial class ReportExportService
{
    // ════════════════════════════════════════════════════════
    //  INCOME REPORT — PDF
    // ════════════════════════════════════════════════════════

    public byte[] GenerateIncomeReportPdf(DetailedIncomeReportResponse report)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Element(h => ComposeIncomeReportHeader(h, report));
                page.Content().Element(c => ComposeIncomeReportContent(c, report));
                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    private static void ComposeIncomeReportHeader(IContainer container, DetailedIncomeReportResponse report)
    {
        container.Column(col =>
        {
            col.Item().Text($"Reporte de Ingresos — {report.ProjectName}")
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
                row.RelativeItem().Background(Colors.Green.Lighten4).Padding(8).Column(c =>
                {
                    c.Item().Text("Total Ingresos").FontSize(8).FontColor(Colors.Grey.Darken2);
                    c.Item().Text($"{report.CurrencyCode} {report.TotalIncome:N2}").FontSize(14).Bold();
                });
                row.ConstantItem(10);
                row.RelativeItem().Background(Colors.Grey.Lighten3).Padding(8).Column(c =>
                {
                    c.Item().Text("Transacciones").FontSize(8).FontColor(Colors.Grey.Darken2);
                    c.Item().Text($"{report.TotalIncomeCount}").FontSize(14).Bold();
                });
                row.ConstantItem(10);
                row.RelativeItem().Background(Colors.Grey.Lighten3).Padding(8).Column(c =>
                {
                    c.Item().Text("Promedio Mensual").FontSize(8).FontColor(Colors.Grey.Darken2);
                    c.Item().Text(FormatCurrency(report.CurrencyCode, report.AverageMonthlyIncome)).FontSize(10).Bold();
                });
            });

            if (report.PeakMonth is not null)
            {
                col.Item().PaddingTop(6)
                    .Text($"Mes pico: {report.PeakMonth.MonthLabel} ({FormatCurrency(report.CurrencyCode, report.PeakMonth.Total)})")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
            }

            col.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        });
    }

    private static void ComposeIncomeReportContent(IContainer container, DetailedIncomeReportResponse report)
    {
        container.Column(col =>
        {
            if (report.Sections.Count == 0)
            {
                ComposePdfEmptyState(col, "No se encontraron ingresos para el período seleccionado.");
                return;
            }

            foreach (var section in report.Sections)
                ComposeIncomeSection(col, section, report.CurrencyCode);

            if (report.CategoryAnalysis is { Count: > 0 })
                ComposeIncomeCategoryAnalysisSection(col, report);

            if (report.PaymentMethodAnalysis is { Count: > 0 })
                ComposeIncomePaymentMethodAnalysisSection(col, report);
        });
    }

    private static void ComposeIncomeSection(
        ColumnDescriptor col,
        MonthlyIncomeSection section,
        string currencyCode)
    {
        col.Item().PaddingTop(8).Text(section.MonthLabel)
            .FontSize(12).Bold().FontColor(Colors.Blue.Darken2);

        col.Item().Text($"Subtotal: {FormatCurrency(currencyCode, section.SectionTotal)}  ·  {section.SectionCount} ingresos")
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

            foreach (var inc in section.Incomes)
            {
                PdfTableCell(table, inc.IncomeDate.ToString("yyyy-MM-dd"));
                PdfTableCell(table, inc.Title);
                PdfTableCell(table, inc.CategoryName);
                PdfTableCell(table, inc.PaymentMethodName);
                PdfTableCell(table, FormatCurrency(currencyCode, inc.ConvertedAmount), true);
            }
        });
    }

    private static void ComposeIncomeCategoryAnalysisSection(
        ColumnDescriptor col,
        DetailedIncomeReportResponse report)
    {
        col.Item().PaddingTop(15).Text("Análisis por Categoría")
            .FontSize(14).Bold().FontColor(Colors.Blue.Darken3);

        col.Item().PaddingTop(4).Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn(3);
                cols.ConstantColumn(70);
                cols.ConstantColumn(50);
                cols.ConstantColumn(50);
                cols.ConstantColumn(70);
            });

            table.Header(header =>
            {
                PdfTableHeaderCell(header, "Categoría");
                PdfTableHeaderCell(header, "Total", true);
                PdfTableHeaderCell(header, "Cantidad", true);
                PdfTableHeaderCell(header, "%", true);
                PdfTableHeaderCell(header, "Promedio", true);
            });

            foreach (var cat in report.CategoryAnalysis!)
            {
                PdfTableCell(table, cat.CategoryName);
                PdfTableCell(table, FormatCurrency(report.CurrencyCode, cat.TotalAmount), true);
                PdfTableCell(table, $"{cat.IncomeCount}", true);
                PdfTableCell(table, $"{cat.Percentage:N1}%", true);
                PdfTableCell(table, FormatCurrency(report.CurrencyCode, cat.AverageAmount), true);
            }
        });
    }

    private static void ComposeIncomePaymentMethodAnalysisSection(
        ColumnDescriptor col,
        DetailedIncomeReportResponse report)
    {
        col.Item().PaddingTop(15).Text("Análisis por Método de Pago")
            .FontSize(14).Bold().FontColor(Colors.Blue.Darken3);

        col.Item().PaddingTop(4).Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn(3);
                cols.ConstantColumn(50);
                cols.ConstantColumn(70);
                cols.ConstantColumn(50);
                cols.ConstantColumn(50);
                cols.ConstantColumn(70);
            });

            table.Header(header =>
            {
                PdfTableHeaderCell(header, "Método");
                PdfTableHeaderCell(header, "Tipo");
                PdfTableHeaderCell(header, "Total", true);
                PdfTableHeaderCell(header, "Cantidad", true);
                PdfTableHeaderCell(header, "%", true);
                PdfTableHeaderCell(header, "Promedio", true);
            });

            foreach (var pm in report.PaymentMethodAnalysis!)
            {
                PdfTableCell(table, pm.PaymentMethodName);
                PdfTableCell(table, pm.Type);
                PdfTableCell(table, FormatCurrency(report.CurrencyCode, pm.TotalAmount), true);
                PdfTableCell(table, $"{pm.IncomeCount}", true);
                PdfTableCell(table, $"{pm.Percentage:N1}%", true);
                PdfTableCell(table, FormatCurrency(report.CurrencyCode, pm.AverageAmount), true);
            }
        });
    }
}
