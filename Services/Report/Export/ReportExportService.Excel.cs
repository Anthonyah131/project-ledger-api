using ClosedXML.Excel;
using ProjectLedger.API.DTOs.Report;

namespace ProjectLedger.API.Services.Report;

/// <summary>
/// Excel report generation (.xlsx) using ClosedXML.
/// </summary>
public partial class ReportExportService
{
    // ════════════════════════════════════════════════════════
    //  EXPENSE REPORT — EXCEL
    // ════════════════════════════════════════════════════════

    /// <inheritdoc />
    public byte[] GenerateExpenseReportExcel(DetailedExpenseReportResponse report)
    {
        using var workbook = new XLWorkbook();
        ApplyWorkbookDefaults(workbook, _localizer["RptExpense_ReportTitle"].Value, _localizer["RptExpense_ReportSubject"].Value);

        AddExpenseSheet(workbook, report);

        if (report.CategoryAnalysis is { Count: > 0 })
            AddCategoryAnalysisSheet(workbook, report);

        if (report.PaymentMethodAnalysis is { Count: > 0 })
            AddExpensePaymentMethodAnalysisSheet(workbook, report);

        if (report.ObligationSummary is not null)
            AddObligationsSheet(workbook, report);

        return WorkbookToBytes(workbook);
    }

    /// <summary>Adds the main detailed expense tracking worksheet.</summary>
    private void AddExpenseSheet(XLWorkbook workbook, DetailedExpenseReportResponse report)
    {
        var ws = workbook.Worksheets.Add(_localizer["RptSheet_Expenses"].Value);

        // ── Summary Block (Columns A-B) ─────────────────
        ws.Cell(1,  1).Value = _localizer["RptCommon_Project"].Value;    ws.Cell(1,  2).Value = report.ProjectName;
        ws.Cell(2,  1).Value = _localizer["RptCommon_Currency"].Value;   ws.Cell(2,  2).Value = report.CurrencyCode;
        ws.Cell(3,  1).Value = _localizer["RptCommon_Period"].Value;     ws.Cell(3,  2).Value = FormatDateRange(report.DateFrom, report.DateTo);
        ws.Cell(4,  1).Value = _localizer["RptCommon_TotalSpent"].Value; ws.Cell(4,  2).Value = report.TotalSpent;
        ws.Cell(4,  2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(5,  1).Value = _localizer["RptCommon_TotalIncome"].Value; ws.Cell(5,  2).Value = report.TotalIncome;
        ws.Cell(5,  2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(6,  1).Value = _localizer["RptCommon_NetBalance"].Value; ws.Cell(6,  2).Value = report.NetBalance;
        ws.Cell(6,  2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(7,  1).Value = _localizer["RptCommon_ExpenseCount"].Value; ws.Cell(7,  2).Value = report.TotalExpenseCount;
        ws.Cell(8,  1).Value = _localizer["RptCommon_IncomeCount"].Value;  ws.Cell(8,  2).Value = report.TotalIncomeCount;
        ws.Cell(9,  1).Value = _localizer["RptExpense_AvgExpense"].Value;  ws.Cell(9,  2).Value = report.AverageExpenseAmount;
        ws.Cell(9,  2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(10, 1).Value = _localizer["RptExpense_AvgMonthly"].Value;  ws.Cell(10, 2).Value = report.AverageMonthlySpend;
        ws.Cell(10, 2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(11, 1).Value = _localizer["RptCommon_Generated"].Value;    ws.Cell(11, 2).Value = report.GeneratedAt.ToString("yyyy-MM-dd HH:mm UTC");

        StyleHeaderRange(ws.Range(1, 1, 11, 1));

        // ── Insights Block (Columns D-E) ─────────────────
        var peakLabel = report.PeakMonth is not null
            ? $"{report.PeakMonth.MonthLabel} ({report.PeakMonth.Total:N2})"
            : GetPeakExpenseMonthLabel(report);

        ws.Cell(1, 4).Value = _localizer["RptExpense_PeakMonth"].Value;    ws.Cell(1, 5).Value = peakLabel;
        ws.Cell(2, 4).Value = _localizer["RptExpense_TopCategory"].Value;  ws.Cell(2, 5).Value = GetTopExpenseCategoryLabel(report);
        ws.Cell(3, 4).Value = _localizer["RptExpense_OverdueObligs"].Value; ws.Cell(3, 5).Value = report.ObligationSummary?.OverdueCount ?? 0;
        ws.Cell(4, 4).Value = _localizer["RptExpense_OverdueAmount"].Value; ws.Cell(4, 5).Value = report.ObligationSummary?.OverdueAmount ?? 0m;
        ws.Cell(4, 5).Style.NumberFormat.Format = ExcelCurrencyFormat;

        if (report.LargestExpense is not null)
        {
            ws.Cell(5, 4).Value = _localizer["RptExpense_LargestExpense"].Value;
            ws.Cell(5, 5).Value = $"{report.LargestExpense.Title} ({report.LargestExpense.Amount:N2})";
            ws.Cell(6, 4).Value = _localizer["RptExpense_LargestExpenseDate"].Value;
            ws.Cell(6, 5).Value = report.LargestExpense.ExpenseDate.ToString("yyyy-MM-dd");
        }

        StyleHeaderRange(ws.Range(1, 4, 6, 4));

        // ── Alternative Currencies Block (Columns G-J) ───
        var altCurrencies = report.AlternativeCurrencies ?? [];
        if (altCurrencies.Count > 0)
        {
            var altRow = 1;
            ws.Cell(altRow, 7).Value = _localizer["RptExpense_AltCurrency"].Value;
            ws.Cell(altRow, 8).Value = _localizer["RptCommon_TotalSpent"].Value;
            ws.Cell(altRow, 9).Value = _localizer["RptCommon_TotalIncome"].Value;
            ws.Cell(altRow, 10).Value = _localizer["RptCommon_NetBalance"].Value;
            StyleTableHeader(ws.Range(altRow, 7, altRow, 10));
            altRow++;

            foreach (var alt in altCurrencies)
            {
                ws.Cell(altRow, 7).Value = alt.CurrencyCode;
                ws.Cell(altRow, 8).Value = alt.TotalSpent;
                ws.Cell(altRow, 8).Style.NumberFormat.Format = ExcelCurrencyFormat;
                ws.Cell(altRow, 9).Value = alt.TotalIncome;
                ws.Cell(altRow, 9).Style.NumberFormat.Format = ExcelCurrencyFormat;
                ws.Cell(altRow, 10).Value = alt.NetBalance;
                ws.Cell(altRow, 10).Style.NumberFormat.Format = ExcelCurrencyFormat;
                altRow++;
            }
        }

        // ── Expense Table ───────────────────────────────────
        var baseHeaders = new List<string>
        {
            _localizer["RptCommon_Date"].Value,
            _localizer["RptCommon_Title"].Value,
            _localizer["RptCommon_Category"].Value,
            _localizer["RptCommon_PaymentMethod"].Value,
            _localizer["RptCommon_Type"].Value,
            _localizer["RptExpense_OriginalAmount"].Value,
            _localizer["RptExpense_OrigCurrency"].Value,
            _localizer["RptExpense_ExchangeRate"].Value,
            _localizer["RptExpense_ConvertedAmount"].Value,
            _localizer["RptExpense_AccountAmount"].Value,
            _localizer["RptExpense_AccountCurrency"].Value,
            _localizer["RptCommon_Description"].Value,
            _localizer["RptExpense_ReceiptNo"].Value,
            _localizer["RptCommon_Notes"].Value,
            _localizer["RptExpense_IsObligPayment"].Value,
            _localizer["RptExpense_Obligation"].Value,
        };
        var altCodes = altCurrencies.Select(a => a.CurrencyCode).ToList();
        foreach (var code in altCodes)
            baseHeaders.Add(code);
        var headers = baseHeaders.ToArray();

        const int tableStartRow = 13;
        for (var col = 1; col <= headers.Length; col++)
            ws.Cell(tableStartRow, col).Value = headers[col - 1];
        StyleTableHeader(ws.Range(tableStartRow, 1, tableStartRow, headers.Length));

        var row = tableStartRow + 1;

        foreach (var section in report.Sections)
        {
            // ── Monthly Section Row (Enriched) ─────────
            var sectionLabel = _localizer["RptFmt_ExpenseSectionLabel",
                section.MonthLabel, section.SectionCount, section.PercentageOfTotal, section.AverageExpenseAmount].Value;
            if (section.TopExpense is not null)
                sectionLabel += _localizer["RptFmt_SectionTopItem", section.TopExpense.Title, section.TopExpense.Amount].Value;

            ws.Cell(row, 1).Value = sectionLabel;
            ws.Cell(row, 1).Style.Font.Bold   = true;
            ws.Cell(row, 1).Style.Font.Italic = true;
            ws.Range(row, 1, row, headers.Length).Merge()
              .Style.Fill.BackgroundColor = XLColor.LightGray;
            row++;

            foreach (var exp in section.Expenses)
            {
                ws.Cell(row, 1).Value = exp.ExpenseDate.ToString("yyyy-MM-dd");
                ws.Cell(row, 2).Value = exp.Title;
                ws.Cell(row, 3).Value = exp.CategoryName;
                ws.Cell(row, 4).Value = exp.PaymentMethodName;
                ws.Cell(row, 5).Value = exp.PaymentMethodType;
                ws.Cell(row, 6).Value = exp.OriginalAmount;
                ws.Cell(row, 6).Style.NumberFormat.Format = ExcelCurrencyFormat;
                ws.Cell(row, 7).Value = exp.OriginalCurrency;
                ws.Cell(row, 8).Value = exp.ExchangeRate;
                ws.Cell(row, 8).Style.NumberFormat.Format = ExcelExchangeRateFormat;
                ws.Cell(row, 9).Value = exp.ConvertedAmount;
                ws.Cell(row, 9).Style.NumberFormat.Format = ExcelCurrencyFormat;

                if (exp.AccountAmount.HasValue)
                {
                    ws.Cell(row, 10).Value = exp.AccountAmount.Value;
                    ws.Cell(row, 10).Style.NumberFormat.Format = ExcelCurrencyFormat;
                }
                else
                {
                    ws.Cell(row, 10).Value = "—";
                }

                ws.Cell(row, 11).Value = exp.AccountCurrency ?? "—";
                ws.Cell(row, 12).Value = exp.Description    ?? "";
                ws.Cell(row, 13).Value = exp.ReceiptNumber  ?? "";
                ws.Cell(row, 14).Value = exp.Notes          ?? "";
                ws.Cell(row, 15).Value = exp.IsObligationPayment ? _localizer["RptCommon_YesLabel"].Value : _localizer["RptCommon_NoLabel"].Value;
                ws.Cell(row, 16).Value = exp.ObligationTitle ?? "";

                // Alternative currency columns
                for (var ci = 0; ci < altCodes.Count; ci++)
                {
                    var altExchange = exp.CurrencyExchanges?
                        .FirstOrDefault(ce => ce.CurrencyCode == altCodes[ci]);
                    var colIdx = 17 + ci;
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

            WriteSectionTotals(ws, row, section, altCodes);
            row += 3;
        }

        FinalizeSheetLayout(
            ws,
            headerRow: tableStartRow,
            lastRow:   Math.Max(tableStartRow, row - 1),
            lastColumn: headers.Length,
            freezeRows: tableStartRow,
            enableAutoFilter: false,
            maxColumnWidth: 44,
            wrapColumns: [12, 14, 16]);
    }

    /// <summary>Writes monthly subtotal summary rows for a specific section.</summary>
    private void WriteSectionTotals(IXLWorksheet ws, int startRow, MonthlyExpenseSection section, List<string> altCodes)
    {
        void TotalRow(int offset, string label, decimal value)
        {
            ws.Cell(startRow + offset, 8).Value = label;
            ws.Cell(startRow + offset, 8).Style.Font.Bold = true;
            ws.Cell(startRow + offset, 9).Value = value;
            ws.Cell(startRow + offset, 9).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(startRow + offset, 9).Style.Font.Bold = true;
        }

        TotalRow(0, _localizer["RptCommon_Subtotal"].Value, section.SectionTotal);
        TotalRow(1, _localizer["RptCommon_TotalIncome"].Value + ":", section.SectionIncomeTotal);
        TotalRow(2, _localizer["RptCommon_NetBalance"].Value + ":", section.SectionNetBalance);

        // Alternative currency subtotals in the same rows
        var sectionAlt = section.AlternativeCurrencies ?? [];
        for (var ci = 0; ci < altCodes.Count; ci++)
        {
            var alt = sectionAlt.FirstOrDefault(a => a.CurrencyCode == altCodes[ci]);
            if (alt is null) continue;
            var colIdx = 17 + ci;
            ws.Cell(startRow, colIdx).Value = alt.TotalSpent;
            ws.Cell(startRow, colIdx).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(startRow, colIdx).Style.Font.Bold = true;
            ws.Cell(startRow + 1, colIdx).Value = alt.TotalIncome;
            ws.Cell(startRow + 1, colIdx).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(startRow + 1, colIdx).Style.Font.Bold = true;
            ws.Cell(startRow + 2, colIdx).Value = alt.NetBalance;
            ws.Cell(startRow + 2, colIdx).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(startRow + 2, colIdx).Style.Font.Bold = true;
        }
    }

    /// <summary>Adds a worksheet for category-based budgetary analysis.</summary>
    private void AddCategoryAnalysisSheet(XLWorkbook workbook, DetailedExpenseReportResponse report)
    {
        var ws = workbook.Worksheets.Add(_localizer["RptSheet_Categories"].Value);

        var headers = new[]
        {
            _localizer["RptCommon_Category"].Value,
            _localizer["RptExpense_IsDefault"].Value,
            _localizer["RptExpense_Budget"].Value,
            _localizer["RptExpense_Spent"].Value,
            _localizer["RptCommon_ExpenseCount"].Value,
            "%",
            _localizer["RptExpense_Remaining"].Value,
            _localizer["RptExpense_UsedPct"].Value,
            _localizer["RptExpense_Exceeded"].Value,
        };

        for (var col = 1; col <= headers.Length; col++)
            ws.Cell(1, col).Value = headers[col - 1];
        StyleTableHeader(ws.Range(1, 1, 1, headers.Length));

        var row = 2;
        foreach (var cat in report.CategoryAnalysis!)
        {
            ws.Cell(row, 1).Value = cat.CategoryName;
            ws.Cell(row, 2).Value = cat.IsDefault ? _localizer["RptCommon_YesLabel"].Value : _localizer["RptCommon_NoLabel"].Value;
            ws.Cell(row, 3).Value = cat.BudgetAmount ?? 0;
            ws.Cell(row, 3).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 4).Value = cat.SpentAmount;
            ws.Cell(row, 4).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 5).Value = cat.ExpenseCount;
            ws.Cell(row, 6).Value = cat.Percentage;
            ws.Cell(row, 6).Style.NumberFormat.Format = ExcelPercentFormat;
            ws.Cell(row, 7).Value = cat.BudgetRemaining ?? 0;
            ws.Cell(row, 7).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 8).Value = cat.BudgetUsedPercentage ?? 0;
            ws.Cell(row, 8).Style.NumberFormat.Format = ExcelPercentFormat;
            ws.Cell(row, 9).Value = cat.BudgetExceeded == true ? _localizer["RptExpense_ExceededMark"].Value : _localizer["RptCommon_NoLabel"].Value;

            if (cat.BudgetExceeded == true)
                ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.LightCoral;

            row++;
        }

        // Totals row
        ws.Cell(row, 3).Value = report.CategoryAnalysis.Sum(c => c.BudgetAmount ?? 0);
        ws.Cell(row, 3).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(row, 3).Style.Font.Bold = true;
        ws.Cell(row, 4).Value = report.CategoryAnalysis.Sum(c => c.SpentAmount);
        ws.Cell(row, 4).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(row, 4).Style.Font.Bold = true;
        ws.Cell(row, 5).Value = report.CategoryAnalysis.Sum(c => c.ExpenseCount);
        ws.Cell(row, 5).Style.Font.Bold = true;
        ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.LightGray;
        ws.Range(row, 1, row, headers.Length).Style.Border.TopBorder = XLBorderStyleValues.Thin;

        FinalizeSheetLayout(
            ws,
            headerRow: 1,
            lastRow:   Math.Max(1, row),
            lastColumn: headers.Length,
            freezeRows: 1,
            maxColumnWidth: 36,
            wrapColumns: [1]);
    }

    /// <summary>Adds a worksheet for payment method distribution analysis.</summary>
    private void AddExpensePaymentMethodAnalysisSheet(XLWorkbook workbook, DetailedExpenseReportResponse report)
    {
        var ws = workbook.Worksheets.Add(_localizer["RptSheet_PaymentMethod"].Value);

        var headers = new[]
        {
            _localizer["RptCommon_PaymentMethod"].Value,
            _localizer["RptCommon_Type"].Value,
            _localizer["RptCommon_TotalSpent"].Value,
            _localizer["RptCommon_ExpenseCount"].Value,
            "%",
            _localizer["RptExpense_AvgExpense"].Value,
        };

        for (var col = 1; col <= headers.Length; col++)
            ws.Cell(1, col).Value = headers[col - 1];
        StyleTableHeader(ws.Range(1, 1, 1, headers.Length));

        var row = 2;
        foreach (var pm in report.PaymentMethodAnalysis!)
        {
            ws.Cell(row, 1).Value = pm.PaymentMethodName;
            ws.Cell(row, 2).Value = pm.Type;
            ws.Cell(row, 3).Value = pm.SpentAmount;
            ws.Cell(row, 3).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 4).Value = pm.ExpenseCount;
            ws.Cell(row, 5).Value = pm.Percentage;
            ws.Cell(row, 5).Style.NumberFormat.Format = ExcelPercentFormat;
            ws.Cell(row, 6).Value = pm.AverageExpenseAmount;
            ws.Cell(row, 6).Style.NumberFormat.Format = ExcelCurrencyFormat;
            row++;
        }

        // Totals row
        ws.Cell(row, 3).Value = report.PaymentMethodAnalysis.Sum(p => p.SpentAmount);
        ws.Cell(row, 3).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(row, 3).Style.Font.Bold = true;
        ws.Cell(row, 4).Value = report.PaymentMethodAnalysis.Sum(p => p.ExpenseCount);
        ws.Cell(row, 4).Style.Font.Bold = true;
        ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.LightGray;
        ws.Range(row, 1, row, headers.Length).Style.Border.TopBorder = XLBorderStyleValues.Thin;

        FinalizeSheetLayout(ws, 1, Math.Max(1, row), headers.Length, 1, maxColumnWidth: 36, wrapColumns: [1]);
    }

    /// <summary>Adds a worksheet for obligation status and tracking.</summary>
    private void AddObligationsSheet(XLWorkbook workbook, DetailedExpenseReportResponse report)
    {
        var ws  = workbook.Worksheets.Add(_localizer["RptSheet_Obligations"].Value);
        var obl = report.ObligationSummary!;

        // ── Summary ───────────────────────────────────────────
        ws.Cell(1, 1).Value = _localizer["RptExpense_ObligTotalObligs"].Value; ws.Cell(1, 2).Value = obl.TotalObligations;
        ws.Cell(2, 1).Value = _localizer["RptExpense_ObligTotalAmount"].Value; ws.Cell(2, 2).Value = obl.TotalAmount;
        ws.Cell(2, 2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(3, 1).Value = _localizer["RptExpense_ObligTotalPaid"].Value;   ws.Cell(3, 2).Value = obl.TotalPaid;
        ws.Cell(3, 2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(4, 1).Value = _localizer["RptExpense_ObligTotalPending"].Value; ws.Cell(4, 2).Value = obl.TotalPending;
        ws.Cell(4, 2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(5, 1).Value = _localizer["RptExpense_ObligOverdue"].Value;     ws.Cell(5, 2).Value = obl.OverdueCount;
        ws.Cell(6, 1).Value = _localizer["RptExpense_OverdueAmount"].Value;    ws.Cell(6, 2).Value = obl.OverdueAmount;
        ws.Cell(6, 2).Style.NumberFormat.Format = ExcelCurrencyFormat;

        StyleHeaderRange(ws.Range(1, 1, 6, 1));

        // ── Table ─────────────────────────────────────────────
        var headers = new[]
        {
            _localizer["RptCommon_Status"].Value,
            _localizer["RptCommon_Title"].Value,
            _localizer["RptCommon_Description"].Value,
            _localizer["RptExpense_ObligTotalAmount"].Value,
            _localizer["RptCommon_Paid"].Value,
            _localizer["RptExpense_Remaining"].Value,
            "%",
            _localizer["RptCommon_Currency"].Value,
            _localizer["RptExpense_DueDate"].Value,
            _localizer["RptExpense_PaymentCount"].Value,
            _localizer["RptExpense_LastPayment"].Value,
        };

        const int tableStartRow = 8;
        for (var col = 1; col <= headers.Length; col++)
            ws.Cell(tableStartRow, col).Value = headers[col - 1];
        StyleTableHeader(ws.Range(tableStartRow, 1, tableStartRow, headers.Length));

        var row = tableStartRow + 1;
        foreach (var group in obl.ByStatus)
        {
            foreach (var item in group.Obligations)
            {
                ws.Cell(row, 1).Value = FormatStatus(item.Status);
                ws.Cell(row, 2).Value = item.Title;
                ws.Cell(row, 3).Value = item.Description ?? "";
                ws.Cell(row, 4).Value = item.TotalAmount;
                ws.Cell(row, 4).Style.NumberFormat.Format = ExcelCurrencyFormat;
                ws.Cell(row, 5).Value = item.PaidAmount;
                ws.Cell(row, 5).Style.NumberFormat.Format = ExcelCurrencyFormat;
                ws.Cell(row, 6).Value = item.RemainingAmount;
                ws.Cell(row, 6).Style.NumberFormat.Format = ExcelCurrencyFormat;
                ws.Cell(row, 7).Value = item.TotalAmount > 0
                    ? item.PaidAmount / item.TotalAmount * 100 : 0m;
                ws.Cell(row, 7).Style.NumberFormat.Format = "0.0\"%\";(0.0\"%\");-";
                ws.Cell(row, 8).Value = item.Currency;
                ws.Cell(row, 9).Value = item.DueDate?.ToString("yyyy-MM-dd") ?? "—";
                ws.Cell(row, 10).Value = item.PaymentCount;
                ws.Cell(row, 11).Value = item.LastPaymentDate?.ToString("yyyy-MM-dd") ?? "—";

                if (item.Status == "overdue")
                    ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.LightCoral;
                else if (item.Status == "paid")
                    ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.LightGreen;

                row++;
            }
        }

        FinalizeSheetLayout(
            ws,
            headerRow: tableStartRow,
            lastRow:   Math.Max(tableStartRow, row - 1),
            lastColumn: headers.Length,
            freezeRows: tableStartRow,
            maxColumnWidth: 40,
            wrapColumns: [2, 3]);
    }

}
