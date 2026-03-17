using ClosedXML.Excel;
using ProjectLedger.API.DTOs.Report;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ProjectLedger.API.Services.Report;

/// <summary>
/// Constantes, utilidades de formato y helpers de estilo compartidos
/// entre las partes Excel y PDF del servicio de exportación.
/// </summary>
public partial class ReportExportService : IReportExportService
{
    // ── Constantes de formato Excel ──────────────────────────────────────────

    private const string ExcelFontName         = "Arial";
    private const double ExcelFontSize         = 10;
    private const string ExcelCurrencyFormat   = "#,##0.00;(#,##0.00);-";
    private const string ExcelExchangeRateFormat = "#,##0.0000;(#,##0.0000);-";
    private const string ExcelPercentFormat    = "0.0\"%\";(0.0\"%\");-";

    // ── Helpers Excel ────────────────────────────────────────────────────────

    private static byte[] WorkbookToBytes(XLWorkbook workbook)
    {
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static void ApplyWorkbookDefaults(XLWorkbook workbook, string title, string subject)
    {
        workbook.Style.Font.FontName = ExcelFontName;
        workbook.Style.Font.FontSize = ExcelFontSize;

        workbook.Properties.Title   = title;
        workbook.Properties.Subject = subject;
        workbook.Properties.Author  = "Project Ledger";
        workbook.Properties.Company = "Project Ledger";
    }

    /// <summary>
    /// Aplica auto-ajuste de columnas, congelado de filas, auto-filtro opcional
    /// y ajuste de texto en columnas indicadas.
    /// </summary>
    private static void FinalizeSheetLayout(
        IXLWorksheet ws,
        int          headerRow,
        int          lastRow,
        int          lastColumn,
        int          freezeRows,
        bool         enableAutoFilter = true,
        int          maxColumnWidth   = 40,
        params int[] wrapColumns)
    {
        if (freezeRows > 0)
            ws.SheetView.FreezeRows(freezeRows);

        if (enableAutoFilter && lastRow >= headerRow && lastColumn > 0)
            ws.Range(headerRow, 1, lastRow, lastColumn).SetAutoFilter();

        foreach (var col in wrapColumns)
            ws.Column(col).Style.Alignment.WrapText = true;

        ws.Columns().AdjustToContents();

        foreach (var column in ws.ColumnsUsed())
        {
            if (column.Width > maxColumnWidth)
                column.Width = maxColumnWidth;
        }
    }

    /// <summary>Aplica estilo de encabezado de tabla (fondo azul oscuro, texto blanco, negrita, centrado).</summary>
    private static void StyleTableHeader(IXLRange range)
    {
        range.Style.Font.Bold                    = true;
        range.Style.Fill.BackgroundColor         = XLColor.DarkBlue;
        range.Style.Font.FontColor               = XLColor.White;
        range.Style.Alignment.Horizontal         = XLAlignmentHorizontalValues.Center;
    }

    /// <summary>Aplica estilo de etiqueta lateral (negrita, color azul oscuro).</summary>
    private static void StyleHeaderRange(IXLRange range)
    {
        range.Style.Font.Bold      = true;
        range.Style.Font.FontColor = XLColor.DarkBlue;
    }

    // ── Helpers PDF ──────────────────────────────────────────────────────────

    private static void PdfTableHeaderCell(TableCellDescriptor header, string text, bool alignRight = false)
    {
        var cell = header.Cell()
            .Background(Colors.Blue.Darken3)
            .PaddingVertical(4).PaddingHorizontal(4);

        if (alignRight)
            cell.AlignRight().Text(text).FontSize(8).FontColor(Colors.White).Bold();
        else
            cell.Text(text).FontSize(8).FontColor(Colors.White).Bold();
    }

    private static void PdfTableCell(TableDescriptor table, string text, bool alignRight = false)
    {
        var cell = table.Cell()
            .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(3).PaddingHorizontal(4);

        if (alignRight)
            cell.AlignRight().Text(text).FontSize(8);
        else
            cell.Text(text).FontSize(8);
    }

    private static void ComposePdfEmptyState(ColumnDescriptor col, string message)
    {
        col.Item().PaddingTop(10).Background(Colors.Grey.Lighten3).Padding(10).Column(inner =>
        {
            inner.Item().Text("Sin datos para mostrar")
                .FontSize(11).Bold().FontColor(Colors.Grey.Darken2);
            inner.Item().PaddingTop(2).Text(message)
                .FontSize(9).FontColor(Colors.Grey.Darken1);
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.AlignCenter().Text(text =>
        {
            text.Span("Project Ledger — Pagina ").FontSize(8).FontColor(Colors.Grey.Medium);
            text.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
            text.Span(" de ").FontSize(8).FontColor(Colors.Grey.Medium);
            text.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
        });
    }

    // ── Helpers de dominio ───────────────────────────────────────────────────

    private static string FormatCurrency(string currencyCode, decimal amount)
        => string.IsNullOrWhiteSpace(currencyCode)
            ? $"{amount:N2}"
            : $"{currencyCode} {amount:N2}";

    private static string FormatDateRange(DateOnly? from, DateOnly? to) => (from, to) switch
    {
        ({ } f, { } t) => $"{f:yyyy-MM-dd} — {t:yyyy-MM-dd}",
        ({ } f, null) => $"Desde {f:yyyy-MM-dd}",
        (null, { } t) => $"Hasta {t:yyyy-MM-dd}",
        _             => "Todo el historial"
    };

    private static string FormatStatus(string status) => status switch
    {
        "open"           => "Abierta",
        "partially_paid" => "Parcial",
        "paid"           => "Pagada",
        "overdue"        => "Vencida",
        _                => status
    };

    private static string GetPeakExpenseMonthLabel(DetailedExpenseReportResponse report)
    {
        var peak = report.Sections
            .OrderByDescending(s => s.SectionTotal)
            .FirstOrDefault();

        return peak is null
            ? "—"
            : $"{peak.MonthLabel} ({peak.SectionTotal:N2})";
    }

    private static string GetTopExpenseCategoryLabel(DetailedExpenseReportResponse report)
    {
        if (report.CategoryAnalysis is { Count: > 0 })
        {
            var top = report.CategoryAnalysis
                .OrderByDescending(c => c.SpentAmount)
                .First();
            return $"{top.CategoryName} ({top.SpentAmount:N2})";
        }

        var topFromRows = report.Sections
            .SelectMany(s => s.Expenses)
            .GroupBy(e => e.CategoryName)
            .Select(g => new { Category = g.Key, Total = g.Sum(e => e.ConvertedAmount) })
            .OrderByDescending(x => x.Total)
            .FirstOrDefault();

        return topFromRows is null
            ? "—"
            : $"{topFromRows.Category} ({topFromRows.Total:N2})";
    }

    private static string GetTopPaymentMethodLabel(PaymentMethodReportResponse report)
    {
        var top = report.PaymentMethods
            .OrderByDescending(pm => pm.TotalSpent)
            .FirstOrDefault();

        return top is null
            ? "—"
            : $"{top.Name} ({top.TotalSpent:N2})";
    }

    private static string GetPeakTrendMonthLabel(PaymentMethodReportResponse report)
    {
        var peak = report.MonthlyTrend
            .OrderByDescending(m => m.TotalSpent)
            .FirstOrDefault();

        return peak is null
            ? "—"
            : $"{peak.MonthLabel} ({peak.TotalSpent:N2})";
    }
}