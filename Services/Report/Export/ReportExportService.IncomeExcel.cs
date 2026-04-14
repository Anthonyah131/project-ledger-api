using ClosedXML.Excel;
using ProjectLedger.API.DTOs.Report;

namespace ProjectLedger.API.Services.Report;

public partial class ReportExportService
{
    // ════════════════════════════════════════════════════════
    //  INCOME REPORT — EXCEL
    // ════════════════════════════════════════════════════════

    /// <inheritdoc />
    public byte[] GenerateIncomeReportExcel(DetailedIncomeReportResponse report)
    {
        using var workbook = new XLWorkbook();
        ApplyWorkbookDefaults(workbook,
            _localizer["RptIncome_ReportTitle"].Value,
            _localizer["RptIncome_ReportSubject"].Value);

        AddIncomeSheet(workbook, report);

        if (report.CategoryAnalysis is { Count: > 0 })
            AddIncomeCategoryAnalysisSheet(workbook, report);

        if (report.PaymentMethodAnalysis is { Count: > 0 })
            AddIncomePaymentMethodAnalysisSheet(workbook, report);

        return WorkbookToBytes(workbook);
    }

    /// <summary>Adds the main detailed income tracking worksheet.</summary>
    private void AddIncomeSheet(XLWorkbook workbook, DetailedIncomeReportResponse report)
    {
        var ws = workbook.Worksheets.Add(_localizer["RptSheet_Incomes"].Value);

        // ── Summary block (columns A-B) ─────────────────────
        ws.Cell(1,  1).Value = _localizer["RptCommon_Project"].Value;    ws.Cell(1,  2).Value = report.ProjectName;
        ws.Cell(2,  1).Value = _localizer["RptCommon_Currency"].Value;   ws.Cell(2,  2).Value = report.CurrencyCode;
        ws.Cell(3,  1).Value = _localizer["RptCommon_Period"].Value;     ws.Cell(3,  2).Value = FormatDateRange(report.DateFrom, report.DateTo);
        ws.Cell(4,  1).Value = _localizer["RptCommon_TotalIncome"].Value; ws.Cell(4,  2).Value = report.TotalIncome;
        ws.Cell(4,  2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(5,  1).Value = _localizer["RptCommon_IncomeCount"].Value; ws.Cell(5,  2).Value = report.TotalIncomeCount;
        ws.Cell(6,  1).Value = _localizer["RptIncome_AvgIncome"].Value;  ws.Cell(6,  2).Value = report.AverageIncomeAmount;
        ws.Cell(6,  2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(7,  1).Value = _localizer["RptIncome_AvgMonthly"].Value; ws.Cell(7,  2).Value = report.AverageMonthlyIncome;
        ws.Cell(7,  2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(8,  1).Value = _localizer["RptCommon_Generated"].Value;  ws.Cell(8,  2).Value = report.GeneratedAt.ToString("yyyy-MM-dd HH:mm UTC");

        StyleHeaderRange(ws.Range(1, 1, 8, 1));

        // ── Insights block (columns D-E) ────────────────────
        if (report.PeakMonth is not null)
        {
            ws.Cell(1, 4).Value = _localizer["RptIncome_PeakMonth"].Value;
            ws.Cell(1, 5).Value = $"{report.PeakMonth.MonthLabel} ({report.PeakMonth.Total:N2})";
        }

        if (report.LargestIncome is not null)
        {
            ws.Cell(2, 4).Value = _localizer["RptIncome_LargestIncome"].Value;
            ws.Cell(2, 5).Value = $"{report.LargestIncome.Title} ({report.LargestIncome.Amount:N2})";
            ws.Cell(3, 4).Value = _localizer["RptIncome_LargestIncomeDate"].Value;
            ws.Cell(3, 5).Value = report.LargestIncome.IncomeDate.ToString("yyyy-MM-dd");
            ws.Cell(4, 4).Value = _localizer["RptIncome_TopCategory"].Value;
            ws.Cell(4, 5).Value = report.LargestIncome.CategoryName;
        }

        StyleHeaderRange(ws.Range(1, 4, 4, 4));

        // ── Alternative currencies block (columns G-J) ──────
        var altCurrencies = report.AlternativeCurrencies ?? [];
        if (altCurrencies.Count > 0)
        {
            var altRow = 1;
            ws.Cell(altRow, 7).Value = _localizer["RptIncome_AltCurrency"].Value;
            ws.Cell(altRow, 8).Value = _localizer["RptCommon_TotalIncome"].Value;
            ws.Cell(altRow, 9).Value = _localizer["RptIncome_AvgMonthly"].Value;
            StyleTableHeader(ws.Range(altRow, 7, altRow, 9));
            altRow++;

            foreach (var alt in altCurrencies)
            {
                ws.Cell(altRow, 7).Value = alt.CurrencyCode;
                ws.Cell(altRow, 8).Value = alt.TotalIncome;
                ws.Cell(altRow, 8).Style.NumberFormat.Format = ExcelCurrencyFormat;
                ws.Cell(altRow, 9).Value = alt.AverageMonthlySpend;
                ws.Cell(altRow, 9).Style.NumberFormat.Format = ExcelCurrencyFormat;
                altRow++;
            }
        }

        // ── Detail table ────────────────────────────────────
        const int headerRow = 11;
        var altCodes = altCurrencies.Select(a => a.CurrencyCode).ToList();

        var baseHeaders = new List<string>
        {
            _localizer["RptCommon_Date"].Value,
            _localizer["RptCommon_Title"].Value,
            _localizer["RptCommon_Category"].Value,
            _localizer["RptCommon_PaymentMethod"].Value,
            _localizer["RptCommon_Type"].Value,
            _localizer["RptExpense_OriginalAmount"].Value,
            _localizer["RptIncome_OrigCurrency"].Value,
            _localizer["RptIncome_ExchangeRate"].Value,
            _localizer["RptExpense_ConvertedAmount"].Value,
            _localizer["RptExpense_AccountAmount"].Value,
            _localizer["RptExpense_AccountCurrency"].Value,
            _localizer["RptCommon_Description"].Value,
            _localizer["RptIncome_Receipt"].Value,
            _localizer["RptCommon_Notes"].Value,
        };
        foreach (var code in altCodes)
            baseHeaders.Add($"{_localizer["RptCommon_Amount"].Value} {code}");
        var headers = baseHeaders.ToArray();

        for (var c = 0; c < headers.Length; c++)
            ws.Cell(headerRow, c + 1).Value = headers[c];

        StyleTableHeader(ws.Range(headerRow, 1, headerRow, headers.Length));

        var row = headerRow + 1;
        foreach (var section in report.Sections)
        {
            // Section header (enriched with stats)
            var sectionLabel = _localizer["RptFmt_IncomeSectionLabel",
                section.MonthLabel, section.SectionCount,
                section.PercentageOfTotal, section.AverageIncomeAmount].Value;
            if (section.TopIncome is not null)
                sectionLabel += _localizer["RptFmt_SectionTopItem",
                    section.TopIncome.Title, section.TopIncome.Amount].Value;

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

                // Alternative currency columns
                for (var ci = 0; ci < altCodes.Count; ci++)
                {
                    var altExchange = inc.CurrencyExchanges?
                        .FirstOrDefault(ce => ce.CurrencyCode == altCodes[ci]);
                    var colIdx = 15 + ci;
                    if (altExchange is not null)
                    {
                        ws.Cell(row, colIdx).Value = altExchange.ConvertedAmount;
                        ws.Cell(row, colIdx).Style.NumberFormat.Format = ExcelCurrencyFormat;
                    }
                    else
                    {
                        ws.Cell(row, colIdx).Value = "—";
                    }
                }

                row++;
            }

            // Section subtotal row
            ws.Cell(row, 8).Value = _localizer["RptCommon_Subtotal"].Value;
            ws.Cell(row, 8).Style.Font.Bold = true;
            ws.Cell(row, 9).Value = section.SectionTotal;
            ws.Cell(row, 9).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 9).Style.Font.Bold = true;

            // Alternative currency subtotals
            var sectionAlt = section.AlternativeCurrencies ?? [];
            for (var ci = 0; ci < altCodes.Count; ci++)
            {
                var alt = sectionAlt.FirstOrDefault(a => a.CurrencyCode == altCodes[ci]);
                if (alt is null) continue;
                var colIdx = 15 + ci;
                ws.Cell(row, colIdx).Value = alt.TotalIncome;
                ws.Cell(row, colIdx).Style.NumberFormat.Format = ExcelCurrencyFormat;
                ws.Cell(row, colIdx).Style.Font.Bold = true;
            }

            row += 2;
        }

        FinalizeSheetLayout(ws, headerRow, row - 1, headers.Length, headerRow, wrapColumns: [12, 14]);
    }

    /// <summary>Adds a worksheet for category-based income analysis.</summary>
    private void AddIncomeCategoryAnalysisSheet(XLWorkbook workbook, DetailedIncomeReportResponse report)
    {
        var ws = workbook.Worksheets.Add(_localizer["RptSheet_Categories"].Value);

        string[] headers =
        [
            _localizer["RptCommon_Category"].Value,
            _localizer["RptCommon_TotalIncome"].Value,
            _localizer["RptCommon_IncomeCount"].Value,
            "% " + _localizer["RptCommon_Total"].Value,
            _localizer["RptIncome_AvgAmount"].Value,
        ];

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

    /// <summary>Adds a worksheet for payment method distribution analysis for incomes.</summary>
    private void AddIncomePaymentMethodAnalysisSheet(XLWorkbook workbook, DetailedIncomeReportResponse report)
    {
        var ws = workbook.Worksheets.Add(_localizer["RptSheet_PaymentMethod"].Value);

        string[] headers =
        [
            _localizer["RptCommon_PaymentMethod"].Value,
            _localizer["RptCommon_Type"].Value,
            _localizer["RptCommon_TotalIncome"].Value,
            _localizer["RptCommon_IncomeCount"].Value,
            "% " + _localizer["RptCommon_Total"].Value,
            _localizer["RptIncome_AvgAmount"].Value,
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
}
