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
            $"Reporte Partner — {report.PartnerName}",
            "Actividad consolidada del partner por proyecto y método de pago.");

        AddPartnerGeneralSummarySheet(workbook, report);

        foreach (var project in report.Projects)
            AddPartnerProjectSheet(workbook, project, report.PartnerName);

        if (report.PaymentMethods.Count > 0)
            AddPartnerPaymentMethodsSheet(workbook, report);

        return WorkbookToBytes(workbook);
    }

    // ── Sheet 1: Summary ─────────────────────────────────────

    /// <summary>Adds the main partner activity summary worksheet.</summary>
    private static void AddPartnerGeneralSummarySheet(XLWorkbook workbook, PartnerGeneralReportResponse report)
    {
        var ws = workbook.Worksheets.Add("Resumen");

        // Header block
        ws.Cell(1, 1).Value = "Partner";     ws.Cell(1, 2).Value = report.PartnerName;
        ws.Cell(2, 1).Value = "Email";       ws.Cell(2, 2).Value = report.PartnerEmail ?? "—";
        ws.Cell(3, 1).Value = "Período";     ws.Cell(3, 2).Value = FormatDateRange(report.DateFrom, report.DateTo);
        ws.Cell(4, 1).Value = "Proyectos";   ws.Cell(4, 2).Value = report.Projects.Count;
        ws.Cell(5, 1).Value = "Métodos Pago"; ws.Cell(5, 2).Value = report.PaymentMethods.Count;
        ws.Cell(6, 1).Value = "Generado";    ws.Cell(6, 2).Value = report.GeneratedAt.ToString("yyyy-MM-dd HH:mm UTC");

        StyleHeaderRange(ws.Range(1, 1, 6, 1));

        if (report.Projects.Count == 0)
        {
            ws.Cell(9, 1).Value = "Sin actividad en el período seleccionado.";
            ws.Cell(9, 1).Style.Font.Italic = true;
            return;
        }

        // Check if all projects share the same currency (for totals row)
        var currencies = report.Projects.Select(p => p.CurrencyCode).Distinct().ToList();
        var singleCurrency = currencies.Count == 1 ? currencies[0] : null;

        const int headerRow = 9;
        string[] headers =
        [
            "Proyecto", "Moneda", "Pagó Físicamente", "Otros le Deben",
            "Él Debe", "Stl. Pagados", "Stl. Recibidos", "Balance Neto"
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
            ws.Cell(row, 1).Value = "TOTAL";
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
            ws.Cell(row + 1, 1).Value = "Nota: los proyectos tienen monedas base distintas — no se muestran totales globales.";
            ws.Cell(row + 1, 1).Style.Font.Italic = true;
            ws.Cell(row + 1, 1).Style.Font.FontColor = XLColor.DarkOrange;
            ws.Range(row + 1, 1, row + 1, headers.Length).Merge();
        }

        FinalizeSheetLayout(ws, headerRow, row, headers.Length, headerRow);
    }

    // ── Hojas por proyecto ──────────────────────────────────

    /// <summary>Adds a dedicated worksheet for a specific project's partner activity.</summary>
    private static void AddPartnerProjectSheet(
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
        ws.Cell(1, 1).Value = "Proyecto";  ws.Cell(1, 2).Value = project.ProjectName;
        ws.Cell(2, 1).Value = "Moneda";    ws.Cell(2, 2).Value = project.CurrencyCode;
        ws.Cell(3, 1).Value = "Período";   ws.Cell(3, 2).Value = "Según filtros del reporte";
        ws.Cell(4, 1).Value = "Partner";   ws.Cell(4, 2).Value = partnerName;
        StyleHeaderRange(ws.Range(1, 1, 4, 1));

        // ── Block 2: Balance summary (rows 6-13) ─────────────
        var balanceRow = 6;
        ws.Cell(balanceRow, 1).Value = "Resumen de Balance";
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
        WriteBalanceLine(balanceRow, $"Moneda base: {project.CurrencyCode}", 0);
        WriteBalanceLine(balanceRow + 1, "  Pagó Físicamente", project.PaidPhysically);
        WriteBalanceLine(balanceRow + 2, "  Otros le Deben", project.OthersOweHim);
        WriteBalanceLine(balanceRow + 3, "  Él Debe a Otros", project.HeOwesOthers);
        WriteBalanceLine(balanceRow + 4, "  Settlements Pagados", project.SettlementsPaid);
        WriteBalanceLine(balanceRow + 5, "  Settlements Recibidos", project.SettlementsReceived);
        WriteBalanceLine(balanceRow + 6, "  Balance Neto", project.NetBalance, highlight: true);

        var nextRow = balanceRow + 8;

        // Currency totals sub-block
        if (project.CurrencyTotals.Count > 0)
        {
            ws.Cell(nextRow, 1).Value = "Totales en Monedas Alternativas";
            ws.Cell(nextRow, 1).Style.Font.Bold = true;
            ws.Cell(nextRow, 1).Style.Font.FontColor = XLColor.DarkBlue;
            nextRow++;

            string[] altHeaders = ["Moneda", "Otros le Deben", "Él Debe", "Stl. Pagados", "Stl. Recibidos", "Balance Neto"];
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
            ws.Cell(nextRow, 1).Value = $"Transacciones — {project.CurrencyCode} (moneda base)";
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
                "Fecha", "Tipo", "Título", "Categoría", "Método de Pago", "Paga",
                $"Monto Split ({project.CurrencyCode})", "Tipo Split", "Valor Split"
            };
            // Add one column per alternative currency (informative only)
            txHeaders.AddRange(altCurrencies.Select(c => $"{c} (ref)"));

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
            ws.Cell(nextRow, 1).Value = "Settlements";
            ws.Cell(nextRow, 1).Style.Font.Bold = true;
            ws.Cell(nextRow, 1).Style.Font.FontColor = XLColor.DarkBlue;
            nextRow++;

            string[] stlHeaders =
            [
                "Fecha", "Dirección", "Contraparte", "Monto Original", "Moneda",
                $"Monto Convertido ({project.CurrencyCode})"
            ];

            for (var c = 0; c < stlHeaders.Length; c++)
                ws.Cell(nextRow, c + 1).Value = stlHeaders[c];

            var stlHeaderRow = nextRow;
            StyleTableHeader(ws.Range(stlHeaderRow, 1, stlHeaderRow, stlHeaders.Length));
            nextRow++;

            foreach (var s in project.Settlements)
            {
                ws.Cell(nextRow, 1).Value = s.Date.ToString("yyyy-MM-dd");
                ws.Cell(nextRow, 2).Value = s.Direction == "paid_to" ? "Pagó a" : "Recibió de";
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
    private static void AddPartnerPaymentMethodsSheet(
        XLWorkbook workbook, PartnerGeneralReportResponse report)
    {
        var ws = workbook.Worksheets.Add("Métodos de Pago");

        ws.Cell(1, 1).Value = "Nota: los montos están en la moneda nativa de cada método de pago.";
        ws.Cell(1, 1).Style.Font.Italic = true;
        ws.Cell(1, 1).Style.Font.FontColor = XLColor.DarkOrange;
        ws.Range(1, 1, 1, 7).Merge();

        string[] headers =
        [
            "Método de Pago", "Moneda", "Banco",
            "Total Gastos", "Total Ingresos", "Flujo Neto", "# Transacciones"
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
