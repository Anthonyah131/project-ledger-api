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

        if (report.PartnerSummary is not null)
            AddPartnerExpenseSummarySheet(workbook, report);

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

        // ── Tabla de gastos ───────────────────────────────────
        var headers = new[]
        {
            "Fecha", "Título", "Categoría", "Método de Pago", "Tipo",
            "Monto Original", "Moneda Orig.", "Tasa Cambio", "Monto Convertido",
            "Monto Cuenta", "Moneda Cuenta",
            "Descripción", "Nro. Recibo", "Notas", "Pago Obligación", "Obligación"
        };

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
                row++;
            }

            WriteSectionTotals(ws, row, section);
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

    private static void WriteSectionTotals(IXLWorksheet ws, int startRow, dynamic section)
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

    private static void AddPartnerExpenseSummarySheet(XLWorkbook workbook, DetailedExpenseReportResponse report)
    {
        var ws = workbook.Worksheets.Add("Resumen Partners");

        var headers = new[] { "Partner", "Total Splits Gastos", "# Gastos", "% del Total" };

        for (var col = 1; col <= headers.Length; col++)
            ws.Cell(1, col).Value = headers[col - 1];
        StyleTableHeader(ws.Range(1, 1, 1, headers.Length));

        var row = 2;
        foreach (var p in report.PartnerSummary!.Partners)
        {
            ws.Cell(row, 1).Value = p.PartnerName;
            ws.Cell(row, 2).Value = p.TotalSplitAmount;
            ws.Cell(row, 2).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 3).Value = p.ExpenseCount;
            ws.Cell(row, 4).Value = p.Percentage;
            ws.Cell(row, 4).Style.NumberFormat.Format = ExcelPercentFormat;
            row++;
        }

        // Totals row
        ws.Cell(row, 2).Value = report.PartnerSummary.Partners.Sum(p => p.TotalSplitAmount);
        ws.Cell(row, 2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(row, 2).Style.Font.Bold = true;
        ws.Cell(row, 3).Value = report.PartnerSummary.Partners.Sum(p => p.ExpenseCount);
        ws.Cell(row, 3).Style.Font.Bold = true;
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

    // ════════════════════════════════════════════════════════
    //  PAYMENT METHOD REPORT — EXCEL
    // ════════════════════════════════════════════════════════

    public byte[] GeneratePaymentMethodReportExcel(PaymentMethodReportResponse report)
    {
        using var workbook = new XLWorkbook();
        ApplyWorkbookDefaults(workbook, "Reporte de Metodos de Pago", "Reporte de gastos por metodos de pago del usuario.");

        AddPaymentMethodSummarySheet(workbook, report);
        AddPaymentMethodByProjectSheet(workbook, report);
        AddPaymentMethodExpensesSheet(workbook, report);
        AddPaymentMethodIncomesSheet(workbook, report);
        AddMonthlyTrendSheet(workbook, report);

        var hasMethodBreakdown = report.MonthlyTrend.Any(m => m.ByMethod.Count > 0);
        if (hasMethodBreakdown)
            AddMonthlyByMethodSheet(workbook, report);

        var hasTopCategories = report.PaymentMethods.Any(pm => pm.TopCategories.Count > 0);
        if (hasTopCategories)
            AddTopCategoriesSheet(workbook, report);

        return WorkbookToBytes(workbook);
    }

    private static void AddPaymentMethodSummarySheet(XLWorkbook workbook, PaymentMethodReportResponse report)
    {
        var ws = workbook.Worksheets.Add("Métodos de Pago");

        // ── Resumen ───────────────────────────────────────────
        ws.Cell(1,  1).Value = "Período";             ws.Cell(1,  2).Value = FormatDateRange(report.DateFrom, report.DateTo);
        ws.Cell(2,  1).Value = "Total Gastado";       ws.Cell(2,  2).Value = report.GrandTotalSpent;
        ws.Cell(2,  2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(3,  1).Value = "Total Ingresos";      ws.Cell(3,  2).Value = report.GrandTotalIncome;
        ws.Cell(3,  2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(4,  1).Value = "Balance Neto";        ws.Cell(4,  2).Value = report.GrandNetFlow;
        ws.Cell(4,  2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(5,  1).Value = "# Gastos";            ws.Cell(5,  2).Value = report.GrandTotalExpenseCount;
        ws.Cell(6,  1).Value = "# Ingresos";          ws.Cell(6,  2).Value = report.GrandTotalIncomeCount;
        ws.Cell(7,  1).Value = "Prom. Gasto/Trans.";  ws.Cell(7,  2).Value = report.GrandAverageExpenseAmount;
        ws.Cell(7,  2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(8,  1).Value = "Prom. Ingreso/Trans."; ws.Cell(8,  2).Value = report.GrandAverageIncomeAmount;
        ws.Cell(8,  2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(9,  1).Value = "Prom. Mensual";       ws.Cell(9,  2).Value = report.AverageMonthlySpend;
        ws.Cell(9,  2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(10, 1).Value = "Generado";            ws.Cell(10, 2).Value = report.GeneratedAt.ToString("yyyy-MM-dd HH:mm UTC");

        StyleHeaderRange(ws.Range(1, 1, 10, 1));

        // ── Insights ──────────────────────────────────────────
        ws.Cell(1, 4).Value = "Método Mayor Gasto";
        ws.Cell(1, 5).Value = report.HighestSpendMethod?.Name ?? GetTopPaymentMethodLabel(report);
        ws.Cell(2, 4).Value = "Método Más Usado";
        ws.Cell(2, 5).Value = report.MostUsedMethod?.Name
            ?? (report.PaymentMethods.OrderByDescending(pm => pm.ExpenseCount).FirstOrDefault()?.Name ?? "—");
        ws.Cell(3, 4).Value = "Mes Pico";
        ws.Cell(3, 5).Value = report.PeakMonth is not null
            ? $"{report.PeakMonth.MonthLabel} ({report.PeakMonth.Total:N2})"
            : GetPeakTrendMonthLabel(report);
        ws.Cell(4, 4).Value = "Mayor Gasto Individual";
        var topExpense = report.PaymentMethods
            .Where(pm => pm.TopExpense is not null)
            .OrderByDescending(pm => pm.TopExpense!.Amount)
            .FirstOrDefault()?.TopExpense;
        ws.Cell(4, 5).Value = topExpense is not null
            ? $"{topExpense.Title} ({topExpense.Amount:N2})"
            : "—";
        ws.Cell(5, 4).Value = "Métodos Activos";
        ws.Cell(5, 5).Value = report.PaymentMethods.Count(pm => !pm.IsInactive);

        StyleHeaderRange(ws.Range(1, 4, 5, 4));

        // ── Tabla ─────────────────────────────────────────────
        var headers = new[]
        {
            "Método de Pago", "Tipo", "Moneda", "Banco", "Partner Dueño",
            "Total Gastado", "# Gastos", "Total Ingresos", "# Ingresos",
            "Balance Neto", "% del Gasto", "Promedio Gasto", "Promedio Ingreso",
            "Primer Uso", "Último Uso", "Días sin Uso", "Inactivo"
        };

        const int tableStartRow = 12;
        for (var col = 1; col <= headers.Length; col++)
            ws.Cell(tableStartRow, col).Value = headers[col - 1];
        StyleTableHeader(ws.Range(tableStartRow, 1, tableStartRow, headers.Length));

        var row = tableStartRow + 1;
        foreach (var pm in report.PaymentMethods)
        {
            ws.Cell(row, 1).Value  = pm.Name;
            ws.Cell(row, 2).Value  = pm.Type;
            ws.Cell(row, 3).Value  = pm.Currency;
            ws.Cell(row, 4).Value  = pm.BankName ?? "—";
            ws.Cell(row, 5).Value  = pm.OwnerPartnerName ?? "—";
            ws.Cell(row, 6).Value  = pm.TotalSpent;
            ws.Cell(row, 6).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 7).Value  = pm.ExpenseCount;
            ws.Cell(row, 8).Value  = pm.TotalIncome;
            ws.Cell(row, 8).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 9).Value  = pm.IncomeCount;
            ws.Cell(row, 10).Value = pm.NetFlow;
            ws.Cell(row, 10).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 11).Value = pm.Percentage;
            ws.Cell(row, 11).Style.NumberFormat.Format = ExcelPercentFormat;
            ws.Cell(row, 12).Value = pm.AverageExpenseAmount;
            ws.Cell(row, 12).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 13).Value = pm.AverageIncomeAmount;
            ws.Cell(row, 13).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 14).Value = pm.FirstUseDate?.ToString("yyyy-MM-dd") ?? "—";
            ws.Cell(row, 15).Value = pm.LastUseDate?.ToString("yyyy-MM-dd") ?? "—";
            ws.Cell(row, 16).Value = pm.DaysSinceLastUse;
            ws.Cell(row, 17).Value = pm.IsInactive ? "Sí" : "No";

            if (pm.IsInactive)
                ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.LightYellow;

            row++;
        }

        // Totals row
        ws.Cell(row, 6).Value = report.GrandTotalSpent;
        ws.Cell(row, 6).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(row, 6).Style.Font.Bold = true;
        ws.Cell(row, 7).Value = report.GrandTotalExpenseCount;
        ws.Cell(row, 7).Style.Font.Bold = true;
        ws.Cell(row, 8).Value = report.GrandTotalIncome;
        ws.Cell(row, 8).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(row, 8).Style.Font.Bold = true;
        ws.Cell(row, 9).Value = report.GrandTotalIncomeCount;
        ws.Cell(row, 9).Style.Font.Bold = true;
        ws.Cell(row, 10).Value = report.GrandNetFlow;
        ws.Cell(row, 10).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(row, 10).Style.Font.Bold = true;
        ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.LightGray;
        ws.Range(row, 1, row, headers.Length).Style.Border.TopBorder = XLBorderStyleValues.Thin;

        FinalizeSheetLayout(
            ws,
            headerRow: tableStartRow,
            lastRow:   Math.Max(tableStartRow, row),
            lastColumn: headers.Length,
            freezeRows: tableStartRow,
            maxColumnWidth: 36,
            wrapColumns: [1, 4, 5]);
    }

    private static void AddPaymentMethodByProjectSheet(XLWorkbook workbook, PaymentMethodReportResponse report)
    {
        var ws = workbook.Worksheets.Add("Por Proyecto");

        var headers = new[] { "Método de Pago", "Proyecto", "Moneda Proyecto", "Total Gastado", "# Gastos", "% del Método" };
        for (var col = 1; col <= headers.Length; col++)
            ws.Cell(1, col).Value = headers[col - 1];
        StyleTableHeader(ws.Range(1, 1, 1, headers.Length));

        var row = 2;
        foreach (var pm in report.PaymentMethods)
        {
            foreach (var proj in pm.Projects)
            {
                ws.Cell(row, 1).Value = pm.Name;
                ws.Cell(row, 2).Value = proj.ProjectName;
                ws.Cell(row, 3).Value = proj.ProjectCurrency;
                ws.Cell(row, 4).Value = proj.TotalSpent;
                ws.Cell(row, 4).Style.NumberFormat.Format = ExcelCurrencyFormat;
                ws.Cell(row, 5).Value = proj.ExpenseCount;
                ws.Cell(row, 6).Value = proj.Percentage;
                ws.Cell(row, 6).Style.NumberFormat.Format = ExcelPercentFormat;
                row++;
            }
        }

        FinalizeSheetLayout(ws, 1, Math.Max(1, row - 1), headers.Length, 1, maxColumnWidth: 36, wrapColumns: [1, 2]);
    }

    private static void AddPaymentMethodExpensesSheet(XLWorkbook workbook, PaymentMethodReportResponse report)
    {
        var ws = workbook.Worksheets.Add("Gastos");

        var headers = new[]
        {
            "Fecha", "Título", "Método de Pago", "Proyecto",
            "Categoría", "Monto Original", "Mon. Original",
            "Monto Cuenta", "Mon. Cuenta",
            "Monto Convertido", "Mon. Proyecto", "Descripción"
        };
        for (var col = 1; col <= headers.Length; col++)
            ws.Cell(1, col).Value = headers[col - 1];
        StyleTableHeader(ws.Range(1, 1, 1, headers.Length));

        var row = 2;
        foreach (var pm in report.PaymentMethods)
        {
            foreach (var exp in pm.Expenses)
            {
                ws.Cell(row, 1).Value  = exp.ExpenseDate.ToString("yyyy-MM-dd");
                ws.Cell(row, 2).Value  = exp.Title;
                ws.Cell(row, 3).Value  = pm.Name;
                ws.Cell(row, 4).Value  = exp.ProjectName;
                ws.Cell(row, 5).Value  = exp.CategoryName;
                ws.Cell(row, 6).Value  = exp.OriginalAmount;
                ws.Cell(row, 6).Style.NumberFormat.Format = ExcelCurrencyFormat;
                ws.Cell(row, 7).Value  = exp.OriginalCurrency;

                if (exp.AccountAmount.HasValue)
                {
                    ws.Cell(row, 8).Value = exp.AccountAmount.Value;
                    ws.Cell(row, 8).Style.NumberFormat.Format = ExcelCurrencyFormat;
                }
                else
                {
                    ws.Cell(row, 8).Value = "—";
                }
                ws.Cell(row, 9).Value  = exp.AccountCurrency ?? "—";
                ws.Cell(row, 10).Value = exp.ConvertedAmount;
                ws.Cell(row, 10).Style.NumberFormat.Format = ExcelCurrencyFormat;
                ws.Cell(row, 11).Value = exp.ProjectCurrency;
                ws.Cell(row, 12).Value = exp.Description ?? "";
                row++;
            }
        }

        FinalizeSheetLayout(ws, 1, Math.Max(1, row - 1), headers.Length, 1, maxColumnWidth: 42, wrapColumns: [2, 4, 12]);
    }

    private static void AddPaymentMethodIncomesSheet(XLWorkbook workbook, PaymentMethodReportResponse report)
    {
        var ws = workbook.Worksheets.Add("Ingresos");

        var headers = new[]
        {
            "Fecha", "Título", "Método de Pago", "Proyecto",
            "Categoría", "Monto Original", "Mon. Original",
            "Monto Cuenta", "Mon. Cuenta",
            "Monto Convertido", "Mon. Proyecto", "Descripción"
        };
        for (var col = 1; col <= headers.Length; col++)
            ws.Cell(1, col).Value = headers[col - 1];
        StyleTableHeader(ws.Range(1, 1, 1, headers.Length));

        var row = 2;
        foreach (var pm in report.PaymentMethods)
        {
            foreach (var inc in pm.Incomes)
            {
                ws.Cell(row, 1).Value  = inc.IncomeDate.ToString("yyyy-MM-dd");
                ws.Cell(row, 2).Value  = inc.Title;
                ws.Cell(row, 3).Value  = pm.Name;
                ws.Cell(row, 4).Value  = inc.ProjectName;
                ws.Cell(row, 5).Value  = inc.CategoryName;
                ws.Cell(row, 6).Value  = inc.OriginalAmount;
                ws.Cell(row, 6).Style.NumberFormat.Format = ExcelCurrencyFormat;
                ws.Cell(row, 7).Value  = inc.OriginalCurrency;

                if (inc.AccountAmount.HasValue)
                {
                    ws.Cell(row, 8).Value = inc.AccountAmount.Value;
                    ws.Cell(row, 8).Style.NumberFormat.Format = ExcelCurrencyFormat;
                }
                else
                {
                    ws.Cell(row, 8).Value = "—";
                }
                ws.Cell(row, 9).Value  = inc.AccountCurrency ?? "—";
                ws.Cell(row, 10).Value = inc.ConvertedAmount;
                ws.Cell(row, 10).Style.NumberFormat.Format = ExcelCurrencyFormat;
                ws.Cell(row, 11).Value = inc.ProjectCurrency;
                ws.Cell(row, 12).Value = inc.Description ?? "";
                row++;
            }
        }

        FinalizeSheetLayout(ws, 1, Math.Max(1, row - 1), headers.Length, 1, maxColumnWidth: 42, wrapColumns: [2, 4, 12]);
    }

    private static void AddMonthlyTrendSheet(XLWorkbook workbook, PaymentMethodReportResponse report)
    {
        var ws = workbook.Worksheets.Add("Tendencia Mensual");

        var headers = new[] { "Mes", "Total Gastado", "# Gastos", "Total Ingresos", "# Ingresos", "Balance Neto" };
        for (var col = 1; col <= headers.Length; col++)
            ws.Cell(1, col).Value = headers[col - 1];
        StyleTableHeader(ws.Range(1, 1, 1, headers.Length));

        var row = 2;
        foreach (var m in report.MonthlyTrend)
        {
            ws.Cell(row, 1).Value = m.MonthLabel;
            ws.Cell(row, 2).Value = m.TotalSpent;
            ws.Cell(row, 2).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 3).Value = m.ExpenseCount;
            ws.Cell(row, 4).Value = m.TotalIncome;
            ws.Cell(row, 4).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 5).Value = m.IncomeCount;
            ws.Cell(row, 6).Value = m.NetBalance;
            ws.Cell(row, 6).Style.NumberFormat.Format = ExcelCurrencyFormat;
            row++;
        }

        // Totals
        ws.Cell(row, 2).Value = report.GrandTotalSpent;
        ws.Cell(row, 2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(row, 2).Style.Font.Bold = true;
        ws.Cell(row, 3).Value = report.GrandTotalExpenseCount;
        ws.Cell(row, 3).Style.Font.Bold = true;
        ws.Cell(row, 4).Value = report.GrandTotalIncome;
        ws.Cell(row, 4).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(row, 4).Style.Font.Bold = true;
        ws.Cell(row, 5).Value = report.GrandTotalIncomeCount;
        ws.Cell(row, 5).Style.Font.Bold = true;
        ws.Cell(row, 6).Value = report.GrandNetFlow;
        ws.Cell(row, 6).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(row, 6).Style.Font.Bold = true;
        ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.LightGray;
        ws.Range(row, 1, row, headers.Length).Style.Border.TopBorder = XLBorderStyleValues.Thin;

        FinalizeSheetLayout(ws, 1, Math.Max(1, row), headers.Length, 1, maxColumnWidth: 28, wrapColumns: []);
    }

    private static void AddMonthlyByMethodSheet(XLWorkbook workbook, PaymentMethodReportResponse report)
    {
        var ws = workbook.Worksheets.Add("Tendencia por Método");

        var headers = new[]
        {
            "Mes", "Método de Pago", "Total Gastado", "# Gastos",
            "Total Ingresos", "# Ingresos", "Balance", "% del Mes"
        };
        for (var col = 1; col <= headers.Length; col++)
            ws.Cell(1, col).Value = headers[col - 1];
        StyleTableHeader(ws.Range(1, 1, 1, headers.Length));

        var row = 2;
        foreach (var m in report.MonthlyTrend)
        {
            foreach (var bm in m.ByMethod)
            {
                ws.Cell(row, 1).Value = m.MonthLabel;
                ws.Cell(row, 2).Value = bm.Name;
                ws.Cell(row, 3).Value = bm.TotalSpent;
                ws.Cell(row, 3).Style.NumberFormat.Format = ExcelCurrencyFormat;
                ws.Cell(row, 4).Value = bm.ExpenseCount;
                ws.Cell(row, 5).Value = bm.TotalIncome;
                ws.Cell(row, 5).Style.NumberFormat.Format = ExcelCurrencyFormat;
                ws.Cell(row, 6).Value = bm.IncomeCount;
                ws.Cell(row, 7).Value = bm.NetFlow;
                ws.Cell(row, 7).Style.NumberFormat.Format = ExcelCurrencyFormat;
                ws.Cell(row, 8).Value = bm.Percentage;
                ws.Cell(row, 8).Style.NumberFormat.Format = ExcelPercentFormat;
                row++;
            }
        }

        FinalizeSheetLayout(ws, 1, Math.Max(1, row - 1), headers.Length, 1, maxColumnWidth: 36, wrapColumns: [1, 2]);
    }

    private static void AddTopCategoriesSheet(XLWorkbook workbook, PaymentMethodReportResponse report)
    {
        var ws = workbook.Worksheets.Add("Top Categorías");

        var headers = new[] { "Método de Pago", "Categoría", "Total Gastado", "# Gastos", "% del Método" };
        for (var col = 1; col <= headers.Length; col++)
            ws.Cell(1, col).Value = headers[col - 1];
        StyleTableHeader(ws.Range(1, 1, 1, headers.Length));

        var row = 2;
        foreach (var pm in report.PaymentMethods)
        {
            foreach (var cat in pm.TopCategories)
            {
                ws.Cell(row, 1).Value = pm.Name;
                ws.Cell(row, 2).Value = cat.CategoryName;
                ws.Cell(row, 3).Value = cat.TotalAmount;
                ws.Cell(row, 3).Style.NumberFormat.Format = ExcelCurrencyFormat;
                ws.Cell(row, 4).Value = cat.ExpenseCount;
                ws.Cell(row, 5).Value = cat.Percentage;
                ws.Cell(row, 5).Style.NumberFormat.Format = ExcelPercentFormat;
                row++;
            }
        }

        FinalizeSheetLayout(ws, 1, Math.Max(1, row - 1), headers.Length, 1, maxColumnWidth: 36, wrapColumns: [1, 2]);
    }
}
