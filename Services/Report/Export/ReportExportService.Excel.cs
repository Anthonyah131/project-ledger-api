using ClosedXML.Excel;
using ProjectLedger.API.DTOs.Report;

namespace ProjectLedger.API.Services.Report;

/// <summary>
/// Generación de reportes en formato Excel (.xlsx) usando ClosedXML.
/// </summary>
public partial class ReportExportService
{
    // ════════════════════════════════════════════════════════
    //  EXPENSE REPORT — EXCEL
    // ════════════════════════════════════════════════════════

    public byte[] GenerateExpenseReportExcel(DetailedExpenseReportResponse report)
    {
        using var workbook = new XLWorkbook();
        ApplyWorkbookDefaults(workbook, "Reporte de Gastos", "Reporte financiero detallado de gastos por proyecto.");

        AddExpenseSheet(workbook, report);

        if (report.CategoryAnalysis is { Count: > 0 })
            AddCategoryAnalysisSheet(workbook, report);

        if (report.PaymentMethodAnalysis is { Count: > 0 })
            AddExpensePaymentMethodAnalysisSheet(workbook, report);

        if (report.ObligationSummary is not null)
            AddObligationsSheet(workbook, report);

        return WorkbookToBytes(workbook);
    }

    private static void AddExpenseSheet(XLWorkbook workbook, DetailedExpenseReportResponse report)
    {
        var ws = workbook.Worksheets.Add("Gastos");

        // ── Bloque de resumen (columnas A-B) ─────────────────
        ws.Cell(1,  1).Value = "Proyecto";           ws.Cell(1,  2).Value = report.ProjectName;
        ws.Cell(2,  1).Value = "Moneda";             ws.Cell(2,  2).Value = report.CurrencyCode;
        ws.Cell(3,  1).Value = "Período";            ws.Cell(3,  2).Value = FormatDateRange(report.DateFrom, report.DateTo);
        ws.Cell(4,  1).Value = "Total Gastado";      ws.Cell(4,  2).Value = report.TotalSpent;
        ws.Cell(4,  2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(5,  1).Value = "Total Ingresos";     ws.Cell(5,  2).Value = report.TotalIncome;
        ws.Cell(5,  2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(6,  1).Value = "Balance Neto";       ws.Cell(6,  2).Value = report.NetBalance;
        ws.Cell(6,  2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(7,  1).Value = "# Gastos";           ws.Cell(7,  2).Value = report.TotalExpenseCount;
        ws.Cell(8,  1).Value = "# Ingresos";         ws.Cell(8,  2).Value = report.TotalIncomeCount;
        ws.Cell(9,  1).Value = "Promedio Gasto";     ws.Cell(9,  2).Value = report.AverageExpenseAmount;
        ws.Cell(9,  2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(10, 1).Value = "Promedio Mensual";   ws.Cell(10, 2).Value = report.AverageMonthlySpend;
        ws.Cell(10, 2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(11, 1).Value = "Generado";           ws.Cell(11, 2).Value = report.GeneratedAt.ToString("yyyy-MM-dd HH:mm UTC");

        StyleHeaderRange(ws.Range(1, 1, 11, 1));

        // ── Bloque de insights (columnas D-E) ─────────────────
        var peakLabel = report.PeakMonth is not null
            ? $"{report.PeakMonth.MonthLabel} ({report.PeakMonth.Total:N2})"
            : GetPeakExpenseMonthLabel(report);

        ws.Cell(1, 4).Value = "Mes Mayor Gasto";        ws.Cell(1, 5).Value = peakLabel;
        ws.Cell(2, 4).Value = "Top Categoría";          ws.Cell(2, 5).Value = GetTopExpenseCategoryLabel(report);
        ws.Cell(3, 4).Value = "Obligaciones Vencidas";  ws.Cell(3, 5).Value = report.ObligationSummary?.OverdueCount ?? 0;
        ws.Cell(4, 4).Value = "Monto Vencido";          ws.Cell(4, 5).Value = report.ObligationSummary?.OverdueAmount ?? 0m;
        ws.Cell(4, 5).Style.NumberFormat.Format = ExcelCurrencyFormat;

        if (report.LargestExpense is not null)
        {
            ws.Cell(5, 4).Value = "Mayor Gasto";
            ws.Cell(5, 5).Value = $"{report.LargestExpense.Title} ({report.LargestExpense.Amount:N2})";
            ws.Cell(6, 4).Value = "Fecha Mayor Gasto";
            ws.Cell(6, 5).Value = report.LargestExpense.ExpenseDate.ToString("yyyy-MM-dd");
        }

        StyleHeaderRange(ws.Range(1, 4, 6, 4));

        // ── Bloque de monedas alternativas (columnas G-J) ───
        var altCurrencies = report.AlternativeCurrencies ?? [];
        if (altCurrencies.Count > 0)
        {
            var altRow = 1;
            ws.Cell(altRow, 7).Value = "Moneda Alternativa";
            ws.Cell(altRow, 8).Value = "Total Gastado";
            ws.Cell(altRow, 9).Value = "Total Ingresos";
            ws.Cell(altRow, 10).Value = "Balance Neto";
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

        // ── Tabla de gastos ───────────────────────────────────
        // Build dynamic headers: base columns + one column per alternative currency
        var baseHeaders = new List<string>
        {
            "Fecha", "Título", "Categoría", "Método de Pago", "Tipo",
            "Monto Original", "Moneda Orig.", "Tasa Cambio", "Monto Convertido",
            "Monto Cuenta", "Moneda Cuenta",
            "Descripción", "Nro. Recibo", "Notas", "Pago Obligación", "Obligación"
        };
        var altCodes = altCurrencies.Select(a => a.CurrencyCode).ToList();
        foreach (var code in altCodes)
            baseHeaders.Add($"Monto {code}");
        var headers = baseHeaders.ToArray();

        const int tableStartRow = 13;
        for (var col = 1; col <= headers.Length; col++)
            ws.Cell(tableStartRow, col).Value = headers[col - 1];
        StyleTableHeader(ws.Range(tableStartRow, 1, tableStartRow, headers.Length));

        var row = tableStartRow + 1;

        foreach (var section in report.Sections)
        {
            // ── Fila de sección mensual (enriquecida) ─────────
            var sectionLabel = $"── {section.MonthLabel}  |  {section.SectionCount} gastos  |  {section.PercentageOfTotal:N1}% del total  |  Prom: {section.AverageExpenseAmount:N2}";
            if (section.TopExpense is not null)
                sectionLabel += $"  |  Mayor: {section.TopExpense.Title} ({section.TopExpense.Amount:N2})";

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
                ws.Cell(row, 15).Value = exp.IsObligationPayment ? "Sí" : "No";
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

    private static void WriteSectionTotals(IXLWorksheet ws, int startRow, MonthlyExpenseSection section, List<string> altCodes)
    {
        void TotalRow(int offset, string label, decimal value)
        {
            ws.Cell(startRow + offset, 8).Value = label;
            ws.Cell(startRow + offset, 8).Style.Font.Bold = true;
            ws.Cell(startRow + offset, 9).Value = value;
            ws.Cell(startRow + offset, 9).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(startRow + offset, 9).Style.Font.Bold = true;
        }

        TotalRow(0, "Subtotal:",     section.SectionTotal);
        TotalRow(1, "Ingresos:",     section.SectionIncomeTotal);
        TotalRow(2, "Balance Neto:", section.SectionNetBalance);

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

    private static void AddCategoryAnalysisSheet(XLWorkbook workbook, DetailedExpenseReportResponse report)
    {
        var ws = workbook.Worksheets.Add("Categorías");

        var headers = new[]
        {
            "Categoría", "Es Default", "Presupuesto", "Gastado",
            "# Gastos", "% del Total", "Restante", "% Usado", "Excedido"
        };

        for (var col = 1; col <= headers.Length; col++)
            ws.Cell(1, col).Value = headers[col - 1];
        StyleTableHeader(ws.Range(1, 1, 1, headers.Length));

        var row = 2;
        foreach (var cat in report.CategoryAnalysis!)
        {
            ws.Cell(row, 1).Value = cat.CategoryName;
            ws.Cell(row, 2).Value = cat.IsDefault ? "Sí" : "No";
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
            ws.Cell(row, 9).Value = cat.BudgetExceeded == true ? "⚠ Sí" : "No";

            if (cat.BudgetExceeded == true)
                ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.LightCoral;

            row++;
        }

        // Fila de totales
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

    private static void AddExpensePaymentMethodAnalysisSheet(XLWorkbook workbook, DetailedExpenseReportResponse report)
    {
        var ws = workbook.Worksheets.Add("Por Método de Pago");

        var headers = new[]
        {
            "Método de Pago", "Tipo", "Total Gastado", "# Gastos", "% del Total", "Promedio Gasto"
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

    private static void AddObligationsSheet(XLWorkbook workbook, DetailedExpenseReportResponse report)
    {
        var ws  = workbook.Worksheets.Add("Obligaciones");
        var obl = report.ObligationSummary!;

        // ── Resumen ───────────────────────────────────────────
        ws.Cell(1, 1).Value = "Total Obligaciones"; ws.Cell(1, 2).Value = obl.TotalObligations;
        ws.Cell(2, 1).Value = "Monto Total";        ws.Cell(2, 2).Value = obl.TotalAmount;
        ws.Cell(2, 2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(3, 1).Value = "Total Pagado";       ws.Cell(3, 2).Value = obl.TotalPaid;
        ws.Cell(3, 2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(4, 1).Value = "Total Pendiente";    ws.Cell(4, 2).Value = obl.TotalPending;
        ws.Cell(4, 2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(5, 1).Value = "Vencidas";           ws.Cell(5, 2).Value = obl.OverdueCount;
        ws.Cell(6, 1).Value = "Monto Vencido";      ws.Cell(6, 2).Value = obl.OverdueAmount;
        ws.Cell(6, 2).Style.NumberFormat.Format = ExcelCurrencyFormat;

        StyleHeaderRange(ws.Range(1, 1, 6, 1));

        // ── Tabla ─────────────────────────────────────────────
        var headers = new[]
        {
            "Estado", "Título", "Descripción", "Monto Total",
            "Pagado", "Restante", "% Pagado", "Moneda", "Fecha Vencimiento",
            "# Pagos", "Último Pago"
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
