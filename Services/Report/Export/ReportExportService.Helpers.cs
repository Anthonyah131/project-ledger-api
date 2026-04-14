using ClosedXML.Excel;
using ProjectLedger.API.DTOs.Report;
using ProjectLedger.API.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ProjectLedger.API.Services.Report;

/// <summary>
/// Constants, formatting utilities, and shared style helpers 
/// between the Excel and PDF parts of the export service.
/// </summary>
public partial class ReportExportService : IReportExportService
{
    // ── Excel Formatting Constants ──────────────────────────────────────────

    private const string ExcelFontName         = "Arial";
    private const double ExcelFontSize         = 10;
    private const string ExcelCurrencyFormat   = "#,##0.00;(#,##0.00);-";
    private const string ExcelExchangeRateFormat = "#,##0.0000;(#,##0.0000);-";
    private const string ExcelPercentFormat    = "0.0\"%\";(0.0\"%\");-";

    // ── Excel Helpers ────────────────────────────────────────────────────────

    /// <summary>Converts an XLWorkbook to a byte array for file downloads.</summary>
    private static byte[] WorkbookToBytes(XLWorkbook workbook)
    {
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    /// <summary>Applies default fonts, sizes, and metadata to an Excel workbook.</summary>
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
    /// Applies column auto-fit, row freezing, optional auto-filtering,
    /// and text wrapping to specified columns in a worksheet.
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

    /// <summary>Applies table header styling (dark blue background, white bold centered text).</summary>
    private static void StyleTableHeader(IXLRange range)
    {
        range.Style.Font.Bold                    = true;
        range.Style.Fill.BackgroundColor         = XLColor.DarkBlue;
        range.Style.Font.FontColor               = XLColor.White;
        range.Style.Alignment.Horizontal         = XLAlignmentHorizontalValues.Center;
    }

    /// <summary>Applies side label styling (bold dark blue text).</summary>
    private static void StyleHeaderRange(IXLRange range)
    {
        range.Style.Font.Bold      = true;
        range.Style.Font.FontColor = XLColor.DarkBlue;
    }

    // ── PDF Helpers ──────────────────────────────────────────────────────────

    /// <summary>Styles and renders a PDF table header cell.</summary>
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

    /// <summary>Styles and renders a PDF standard table data cell.</summary>
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

    /// <summary>Renders a placeholder section when no data is available for a report segment.</summary>
    private void ComposePdfEmptyState(ColumnDescriptor col, string message)
    {
        col.Item().PaddingTop(10).Background(Colors.Grey.Lighten3).Padding(10).Column(inner =>
        {
            inner.Item().Text(_localizer["RptCommon_NoData"].Value)
                .FontSize(11).Bold().FontColor(Colors.Grey.Darken2);
            inner.Item().PaddingTop(2).Text(message)
                .FontSize(9).FontColor(Colors.Grey.Darken1);
        });
    }

    /// <summary>Composes the standard page footer with numbering.</summary>
    private void ComposeFooter(IContainer container)
    {
        container.AlignCenter().Text(text =>
        {
            text.Span(_localizer["RptFmt_FooterPagePrefix"].Value).FontSize(8).FontColor(Colors.Grey.Medium);
            text.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
            text.Span(_localizer["RptFmt_FooterPageMiddle"].Value).FontSize(8).FontColor(Colors.Grey.Medium);
            text.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
        });
    }

    // ── Domain Helpers ───────────────────────────────────────────────────────

    /// <summary>Formats a decimal amount with its respective ISO currency code.</summary>
    private static string FormatCurrency(string currencyCode, decimal amount)
        => string.IsNullOrWhiteSpace(currencyCode)
            ? $"{amount:N2}"
            : $"{currencyCode} {amount:N2}";

    /// <summary>Generates a human-readable date range label.</summary>
    private string FormatDateRange(DateOnly? from, DateOnly? to) => (from, to) switch
    {
        ({ } f, { } t) => $"{f:yyyy-MM-dd} — {t:yyyy-MM-dd}",
        ({ } f, null) => _localizer["RptFmt_DateFrom", $"{f:yyyy-MM-dd}"].Value,
        (null, { } t) => _localizer["RptFmt_DateTo", $"{t:yyyy-MM-dd}"].Value,
        _             => _localizer["RptFmt_AllHistory"].Value
    };

    /// <summary>Translates internal status codes to display-friendly labels.</summary>
    private string FormatStatus(string status) => status switch
    {
        "open"           => _localizer["RptStatus_Open"].Value,
        "partially_paid" => _localizer["RptStatus_PartiallyPaid"].Value,
        "paid"           => _localizer["RptStatus_Paid"].Value,
        "overdue"        => _localizer["RptStatus_Overdue"].Value,
        _                => status
    };

    /// <summary>Identifies and labels the month with the highest total expenditure.</summary>
    private static string GetPeakExpenseMonthLabel(DetailedExpenseReportResponse report)
    {
        var peak = report.Sections
            .OrderByDescending(s => s.SectionTotal)
            .FirstOrDefault();

        return peak is null
            ? "—"
            : $"{peak.MonthLabel} ({peak.SectionTotal:N2})";
    }

    /// <summary>Identifies and labels the category with the highest total expenditure.</summary>
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

}