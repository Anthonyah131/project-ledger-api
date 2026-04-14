using ClosedXML.Excel;
using ProjectLedger.API.DTOs.Partner;
using ProjectLedger.API.DTOs.Report;

namespace ProjectLedger.API.Services.Report;

public partial class ReportExportService
{
    // ════════════════════════════════════════════════════════
    //  PARTNER BALANCE REPORT — EXCEL
    // ════════════════════════════════════════════════════════

    /// <inheritdoc />
    public byte[] GeneratePartnerBalanceReportExcel(PartnerBalanceReportResponse report)
    {
        using var workbook = new XLWorkbook();
        ApplyWorkbookDefaults(workbook,
            _localizer["RptPartnerBalance_ReportTitle"].Value,
            _localizer["RptPartnerBalance_ReportSubject"].Value);

        AddPartnerBalanceSheet(workbook, report);

        if (report.Settlements.Count > 0)
            AddSettlementsSheet(workbook, report);

        if (report.PairwiseBalances.Count > 0)
            AddPairwiseSheet(workbook, report);

        if (report.Warnings.Count > 0)
            AddPartnerBalanceWarningsSheet(workbook, report);

        return WorkbookToBytes(workbook);
    }

    /// <summary>Adds the main partner balance summary worksheet.</summary>
    private void AddPartnerBalanceSheet(XLWorkbook workbook, PartnerBalanceReportResponse report)
    {
        var ws = workbook.Worksheets.Add(_localizer["RptSheet_Balances"].Value);

        // ── Summary block ───────────────────────────────────
        ws.Cell(1, 1).Value = _localizer["RptCommon_Project"].Value;              ws.Cell(1, 2).Value = report.ProjectName;
        ws.Cell(2, 1).Value = _localizer["RptCommon_Currency"].Value;             ws.Cell(2, 2).Value = report.CurrencyCode;
        ws.Cell(3, 1).Value = _localizer["RptCommon_Period"].Value;               ws.Cell(3, 2).Value = FormatDateRange(report.DateFrom, report.DateTo);
        ws.Cell(4, 1).Value = _localizer["RptCommon_Partner"].Value;              ws.Cell(4, 2).Value = report.Partners.Count;
        ws.Cell(5, 1).Value = _localizer["RptCommon_Settlements"].Value;          ws.Cell(5, 2).Value = report.Settlements.Count;
        ws.Cell(6, 1).Value = _localizer["RptCommon_Generated"].Value;            ws.Cell(6, 2).Value = report.GeneratedAt.ToString("yyyy-MM-dd HH:mm UTC");

        // Insights block
        var maxOwes = report.Partners.OrderBy(p => p.NetBalance).FirstOrDefault();
        var maxOwed = report.Partners.OrderByDescending(p => p.NetBalance).FirstOrDefault();

        if (maxOwes is not null && maxOwes.NetBalance < 0)
        {
            ws.Cell(1, 4).Value = _localizer["RptPartnerBalance_OwesMore"].Value;
            ws.Cell(1, 5).Value = $"{maxOwes.PartnerName} ({maxOwes.NetBalance:N2})";
            ws.Cell(1, 5).Style.Font.FontColor = XLColor.Red;
        }

        if (maxOwed is not null && maxOwed.NetBalance > 0)
        {
            ws.Cell(2, 4).Value = _localizer["RptPartnerBalance_OwedMore"].Value;
            ws.Cell(2, 5).Value = $"{maxOwed.PartnerName} ({maxOwed.NetBalance:N2})";
            ws.Cell(2, 5).Style.Font.FontColor = XLColor.DarkGreen;
        }

        ws.Cell(3, 4).Value = _localizer["RptPartnerBalance_TotalSettlements"].Value;
        ws.Cell(3, 5).Value = report.Settlements.Sum(s => s.ConvertedAmount);
        ws.Cell(3, 5).Style.NumberFormat.Format = ExcelCurrencyFormat;

        StyleHeaderRange(ws.Range(1, 1, 6, 1));
        StyleHeaderRange(ws.Range(1, 4, 3, 4));

        // ── Partner balance table ───────────────────────────
        const int headerRow = 9;
        string[] headers =
        [
            _localizer["RptCommon_Partner"].Value,
            _localizer["RptFmt_PaidPhysicallyCurrency", report.CurrencyCode].Value,
            _localizer["RptFmt_OthersOweCurrency", report.CurrencyCode].Value,
            _localizer["RptFmt_HeOwesCurrency", report.CurrencyCode].Value,
            _localizer["RptFmt_StlPaidCurrency", report.CurrencyCode].Value,
            _localizer["RptFmt_StlReceivedCurrency", report.CurrencyCode].Value,
            _localizer["RptFmt_NetBalanceCurrency", report.CurrencyCode].Value,
        ];

        for (var c = 0; c < headers.Length; c++)
            ws.Cell(headerRow, c + 1).Value = headers[c];

        StyleTableHeader(ws.Range(headerRow, 1, headerRow, headers.Length));

        var row = headerRow + 1;
        foreach (var p in report.Partners)
        {
            ws.Cell(row, 1).Value = p.PartnerName;
            ws.Cell(row, 2).Value = p.PaidPhysically;
            ws.Cell(row, 2).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 3).Value = p.OthersOweHim;
            ws.Cell(row, 3).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 4).Value = p.HeOwesOthers;
            ws.Cell(row, 4).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 5).Value = p.SettlementsPaid;
            ws.Cell(row, 5).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 6).Value = p.SettlementsReceived;
            ws.Cell(row, 6).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 7).Value = p.NetBalance;
            ws.Cell(row, 7).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 7).Style.Font.Bold = true;

            if (p.NetBalance < 0)
                ws.Cell(row, 7).Style.Font.FontColor = XLColor.Red;
            else if (p.NetBalance > 0)
                ws.Cell(row, 7).Style.Font.FontColor = XLColor.DarkGreen;

            row++;
        }

        // Totals row
        for (var c = 2; c <= 7; c++)
        {
            ws.Cell(row, c).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, c).Style.Font.Bold = true;
        }
        ws.Cell(row, 2).Value = report.Partners.Sum(p => p.PaidPhysically);
        ws.Cell(row, 3).Value = report.Partners.Sum(p => p.OthersOweHim);
        ws.Cell(row, 4).Value = report.Partners.Sum(p => p.HeOwesOthers);
        ws.Cell(row, 5).Value = report.Partners.Sum(p => p.SettlementsPaid);
        ws.Cell(row, 6).Value = report.Partners.Sum(p => p.SettlementsReceived);
        ws.Cell(row, 7).Value = report.Partners.Sum(p => p.NetBalance);
        ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.LightGray;
        ws.Range(row, 1, row, headers.Length).Style.Border.TopBorder = XLBorderStyleValues.Thin;

        // ── Currency totals sub-table ────────────────────────
        var currencies = report.Partners
            .SelectMany(p => p.CurrencyTotals)
            .Select(ct => ct.CurrencyCode)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        if (currencies.Count > 0)
        {
            var altRow = row + 2;
            ws.Cell(altRow, 1).Value = _localizer["RptPartnerBalance_AltCurrencyTotals"].Value;
            ws.Cell(altRow, 1).Style.Font.Bold = true;
            ws.Cell(altRow, 1).Style.Font.FontColor = XLColor.DarkBlue;
            altRow++;

            string[] altHeaders =
            [
                _localizer["RptCommon_Partner"].Value,
                _localizer["RptCommon_Currency"].Value,
                _localizer["RptPartnerBalance_OthersOwe"].Value,
                _localizer["RptPartnerBalance_HeOwes"].Value,
                _localizer["RptPartnerBalance_StlPaid"].Value,
                _localizer["RptPartnerBalance_StlReceived"].Value,
                _localizer["RptCommon_Balance"].Value,
            ];
            for (var c = 0; c < altHeaders.Length; c++)
                ws.Cell(altRow, c + 1).Value = altHeaders[c];
            StyleTableHeader(ws.Range(altRow, 1, altRow, altHeaders.Length));
            altRow++;

            foreach (var p in report.Partners)
            {
                foreach (var ct in p.CurrencyTotals.OrderBy(c => c.CurrencyCode))
                {
                    ws.Cell(altRow, 1).Value = p.PartnerName;
                    ws.Cell(altRow, 2).Value = ct.CurrencyCode;
                    ws.Cell(altRow, 3).Value = ct.OthersOweHim;
                    ws.Cell(altRow, 3).Style.NumberFormat.Format = ExcelCurrencyFormat;
                    ws.Cell(altRow, 4).Value = ct.HeOwesOthers;
                    ws.Cell(altRow, 4).Style.NumberFormat.Format = ExcelCurrencyFormat;
                    ws.Cell(altRow, 5).Value = ct.SettlementsPaid;
                    ws.Cell(altRow, 5).Style.NumberFormat.Format = ExcelCurrencyFormat;
                    ws.Cell(altRow, 6).Value = ct.SettlementsReceived;
                    ws.Cell(altRow, 6).Style.NumberFormat.Format = ExcelCurrencyFormat;
                    ws.Cell(altRow, 7).Value = ct.NetBalance;
                    ws.Cell(altRow, 7).Style.NumberFormat.Format = ExcelCurrencyFormat;
                    ws.Cell(altRow, 7).Style.Font.Bold = true;

                    if (ct.NetBalance < 0)
                        ws.Cell(altRow, 7).Style.Font.FontColor = XLColor.Red;
                    else if (ct.NetBalance > 0)
                        ws.Cell(altRow, 7).Style.Font.FontColor = XLColor.DarkGreen;

                    altRow++;
                }
            }
        }

        FinalizeSheetLayout(ws, headerRow, row, headers.Length, headerRow);
    }

    /// <summary>Adds a worksheet detailing settlements between partners.</summary>
    private void AddSettlementsSheet(XLWorkbook workbook, PartnerBalanceReportResponse report)
    {
        var ws = workbook.Worksheets.Add(_localizer["RptSheet_Settlements"].Value);

        string[] headers =
        [
            _localizer["RptCommon_Date"].Value,
            _localizer["RptPartnerBalance_From"].Value,
            _localizer["RptPartnerBalance_To"].Value,
            _localizer["RptCommon_Amount"].Value,
            _localizer["RptCommon_Currency"].Value,
            _localizer["RptIncome_ExchangeRate"].Value,
            _localizer["RptExpense_ConvertedAmount"].Value,
            _localizer["RptCommon_Description"].Value,
            _localizer["RptCommon_Notes"].Value,
        ];

        for (var c = 0; c < headers.Length; c++)
            ws.Cell(1, c + 1).Value = headers[c];

        StyleTableHeader(ws.Range(1, 1, 1, headers.Length));

        var row = 2;
        foreach (var s in report.Settlements)
        {
            ws.Cell(row, 1).Value = s.SettlementDate.ToString("yyyy-MM-dd");
            ws.Cell(row, 2).Value = s.FromPartnerName;
            ws.Cell(row, 3).Value = s.ToPartnerName;
            ws.Cell(row, 4).Value = s.Amount;
            ws.Cell(row, 4).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 5).Value = s.Currency;
            ws.Cell(row, 6).Value = s.ExchangeRate;
            ws.Cell(row, 6).Style.NumberFormat.Format = ExcelExchangeRateFormat;
            ws.Cell(row, 7).Value = s.ConvertedAmount;
            ws.Cell(row, 7).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 8).Value = s.Description ?? "";
            ws.Cell(row, 9).Value = s.Notes ?? "";
            row++;
        }

        // Totals row
        ws.Cell(row, 4).Value = report.Settlements.Sum(s => s.Amount);
        ws.Cell(row, 4).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(row, 4).Style.Font.Bold = true;
        ws.Cell(row, 7).Value = report.Settlements.Sum(s => s.ConvertedAmount);
        ws.Cell(row, 7).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(row, 7).Style.Font.Bold = true;
        ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.LightGray;
        ws.Range(row, 1, row, headers.Length).Style.Border.TopBorder = XLBorderStyleValues.Thin;

        FinalizeSheetLayout(ws, 1, row, headers.Length, 1, wrapColumns: [8, 9]);
    }

    /// <summary>Adds a worksheet for pairwise balance tracking between partner combinations.</summary>
    private void AddPairwiseSheet(XLWorkbook workbook, PartnerBalanceReportResponse report)
    {
        var ws = workbook.Worksheets.Add(_localizer["RptSheet_PairwiseBalances"].Value);

        string[] headers =
        [
            "Partner A", "Partner B",
            _localizer["RptFmt_AOwesB", report.CurrencyCode].Value,
            _localizer["RptFmt_BOwesA", report.CurrencyCode].Value,
            _localizer["RptFmt_StlAtoB", report.CurrencyCode].Value,
            _localizer["RptFmt_StlBtoA", report.CurrencyCode].Value,
            _localizer["RptFmt_NetBalanceCurrency", report.CurrencyCode].Value,
            _localizer["RptCommon_Direction"].Value,
            _localizer["RptCommon_Status"].Value,
        ];

        for (var c = 0; c < headers.Length; c++)
            ws.Cell(1, c + 1).Value = headers[c];

        StyleTableHeader(ws.Range(1, 1, 1, headers.Length));

        var row = 2;
        foreach (var pw in report.PairwiseBalances)
        {
            ws.Cell(row, 1).Value = pw.PartnerAName;
            ws.Cell(row, 2).Value = pw.PartnerBName;
            ws.Cell(row, 3).Value = pw.AOwesB;
            ws.Cell(row, 3).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 4).Value = pw.BOwesA;
            ws.Cell(row, 4).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 5).Value = pw.SettlementsAToB;
            ws.Cell(row, 5).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 6).Value = pw.SettlementsBToA;
            ws.Cell(row, 6).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 7).Value = pw.NetBalance;
            ws.Cell(row, 7).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 7).Style.Font.Bold = true;

            ws.Cell(row, 8).Value = pw.NetBalance > 0
                ? $"{pw.PartnerAName} → {pw.PartnerBName}"
                : pw.NetBalance < 0
                    ? $"{pw.PartnerBName} → {pw.PartnerAName}"
                    : _localizer["RptPartnerBalance_Saldado"].Value;

            if (pw.NetBalance == 0)
            {
                ws.Cell(row, 9).Value = _localizer["RptPartnerBalance_Settled"].Value;
                ws.Cell(row, 9).Style.Font.FontColor = XLColor.DarkGreen;
                ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.LightGreen;
            }
            else
            {
                ws.Cell(row, 9).Value = _localizer["RptPartnerBalance_Pending"].Value;
                ws.Cell(row, 9).Style.Font.FontColor = XLColor.DarkOrange;

                if (pw.NetBalance < 0)
                    ws.Cell(row, 7).Style.Font.FontColor = XLColor.Red;
                else
                    ws.Cell(row, 7).Style.Font.FontColor = XLColor.DarkGreen;
            }

            row++;
        }

        FinalizeSheetLayout(ws, 1, Math.Max(1, row - 1), headers.Length, 1, maxColumnWidth: 36);
    }

    /// <summary>Adds a worksheet for transactions with missing exchange rates.</summary>
    private void AddPartnerBalanceWarningsSheet(XLWorkbook workbook, PartnerBalanceReportResponse report)
    {
        var ws = workbook.Worksheets.Add(_localizer["RptSheet_Warnings"].Value);

        ws.Cell(1, 1).Value = _localizer["RptPartnerBalance_WarningsNote"].Value;
        ws.Cell(1, 1).Style.Font.Italic = true;
        ws.Cell(1, 1).Style.Font.FontColor = XLColor.DarkOrange;
        ws.Range(1, 1, 1, 5).Merge();

        string[] headers =
        [
            _localizer["RptCommon_Type"].Value,
            _localizer["RptCommon_Title"].Value,
            _localizer["RptCommon_Date"].Value,
            _localizer["RptCommon_Amount"].Value,
        ];
        for (var c = 0; c < headers.Length; c++)
            ws.Cell(3, c + 1).Value = headers[c];
        StyleTableHeader(ws.Range(3, 1, 3, headers.Length));

        var row = 4;
        foreach (var w in report.Warnings)
        {
            ws.Cell(row, 1).Value = w.TransactionType;
            ws.Cell(row, 2).Value = w.Title;
            ws.Cell(row, 3).Value = w.Date.ToString("yyyy-MM-dd");
            ws.Cell(row, 4).Value = w.ConvertedAmount;
            ws.Cell(row, 4).Style.NumberFormat.Format = ExcelCurrencyFormat;
            row++;
        }

        FinalizeSheetLayout(ws, 3, Math.Max(3, row - 1), headers.Length, 3);
    }
}
