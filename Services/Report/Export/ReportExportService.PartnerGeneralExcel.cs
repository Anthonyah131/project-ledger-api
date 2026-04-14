using ClosedXML.Excel;
using ProjectLedger.API.DTOs.Report;

namespace ProjectLedger.API.Services.Report;

public partial class ReportExportService
{
    // ════════════════════════════════════════════════════════
    //  PARTNER GENERAL REPORT — EXCEL
    // ════════════════════════════════════════════════════════

    /// <inheritdoc />
    public byte[] GeneratePartnerGeneralReportExcel(PartnerGeneralReportResponse report)
    {
        using var workbook = new XLWorkbook();
        ApplyWorkbookDefaults(workbook,
            _localizer["RptFmt_PartnerReportTitle", report.PartnerName].Value,
            _localizer["RptPartnerGeneral_ReportSubject"].Value);

        AddPartnerGeneralSummarySheet(workbook, report);

        foreach (var project in report.Projects)
            AddPartnerProjectSheet(workbook, project, report.PartnerName);

        if (report.PaymentMethods.Count > 0)
            AddPartnerPaymentMethodsSheet(workbook, report);

        return WorkbookToBytes(workbook);
    }

    // ── Sheet 1: Summary ─────────────────────────────────────

    /// <summary>Adds the main partner activity summary worksheet.</summary>
    private void AddPartnerGeneralSummarySheet(XLWorkbook workbook, PartnerGeneralReportResponse report)
    {
        var ws = workbook.Worksheets.Add(_localizer["RptSheet_Summary"].Value);

        // Header block
        ws.Cell(1, 1).Value = _localizer["RptCommon_Partner"].Value;        ws.Cell(1, 2).Value = report.PartnerName;
        ws.Cell(2, 1).Value = _localizer["RptCommon_Email"].Value;          ws.Cell(2, 2).Value = report.PartnerEmail ?? "—";
        ws.Cell(3, 1).Value = _localizer["RptCommon_Period"].Value;         ws.Cell(3, 2).Value = FormatDateRange(report.DateFrom, report.DateTo);
        ws.Cell(4, 1).Value = _localizer["RptCommon_Projects"].Value;       ws.Cell(4, 2).Value = report.Projects.Count;
        ws.Cell(5, 1).Value = _localizer["RptPartnerGeneral_PaymentMethods"].Value; ws.Cell(5, 2).Value = report.PaymentMethods.Count;
        ws.Cell(6, 1).Value = _localizer["RptCommon_Generated"].Value;      ws.Cell(6, 2).Value = report.GeneratedAt.ToString("yyyy-MM-dd HH:mm UTC");

        StyleHeaderRange(ws.Range(1, 1, 6, 1));

        if (report.Projects.Count == 0)
        {
            ws.Cell(9, 1).Value = _localizer["RptPartnerGeneral_NoActivity"].Value;
            ws.Cell(9, 1).Style.Font.Italic = true;
            return;
        }

        // Check if all projects share the same currency (for totals row)
        var currencies = report.Projects.Select(p => p.CurrencyCode).Distinct().ToList();
        var singleCurrency = currencies.Count == 1 ? currencies[0] : null;

        const int headerRow = 9;
        string[] headers =
        [
            _localizer["RptCommon_Projects"].Value,
            _localizer["RptCommon_Currency"].Value,
            _localizer["RptPartnerGeneral_PaidPhysically"].Value,
            _localizer["RptPartnerGeneral_OthersOwe"].Value,
            _localizer["RptPartnerGeneral_HeOwes"].Value,
            _localizer["RptPartnerGeneral_StlPaid"].Value,
            _localizer["RptPartnerGeneral_StlReceived"].Value,
            _localizer["RptCommon_NetBalance"].Value,
        ];

        for (var c = 0; c < headers.Length; c++)
            ws.Cell(headerRow, c + 1).Value = headers[c];

        StyleTableHeader(ws.Range(headerRow, 1, headerRow, headers.Length));

        var row = headerRow + 1;
        foreach (var project in report.Projects)
        {
            ws.Cell(row, 1).Value = project.ProjectName;
            ws.Cell(row, 2).Value = project.CurrencyCode;
            ws.Cell(row, 3).Value = project.PaidPhysically;
            ws.Cell(row, 3).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 4).Value = project.OthersOweHim;
            ws.Cell(row, 4).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 5).Value = project.HeOwesOthers;
            ws.Cell(row, 5).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 6).Value = project.SettlementsPaid;
            ws.Cell(row, 6).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 7).Value = project.SettlementsReceived;
            ws.Cell(row, 7).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 8).Value = project.NetBalance;
            ws.Cell(row, 8).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 8).Style.Font.Bold = true;

            if (project.NetBalance < 0)
                ws.Cell(row, 8).Style.Font.FontColor = XLColor.Red;
            else if (project.NetBalance > 0)
                ws.Cell(row, 8).Style.Font.FontColor = XLColor.DarkGreen;

            row++;
        }

        if (singleCurrency is not null)
        {
            // Totals row when all projects share the same currency
            ws.Cell(row, 1).Value = _localizer["RptCommon_Total"].Value;
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 2).Value = singleCurrency;
            for (var c = 3; c <= 8; c++)
                ws.Cell(row, c).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 3).Value = report.Projects.Sum(p => p.PaidPhysically);
            ws.Cell(row, 4).Value = report.Projects.Sum(p => p.OthersOweHim);
            ws.Cell(row, 5).Value = report.Projects.Sum(p => p.HeOwesOthers);
            ws.Cell(row, 6).Value = report.Projects.Sum(p => p.SettlementsPaid);
            ws.Cell(row, 7).Value = report.Projects.Sum(p => p.SettlementsReceived);

            var netTotal = report.Projects.Sum(p => p.NetBalance);
            ws.Cell(row, 8).Value = netTotal;
            ws.Cell(row, 8).Style.Font.Bold = true;
            if (netTotal < 0) ws.Cell(row, 8).Style.Font.FontColor = XLColor.Red;
            else if (netTotal > 0) ws.Cell(row, 8).Style.Font.FontColor = XLColor.DarkGreen;

            ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.LightGray;
            ws.Range(row, 1, row, headers.Length).Style.Border.TopBorder = XLBorderStyleValues.Thin;
        }
        else
        {
            // Note: multiple currencies
            ws.Cell(row + 1, 1).Value = _localizer["RptPartnerGeneral_MultiCurrencyNote"].Value;
            ws.Cell(row + 1, 1).Style.Font.Italic = true;
            ws.Cell(row + 1, 1).Style.Font.FontColor = XLColor.DarkOrange;
            ws.Range(row + 1, 1, row + 1, headers.Length).Merge();
        }

        FinalizeSheetLayout(ws, headerRow, row, headers.Length, headerRow);
    }

    // ── Sheets per project ──────────────────────────────────

    /// <summary>Adds a dedicated worksheet for a specific project's partner activity.</summary>
    private void AddPartnerProjectSheet(
        XLWorkbook workbook, PartnerProjectSummary project, string partnerName)
    {
        // ClosedXML sheet names are max 31 chars
        var sheetName = project.ProjectName.Length > 28
            ? project.ProjectName[..28] + "..."
            : project.ProjectName;

        // Ensure unique name if truncation creates duplicate
        var existingNames = workbook.Worksheets.Select(ws => ws.Name).ToHashSet();
        var finalName = sheetName;
        var suffix = 2;
        while (existingNames.Contains(finalName))
            finalName = $"{sheetName[..Math.Min(sheetName.Length, 28)]} {suffix++}";

        var ws = workbook.Worksheets.Add(finalName);

        // ── Block 1: Project header (rows 1-5) ───────────────
        ws.Cell(1, 1).Value = _localizer["RptCommon_Projects"].Value; ws.Cell(1, 2).Value = project.ProjectName;
        ws.Cell(2, 1).Value = _localizer["RptCommon_Currency"].Value; ws.Cell(2, 2).Value = project.CurrencyCode;
        ws.Cell(3, 1).Value = _localizer["RptCommon_Period"].Value;   ws.Cell(3, 2).Value = _localizer["RptPartnerGeneral_ReportFilter"].Value;
        ws.Cell(4, 1).Value = _localizer["RptCommon_Partner"].Value;  ws.Cell(4, 2).Value = partnerName;
        StyleHeaderRange(ws.Range(1, 1, 4, 1));

        // ── Block 2: Balance summary (rows 6-13) ─────────────
        var balanceRow = 6;
        ws.Cell(balanceRow, 1).Value = _localizer["RptPartnerGeneral_BalanceSummary"].Value;
        ws.Cell(balanceRow, 1).Style.Font.Bold = true;
        ws.Cell(balanceRow, 1).Style.Font.FontColor = XLColor.DarkBlue;
        balanceRow++;

        void WriteBalanceLine(int r, string label, decimal value, bool highlight = false)
        {
            ws.Cell(r, 1).Value = label;
            ws.Cell(r, 1).Style.Font.Bold = highlight;
            ws.Cell(r, 2).Value = value;
            ws.Cell(r, 2).Style.NumberFormat.Format = ExcelCurrencyFormat;
            if (highlight)
            {
                ws.Cell(r, 2).Style.Font.Bold = true;
                if (value < 0) ws.Cell(r, 2).Style.Font.FontColor = XLColor.Red;
                else if (value > 0) ws.Cell(r, 2).Style.Font.FontColor = XLColor.DarkGreen;
            }
        }

        StyleHeaderRange(ws.Range(balanceRow, 1, balanceRow, 1));
        WriteBalanceLine(balanceRow,     _localizer["RptFmt_BaseCurrency", project.CurrencyCode].Value, 0);
        WriteBalanceLine(balanceRow + 1, $"  {_localizer["RptPartnerGeneral_PaidPhysically"].Value}", project.PaidPhysically);
        WriteBalanceLine(balanceRow + 2, $"  {_localizer["RptPartnerGeneral_OthersOwe"].Value}", project.OthersOweHim);
        WriteBalanceLine(balanceRow + 3, $"  {_localizer["RptPartnerGeneral_HeOwes"].Value}", project.HeOwesOthers);
        WriteBalanceLine(balanceRow + 4, $"  {_localizer["RptPartnerGeneral_StlPaid"].Value}", project.SettlementsPaid);
        WriteBalanceLine(balanceRow + 5, $"  {_localizer["RptPartnerGeneral_StlReceived"].Value}", project.SettlementsReceived);
        WriteBalanceLine(balanceRow + 6, $"  {_localizer["RptCommon_NetBalance"].Value}", project.NetBalance, highlight: true);

        var nextRow = balanceRow + 8;

        // Currency totals sub-block
        if (project.CurrencyTotals.Count > 0)
        {
            ws.Cell(nextRow, 1).Value = _localizer["RptPartnerGeneral_AltCurrencyTotals"].Value;
            ws.Cell(nextRow, 1).Style.Font.Bold = true;
            ws.Cell(nextRow, 1).Style.Font.FontColor = XLColor.DarkBlue;
            nextRow++;

            string[] altHeaders =
            [
                _localizer["RptCommon_Currency"].Value,
                _localizer["RptPartnerGeneral_OthersOwe"].Value,
                _localizer["RptPartnerGeneral_HeOwesShort"].Value,
                _localizer["RptPartnerBalance_StlPaid"].Value,
                _localizer["RptPartnerBalance_StlReceived"].Value,
                _localizer["RptCommon_NetBalance"].Value,
            ];
            for (var c = 0; c < altHeaders.Length; c++)
                ws.Cell(nextRow, c + 1).Value = altHeaders[c];
            StyleTableHeader(ws.Range(nextRow, 1, nextRow, altHeaders.Length));
            nextRow++;

            foreach (var ct in project.CurrencyTotals.OrderBy(c => c.CurrencyCode))
            {
                ws.Cell(nextRow, 1).Value = ct.CurrencyCode;
                ws.Cell(nextRow, 2).Value = ct.OthersOweHim;
                ws.Cell(nextRow, 2).Style.NumberFormat.Format = ExcelCurrencyFormat;
                ws.Cell(nextRow, 3).Value = ct.HeOwesOthers;
                ws.Cell(nextRow, 3).Style.NumberFormat.Format = ExcelCurrencyFormat;
                ws.Cell(nextRow, 4).Value = ct.SettlementsPaid;
                ws.Cell(nextRow, 4).Style.NumberFormat.Format = ExcelCurrencyFormat;
                ws.Cell(nextRow, 5).Value = ct.SettlementsReceived;
                ws.Cell(nextRow, 5).Style.NumberFormat.Format = ExcelCurrencyFormat;
                ws.Cell(nextRow, 6).Value = ct.NetBalance;
                ws.Cell(nextRow, 6).Style.NumberFormat.Format = ExcelCurrencyFormat;
                ws.Cell(nextRow, 6).Style.Font.Bold = true;
                if (ct.NetBalance < 0) ws.Cell(nextRow, 6).Style.Font.FontColor = XLColor.Red;
                else if (ct.NetBalance > 0) ws.Cell(nextRow, 6).Style.Font.FontColor = XLColor.DarkGreen;
                nextRow++;
            }

            nextRow++;
        }

        // ── Block 3: Transactions table ──────────────────────
        if (project.Transactions.Count > 0)
        {
            ws.Cell(nextRow, 1).Value = _localizer["RptFmt_TransactionsCurrency", project.CurrencyCode].Value;
            ws.Cell(nextRow, 1).Style.Font.Bold = true;
            ws.Cell(nextRow, 1).Style.Font.FontColor = XLColor.DarkBlue;
            nextRow++;

            // Collect all alternative currency codes present
            var altCurrencies = project.Transactions
                .SelectMany(t => t.CurrencyExchanges)
                .Select(ce => ce.CurrencyCode)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            var txHeaders = new List<string>
            {
                _localizer["RptCommon_Date"].Value,
                _localizer["RptCommon_Type"].Value,
                _localizer["RptCommon_Title"].Value,
                _localizer["RptCommon_Category"].Value,
                _localizer["RptCommon_PaymentMethod"].Value,
                _localizer["RptPartnerGeneral_PayingPartner"].Value,
                _localizer["RptFmt_SplitAmountCurrency", project.CurrencyCode].Value,
                _localizer["RptPartnerGeneral_SplitType"].Value,
                _localizer["RptPartnerGeneral_SplitValue"].Value,
            };
            // Add one column per alternative currency (informative only)
            txHeaders.AddRange(altCurrencies.Select(c => _localizer["RptFmt_AltCurrencyRef", c].Value));

            var txHeaderRow = nextRow;
            for (var c = 0; c < txHeaders.Count; c++)
                ws.Cell(txHeaderRow, c + 1).Value = txHeaders[c];
            StyleTableHeader(ws.Range(txHeaderRow, 1, txHeaderRow, txHeaders.Count));
            nextRow++;

            foreach (var tx in project.Transactions)
            {
                ws.Cell(nextRow, 1).Value = tx.Date.ToString("yyyy-MM-dd");
                ws.Cell(nextRow, 2).Value = tx.Type;
                ws.Cell(nextRow, 3).Value = tx.Title;
                ws.Cell(nextRow, 4).Value = tx.Category ?? "—";
                ws.Cell(nextRow, 5).Value = tx.PaymentMethodName ?? "—";
                ws.Cell(nextRow, 6).Value = tx.PayingPartnerName ?? "—";
                ws.Cell(nextRow, 7).Value = tx.SplitAmount;
                ws.Cell(nextRow, 7).Style.NumberFormat.Format = ExcelCurrencyFormat;
                ws.Cell(nextRow, 8).Value = tx.SplitType;
                ws.Cell(nextRow, 9).Value = tx.SplitValue;
                if (tx.SplitType == "percentage")
                    ws.Cell(nextRow, 9).Style.NumberFormat.Format = ExcelPercentFormat;
                else
                    ws.Cell(nextRow, 9).Style.NumberFormat.Format = ExcelCurrencyFormat;

                // Fill alternative currencies (informative)
                for (var ai = 0; ai < altCurrencies.Count; ai++)
                {
                    var altCe = tx.CurrencyExchanges.FirstOrDefault(ce => ce.CurrencyCode == altCurrencies[ai]);
                    if (altCe is not null)
                    {
                        ws.Cell(nextRow, 10 + ai).Value = altCe.ConvertedAmount;
                        ws.Cell(nextRow, 10 + ai).Style.NumberFormat.Format = ExcelCurrencyFormat;
                        ws.Cell(nextRow, 10 + ai).Style.Font.Italic = true;
                        ws.Cell(nextRow, 10 + ai).Style.Font.FontColor = XLColor.Gray;
                    }
                }

                nextRow++;
            }

            // Total on split amount column only (base currency)
            ws.Cell(nextRow, 7).Value = project.Transactions.Sum(t => t.SplitAmount);
            ws.Cell(nextRow, 7).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(nextRow, 7).Style.Font.Bold = true;
            ws.Range(nextRow, 1, nextRow, txHeaders.Count).Style.Fill.BackgroundColor = XLColor.LightGray;
            ws.Range(nextRow, 1, nextRow, txHeaders.Count).Style.Border.TopBorder = XLBorderStyleValues.Thin;

            FinalizeSheetLayout(ws, txHeaderRow, nextRow, txHeaders.Count, txHeaderRow,
                wrapColumns: [3, 4], maxColumnWidth: 40);
            nextRow += 2;
        }

        // ── Block 4: Settlements table ───────────────────────
        if (project.Settlements.Count > 0)
        {
            ws.Cell(nextRow, 1).Value = _localizer["RptCommon_Settlements"].Value;
            ws.Cell(nextRow, 1).Style.Font.Bold = true;
            ws.Cell(nextRow, 1).Style.Font.FontColor = XLColor.DarkBlue;
            nextRow++;

            string[] stlHeaders =
            [
                _localizer["RptCommon_Date"].Value,
                _localizer["RptCommon_Direction"].Value,
                _localizer["RptPartnerGeneral_Counterparty"].Value,
                _localizer["RptPartnerGeneral_OriginalAmount"].Value,
                _localizer["RptCommon_Currency"].Value,
                _localizer["RptFmt_ConvertedAmountCurrency", project.CurrencyCode].Value,
            ];

            for (var c = 0; c < stlHeaders.Length; c++)
                ws.Cell(nextRow, c + 1).Value = stlHeaders[c];

            var stlHeaderRow = nextRow;
            StyleTableHeader(ws.Range(stlHeaderRow, 1, stlHeaderRow, stlHeaders.Length));
            nextRow++;

            foreach (var s in project.Settlements)
            {
                ws.Cell(nextRow, 1).Value = s.Date.ToString("yyyy-MM-dd");
                ws.Cell(nextRow, 2).Value = s.Direction == "paid_to"
                    ? _localizer["RptPartnerGeneral_PaidTo"].Value
                    : _localizer["RptPartnerGeneral_ReceivedFrom"].Value;
                ws.Cell(nextRow, 3).Value = s.OtherPartnerName;
                ws.Cell(nextRow, 4).Value = s.Amount;
                ws.Cell(nextRow, 4).Style.NumberFormat.Format = ExcelCurrencyFormat;
                ws.Cell(nextRow, 5).Value = s.Currency;
                ws.Cell(nextRow, 6).Value = s.ConvertedAmount;
                ws.Cell(nextRow, 6).Style.NumberFormat.Format = ExcelCurrencyFormat;

                if (s.Direction == "paid_to")
                    ws.Range(nextRow, 1, nextRow, stlHeaders.Length).Style.Fill.BackgroundColor = XLColor.LightSalmon;
                else
                    ws.Range(nextRow, 1, nextRow, stlHeaders.Length).Style.Fill.BackgroundColor = XLColor.LightGreen;

                nextRow++;
            }

            // Totals
            ws.Cell(nextRow, 4).Value = project.Settlements.Sum(s => s.Amount);
            ws.Cell(nextRow, 4).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(nextRow, 4).Style.Font.Bold = true;
            ws.Cell(nextRow, 6).Value = project.Settlements.Sum(s => s.ConvertedAmount);
            ws.Cell(nextRow, 6).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(nextRow, 6).Style.Font.Bold = true;
            ws.Range(nextRow, 1, nextRow, stlHeaders.Length).Style.Fill.BackgroundColor = XLColor.LightGray;
            ws.Range(nextRow, 1, nextRow, stlHeaders.Length).Style.Border.TopBorder = XLBorderStyleValues.Thin;
        }
    }

    // ── Payment Methods sheet ───────────────────────────────

    /// <summary>Adds a worksheet detailing payment method usage for the partner.</summary>
    private void AddPartnerPaymentMethodsSheet(
        XLWorkbook workbook, PartnerGeneralReportResponse report)
    {
        var ws = workbook.Worksheets.Add(_localizer["RptSheet_PaymentMethods"].Value);

        ws.Cell(1, 1).Value = _localizer["RptPartnerGeneral_PmNote"].Value;
        ws.Cell(1, 1).Style.Font.Italic = true;
        ws.Cell(1, 1).Style.Font.FontColor = XLColor.DarkOrange;
        ws.Range(1, 1, 1, 7).Merge();

        string[] headers =
        [
            _localizer["RptCommon_PaymentMethod"].Value,
            _localizer["RptCommon_Currency"].Value,
            _localizer["RptPartnerGeneral_BankName"].Value,
            _localizer["RptPartnerGeneral_TotalExpenses"].Value,
            _localizer["RptPartnerGeneral_TotalIncomes"].Value,
            _localizer["RptPartnerGeneral_NetFlow"].Value,
            _localizer["RptPartnerGeneral_TransactionCount"].Value,
        ];

        const int headerRow = 3;
        for (var c = 0; c < headers.Length; c++)
            ws.Cell(headerRow, c + 1).Value = headers[c];

        StyleTableHeader(ws.Range(headerRow, 1, headerRow, headers.Length));

        var row = headerRow + 1;
        foreach (var pm in report.PaymentMethods)
        {
            ws.Cell(row, 1).Value = pm.PaymentMethodName;
            ws.Cell(row, 2).Value = pm.Currency;
            ws.Cell(row, 3).Value = pm.BankName ?? "—";
            ws.Cell(row, 4).Value = pm.TotalExpenses;
            ws.Cell(row, 4).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 5).Value = pm.TotalIncomes;
            ws.Cell(row, 5).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 6).Value = pm.NetFlow;
            ws.Cell(row, 6).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 6).Style.Font.Bold = true;

            if (pm.NetFlow < 0)
                ws.Cell(row, 6).Style.Font.FontColor = XLColor.Red;
            else if (pm.NetFlow > 0)
                ws.Cell(row, 6).Style.Font.FontColor = XLColor.DarkGreen;

            ws.Cell(row, 7).Value = pm.TransactionCount;
            row++;
        }

        FinalizeSheetLayout(ws, headerRow, Math.Max(headerRow, row - 1), headers.Length, headerRow);
    }
}
