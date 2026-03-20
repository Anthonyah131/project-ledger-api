using ClosedXML.Excel;
using ProjectLedger.API.DTOs.Report;

namespace ProjectLedger.API.Services.Report;

public partial class ReportExportService
{
    // ════════════════════════════════════════════════════════
    //  INCOME REPORT — EXCEL
    // ════════════════════════════════════════════════════════

    public byte[] GenerateIncomeReportExcel(DetailedIncomeReportResponse report)
    {
        using var workbook = new XLWorkbook();
        ApplyWorkbookDefaults(workbook, "Reporte de Ingresos", "Reporte financiero detallado de ingresos por proyecto.");

        AddIncomeSheet(workbook, report);

        if (report.CategoryAnalysis is { Count: > 0 })
            AddIncomeCategoryAnalysisSheet(workbook, report);

        if (report.PaymentMethodAnalysis is { Count: > 0 })
            AddIncomePaymentMethodAnalysisSheet(workbook, report);

        if (report.PartnerSummary is not null)
            AddPartnerIncomeSummarySheet(workbook, report);

        return WorkbookToBytes(workbook);
    }

    private static void AddIncomeSheet(XLWorkbook workbook, DetailedIncomeReportResponse report)
    {
        var ws = workbook.Worksheets.Add("Ingresos");

        // ── Summary block (columns A-B) ─────────────────────
        ws.Cell(1,  1).Value = "Proyecto";           ws.Cell(1,  2).Value = report.ProjectName;
        ws.Cell(2,  1).Value = "Moneda";             ws.Cell(2,  2).Value = report.CurrencyCode;
        ws.Cell(3,  1).Value = "Período";            ws.Cell(3,  2).Value = FormatDateRange(report.DateFrom, report.DateTo);
        ws.Cell(4,  1).Value = "Total Ingresos";     ws.Cell(4,  2).Value = report.TotalIncome;
        ws.Cell(4,  2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(5,  1).Value = "# Ingresos";         ws.Cell(5,  2).Value = report.TotalIncomeCount;
        ws.Cell(6,  1).Value = "Promedio Ingreso";   ws.Cell(6,  2).Value = report.AverageIncomeAmount;
        ws.Cell(6,  2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(7,  1).Value = "Promedio Mensual";   ws.Cell(7,  2).Value = report.AverageMonthlyIncome;
        ws.Cell(7,  2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(8,  1).Value = "Generado";           ws.Cell(8,  2).Value = report.GeneratedAt.ToString("yyyy-MM-dd HH:mm UTC");

        StyleHeaderRange(ws.Range(1, 1, 8, 1));

        // ── Insights block (columns D-E) ────────────────────
        if (report.PeakMonth is not null)
        {
            ws.Cell(1, 4).Value = "Mes Mayor Ingreso";
            ws.Cell(1, 5).Value = $"{report.PeakMonth.MonthLabel} ({report.PeakMonth.Total:N2})";
        }

        if (report.LargestIncome is not null)
        {
            ws.Cell(2, 4).Value = "Mayor Ingreso";
            ws.Cell(2, 5).Value = $"{report.LargestIncome.Title} ({report.LargestIncome.Amount:N2})";
            ws.Cell(3, 4).Value = "Fecha Mayor Ingreso";
            ws.Cell(3, 5).Value = report.LargestIncome.IncomeDate.ToString("yyyy-MM-dd");
            ws.Cell(4, 4).Value = "Categoría Mayor";
            ws.Cell(4, 5).Value = report.LargestIncome.CategoryName;
        }

        StyleHeaderRange(ws.Range(1, 4, 4, 4));

        // ── Detail table ────────────────────────────────────
        const int headerRow = 11;
        string[] headers =
        [
            "Fecha", "Título", "Categoría", "Método de Pago", "Tipo",
            "Monto Original", "Moneda Original", "Tipo Cambio", "Monto Convertido",
            "Monto Cuenta", "Moneda Cuenta",
            "Descripción", "Recibo", "Notas"
        ];

        for (var c = 0; c < headers.Length; c++)
            ws.Cell(headerRow, c + 1).Value = headers[c];

        StyleTableHeader(ws.Range(headerRow, 1, headerRow, headers.Length));

        var row = headerRow + 1;
        foreach (var section in report.Sections)
        {
            // Section header (enriched with stats)
            var sectionLabel = $"── {section.MonthLabel}  |  {section.SectionCount} ingresos  |  {section.PercentageOfTotal:N1}% del total  |  Prom: {section.AverageIncomeAmount:N2}";
            if (section.TopIncome is not null)
                sectionLabel += $"  |  Mayor: {section.TopIncome.Title} ({section.TopIncome.Amount:N2})";

            ws.Cell(row, 1).Value = sectionLabel;
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.Italic = true;
            ws.Range(row, 1, row, headers.Length).Merge()
              .Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
            row++;

            foreach (var inc in section.Incomes)
            {
                ws.Cell(row, 1).Value  = inc.IncomeDate.ToString("yyyy-MM-dd");
                ws.Cell(row, 2).Value  = inc.Title;
                ws.Cell(row, 3).Value  = inc.CategoryName;
                ws.Cell(row, 4).Value  = inc.PaymentMethodName;
                ws.Cell(row, 5).Value  = inc.PaymentMethodType;
                ws.Cell(row, 6).Value  = inc.OriginalAmount;
                ws.Cell(row, 6).Style.NumberFormat.Format = ExcelCurrencyFormat;
                ws.Cell(row, 7).Value  = inc.OriginalCurrency;
                ws.Cell(row, 8).Value  = inc.ExchangeRate;
                ws.Cell(row, 8).Style.NumberFormat.Format = ExcelExchangeRateFormat;
                ws.Cell(row, 9).Value  = inc.ConvertedAmount;
                ws.Cell(row, 9).Style.NumberFormat.Format = ExcelCurrencyFormat;

                if (inc.AccountAmount.HasValue)
                {
                    ws.Cell(row, 10).Value = inc.AccountAmount.Value;
                    ws.Cell(row, 10).Style.NumberFormat.Format = ExcelCurrencyFormat;
                }
                else
                {
                    ws.Cell(row, 10).Value = "—";
                }

                ws.Cell(row, 11).Value = inc.AccountCurrency ?? "—";
                ws.Cell(row, 12).Value = inc.Description ?? "";
                ws.Cell(row, 13).Value = inc.ReceiptNumber ?? "";
                ws.Cell(row, 14).Value = inc.Notes ?? "";
                row++;
            }

            // Section subtotal row
            ws.Cell(row, 8).Value = "Subtotal:";
            ws.Cell(row, 8).Style.Font.Bold = true;
            ws.Cell(row, 9).Value = section.SectionTotal;
            ws.Cell(row, 9).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 9).Style.Font.Bold = true;
            row += 2;
        }

        FinalizeSheetLayout(ws, headerRow, row - 1, headers.Length, headerRow, wrapColumns: [12, 14]);
    }

    private static void AddIncomeCategoryAnalysisSheet(XLWorkbook workbook, DetailedIncomeReportResponse report)
    {
        var ws = workbook.Worksheets.Add("Categorías");

        string[] headers = ["Categoría", "Total Ingresos", "# Ingresos", "% del Total", "Promedio"];

        for (var c = 0; c < headers.Length; c++)
            ws.Cell(1, c + 1).Value = headers[c];

        StyleTableHeader(ws.Range(1, 1, 1, headers.Length));

        var row = 2;
        foreach (var cat in report.CategoryAnalysis!)
        {
            ws.Cell(row, 1).Value = cat.CategoryName;
            ws.Cell(row, 2).Value = cat.TotalAmount;
            ws.Cell(row, 2).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 3).Value = cat.IncomeCount;
            ws.Cell(row, 4).Value = cat.Percentage;
            ws.Cell(row, 4).Style.NumberFormat.Format = ExcelPercentFormat;
            ws.Cell(row, 5).Value = cat.AverageAmount;
            ws.Cell(row, 5).Style.NumberFormat.Format = ExcelCurrencyFormat;
            row++;
        }

        // Totals row
        ws.Cell(row, 2).Value = report.CategoryAnalysis.Sum(c => c.TotalAmount);
        ws.Cell(row, 2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(row, 2).Style.Font.Bold = true;
        ws.Cell(row, 3).Value = report.CategoryAnalysis.Sum(c => c.IncomeCount);
        ws.Cell(row, 3).Style.Font.Bold = true;
        ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.LightGray;
        ws.Range(row, 1, row, headers.Length).Style.Border.TopBorder = XLBorderStyleValues.Thin;

        FinalizeSheetLayout(ws, 1, Math.Max(1, row), headers.Length, 1);
    }

    private static void AddIncomePaymentMethodAnalysisSheet(XLWorkbook workbook, DetailedIncomeReportResponse report)
    {
        var ws = workbook.Worksheets.Add("Por Método de Pago");

        string[] headers =
        [
            "Método de Pago", "Tipo", "Total Ingresos", "# Ingresos", "% del Total", "Promedio Ingreso"
        ];

        for (var c = 0; c < headers.Length; c++)
            ws.Cell(1, c + 1).Value = headers[c];

        StyleTableHeader(ws.Range(1, 1, 1, headers.Length));

        var row = 2;
        foreach (var pm in report.PaymentMethodAnalysis!)
        {
            ws.Cell(row, 1).Value = pm.PaymentMethodName;
            ws.Cell(row, 2).Value = pm.Type;
            ws.Cell(row, 3).Value = pm.TotalAmount;
            ws.Cell(row, 3).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 4).Value = pm.IncomeCount;
            ws.Cell(row, 5).Value = pm.Percentage;
            ws.Cell(row, 5).Style.NumberFormat.Format = ExcelPercentFormat;
            ws.Cell(row, 6).Value = pm.AverageAmount;
            ws.Cell(row, 6).Style.NumberFormat.Format = ExcelCurrencyFormat;
            row++;
        }

        // Totals row
        ws.Cell(row, 3).Value = report.PaymentMethodAnalysis.Sum(p => p.TotalAmount);
        ws.Cell(row, 3).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(row, 3).Style.Font.Bold = true;
        ws.Cell(row, 4).Value = report.PaymentMethodAnalysis.Sum(p => p.IncomeCount);
        ws.Cell(row, 4).Style.Font.Bold = true;
        ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.LightGray;
        ws.Range(row, 1, row, headers.Length).Style.Border.TopBorder = XLBorderStyleValues.Thin;

        FinalizeSheetLayout(ws, 1, Math.Max(1, row), headers.Length, 1, maxColumnWidth: 36, wrapColumns: [1]);
    }

    private static void AddPartnerIncomeSummarySheet(XLWorkbook workbook, DetailedIncomeReportResponse report)
    {
        var ws = workbook.Worksheets.Add("Resumen Partners");

        string[] headers = ["Partner", "Total Splits Ingresos", "# Ingresos", "% del Total"];

        for (var c = 0; c < headers.Length; c++)
            ws.Cell(1, c + 1).Value = headers[c];

        StyleTableHeader(ws.Range(1, 1, 1, headers.Length));

        var row = 2;
        foreach (var p in report.PartnerSummary!.Partners)
        {
            ws.Cell(row, 1).Value = p.PartnerName;
            ws.Cell(row, 2).Value = p.TotalSplitAmount;
            ws.Cell(row, 2).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 3).Value = p.IncomeCount;
            ws.Cell(row, 4).Value = p.Percentage;
            ws.Cell(row, 4).Style.NumberFormat.Format = ExcelPercentFormat;
            row++;
        }

        // Totals row
        ws.Cell(row, 2).Value = report.PartnerSummary.Partners.Sum(p => p.TotalSplitAmount);
        ws.Cell(row, 2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(row, 2).Style.Font.Bold = true;
        ws.Cell(row, 3).Value = report.PartnerSummary.Partners.Sum(p => p.IncomeCount);
        ws.Cell(row, 3).Style.Font.Bold = true;
        ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.LightGray;
        ws.Range(row, 1, row, headers.Length).Style.Border.TopBorder = XLBorderStyleValues.Thin;

        FinalizeSheetLayout(ws, 1, Math.Max(1, row), headers.Length, 1, maxColumnWidth: 36, wrapColumns: [1]);
    }
}
