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
    private static void ComposeWorkspaceReportHeader(IContainer container, WorkspaceReportResponse report)
    {
        container.Column(col =>
        {
            col.Item().Text($"Reporte de Workspace — {report.WorkspaceName}")
                .FontSize(16).Bold().FontColor(Colors.Blue.Darken3);

            col.Item().Row(row =>
            {
                row.RelativeItem().Text($"Período: {FormatDateRange(report.DateFrom, report.DateTo)}").FontSize(9);
                row.RelativeItem().AlignRight()
                    .Text($"Generado: {report.GeneratedAt:yyyy-MM-dd HH:mm} UTC").FontSize(8);
            });

            col.Item().PaddingTop(5).Row(row =>
            {
                if (report.ConsolidatedTotals is not null)
                {
                    row.RelativeItem().Background(Colors.Blue.Lighten4).Padding(8).Column(c =>
                    {
                        c.Item().Text("Total Gastado").FontSize(8).FontColor(Colors.Grey.Darken2);
                        c.Item().Text(FormatCurrency(report.ReferenceCurrency ?? "", report.ConsolidatedTotals.TotalSpent))
                            .FontSize(14).Bold();
                    });
                    row.ConstantItem(10);
                    row.RelativeItem().Background(Colors.Green.Lighten4).Padding(8).Column(c =>
                    {
                        c.Item().Text("Total Ingresos").FontSize(8).FontColor(Colors.Grey.Darken2);
                        c.Item().Text(FormatCurrency(report.ReferenceCurrency ?? "", report.ConsolidatedTotals.TotalIncome))
                            .FontSize(14).Bold();
                    });
                    row.ConstantItem(10);
                    row.RelativeItem().Background(Colors.Orange.Lighten4).Padding(8).Column(c =>
                    {
                        c.Item().Text("Balance Neto").FontSize(8).FontColor(Colors.Grey.Darken2);
                        c.Item().Text(FormatCurrency(report.ReferenceCurrency ?? "", report.ConsolidatedTotals.NetBalance))
                            .FontSize(14).Bold();
                    });
                }
                else
                {
                    row.RelativeItem().Background(Colors.Grey.Lighten3).Padding(8).Column(c =>
                    {
                        c.Item().Text("Proyectos").FontSize(8).FontColor(Colors.Grey.Darken2);
                        c.Item().Text($"{report.ProjectCount}").FontSize(14).Bold();
                    });
                    row.ConstantItem(10);
                    row.RelativeItem().Background(Colors.Orange.Lighten4).Padding(8).Column(c =>
                    {
                        c.Item().Text("Monedas diferentes").FontSize(8).FontColor(Colors.Grey.Darken2);
                        c.Item().Text("No consolidado").FontSize(10).Bold();
                    });
                }

                row.ConstantItem(10);
                row.RelativeItem().Background(Colors.Grey.Lighten3).Padding(8).Column(c =>
                {
                    c.Item().Text("Proyectos").FontSize(8).FontColor(Colors.Grey.Darken2);
                    c.Item().Text($"{report.ProjectCount}").FontSize(14).Bold();
                });
            });

            col.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        });
    }

    /// <summary>Composes the main body sections of the workspace report.</summary>
    private static void ComposeWorkspaceReportContent(IContainer container, WorkspaceReportResponse report)
    {
        container.Column(col =>
        {
            if (report.Projects.Count == 0)
            {
                ComposePdfEmptyState(col, "No hay proyectos en este workspace.");
                return;
            }

            // ── Projects table ───────────────────────────────
            col.Item().Text("Resumen por Proyecto")
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
                    PdfTableHeaderCell(header, "Proyecto");
                    PdfTableHeaderCell(header, "Moneda");
                    PdfTableHeaderCell(header, "Gastado", true);
                    PdfTableHeaderCell(header, "Ingresos", true);
                    PdfTableHeaderCell(header, "Balance", true);
                    PdfTableHeaderCell(header, "# Gast.", true);
                    PdfTableHeaderCell(header, "# Ingr.", true);
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
                col.Item().PaddingTop(15).Text("Categorías (Cross-Proyecto)")
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
                        PdfTableHeaderCell(header, "Categoría");
                        PdfTableHeaderCell(header, "Total", true);
                        PdfTableHeaderCell(header, "%", true);
                        PdfTableHeaderCell(header, "Proyectos", true);
                        PdfTableHeaderCell(header, "Gastos", true);
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
                col.Item().PaddingTop(15).Text("Tendencia Mensual")
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
                        PdfTableHeaderCell(header, "Mes");
                        PdfTableHeaderCell(header, "Gastado", true);
                        PdfTableHeaderCell(header, "Ingresos", true);
                        PdfTableHeaderCell(header, "Balance", true);
                        PdfTableHeaderCell(header, "# Gast.", true);
                        PdfTableHeaderCell(header, "# Ingr.", true);
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
