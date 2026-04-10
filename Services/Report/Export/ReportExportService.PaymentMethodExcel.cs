using ClosedXML.Excel;
using ProjectLedger.API.DTOs.Report;

namespace ProjectLedger.API.Services.Report;

public partial class ReportExportService
{
    // ════════════════════════════════════════════════════════
    //  PAYMENT METHOD REPORT — EXCEL
    // ════════════════════════════════════════════════════════

    /// <inheritdoc />
    public byte[] GeneratePaymentMethodReportExcel(PaymentMethodReportResponse report)
    {
        using var workbook = new XLWorkbook();
        ApplyWorkbookDefaults(workbook, "Reporte de Metodos de Pago", "Reporte por metodos de pago del usuario.");

        AddPaymentMethodSummarySheet(workbook, report);
        AddPaymentMethodByProjectSheet(workbook, report);
        AddPaymentMethodExpensesSheet(workbook, report);
        AddPaymentMethodIncomesSheet(workbook, report);
        AddMonthlyTrendSheet(workbook, report);

        var hasTopCategories = report.PaymentMethods.Any(pm => pm.TopCategories.Count > 0);
        if (hasTopCategories)
            AddTopCategoriesSheet(workbook, report);

        return WorkbookToBytes(workbook);
    }

    /// <summary>Adds the payment method summary worksheet.</summary>
    private static void AddPaymentMethodSummarySheet(XLWorkbook workbook, PaymentMethodReportResponse report)
    {
        var ws = workbook.Worksheets.Add("Métodos de Pago");

        // ── Encabezado ──────────────────────────────────────────
        ws.Cell(1, 1).Value = "Período";   ws.Cell(1, 2).Value = FormatDateRange(report.DateFrom, report.DateTo);
        ws.Cell(2, 1).Value = "Generado";  ws.Cell(2, 2).Value = report.GeneratedAt.ToString("yyyy-MM-dd HH:mm UTC");

        StyleHeaderRange(ws.Range(1, 1, 2, 1));

        // ── Tabla ─────────────────────────────────────────────
        var headers = new[]
        {
            "Método de Pago", "Tipo", "Moneda", "Banco", "Partner Dueño",
            "Total Gastado", "# Gastos", "Total Ingresos", "# Ingresos",
            "Balance Neto", "Promedio Gasto", "Promedio Ingreso",
            "Primer Uso", "Último Uso", "Días sin Uso", "Inactivo"
        };

        const int tableStartRow = 4;
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
            ws.Cell(row, 11).Value = pm.AverageExpenseAmount;
            ws.Cell(row, 11).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 12).Value = pm.AverageIncomeAmount;
            ws.Cell(row, 12).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 13).Value = pm.FirstUseDate?.ToString("yyyy-MM-dd") ?? "—";
            ws.Cell(row, 14).Value = pm.LastUseDate?.ToString("yyyy-MM-dd") ?? "—";
            ws.Cell(row, 15).Value = pm.DaysSinceLastUse;
            ws.Cell(row, 16).Value = pm.IsInactive ? "Sí" : "No";

            if (pm.IsInactive)
                ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.LightYellow;

            row++;
        }

        FinalizeSheetLayout(
            ws,
            headerRow: tableStartRow,
            lastRow:   Math.Max(tableStartRow, row - 1),
            lastColumn: headers.Length,
            freezeRows: tableStartRow,
            maxColumnWidth: 36,
            wrapColumns: [1, 4, 5]);
    }

    /// <summary>Adds a worksheet for payment method distribution by project.</summary>
    private static void AddPaymentMethodByProjectSheet(XLWorkbook workbook, PaymentMethodReportResponse report)
    {
        var ws = workbook.Worksheets.Add("Por Proyecto");

        var headers = new[] { "Método de Pago", "Moneda Método", "Proyecto", "Moneda Proyecto", "Total Gastado", "# Gastos", "% del Método" };
        for (var col = 1; col <= headers.Length; col++)
            ws.Cell(1, col).Value = headers[col - 1];
        StyleTableHeader(ws.Range(1, 1, 1, headers.Length));

        var row = 2;
        foreach (var pm in report.PaymentMethods)
        {
            foreach (var proj in pm.Projects)
            {
                ws.Cell(row, 1).Value = pm.Name;
                ws.Cell(row, 2).Value = pm.Currency;
                ws.Cell(row, 3).Value = proj.ProjectName;
                ws.Cell(row, 4).Value = proj.ProjectCurrency;
                ws.Cell(row, 5).Value = proj.TotalSpent;
                ws.Cell(row, 5).Style.NumberFormat.Format = ExcelCurrencyFormat;
                ws.Cell(row, 6).Value = proj.ExpenseCount;
                ws.Cell(row, 7).Value = proj.Percentage;
                ws.Cell(row, 7).Style.NumberFormat.Format = ExcelPercentFormat;
                row++;
            }
        }

        FinalizeSheetLayout(ws, 1, Math.Max(1, row - 1), headers.Length, 1, maxColumnWidth: 36, wrapColumns: [1, 3]);
    }

    /// <summary>Adds a worksheet for detailed expense listing related to payment methods.</summary>
    private static void AddPaymentMethodExpensesSheet(XLWorkbook workbook, PaymentMethodReportResponse report)
    {
        var ws = workbook.Worksheets.Add("Gastos");

        var headers = new[]
        {
            "Fecha", "Título", "Método de Pago", "Moneda",
            "Proyecto", "Categoría", "Monto", "Descripción"
        };
        for (var col = 1; col <= headers.Length; col++)
            ws.Cell(1, col).Value = headers[col - 1];
        StyleTableHeader(ws.Range(1, 1, 1, headers.Length));

        var row = 2;
        foreach (var pm in report.PaymentMethods)
        {
            foreach (var exp in pm.Expenses)
            {
                ws.Cell(row, 1).Value = exp.ExpenseDate.ToString("yyyy-MM-dd");
                ws.Cell(row, 2).Value = exp.Title;
                ws.Cell(row, 3).Value = pm.Name;
                ws.Cell(row, 4).Value = pm.Currency;
                ws.Cell(row, 5).Value = exp.ProjectName;
                ws.Cell(row, 6).Value = exp.CategoryName;
                ws.Cell(row, 7).Value = exp.Amount;
                ws.Cell(row, 7).Style.NumberFormat.Format = ExcelCurrencyFormat;
                ws.Cell(row, 8).Value = exp.Description ?? "";
                row++;
            }
        }

        FinalizeSheetLayout(ws, 1, Math.Max(1, row - 1), headers.Length, 1, maxColumnWidth: 42, wrapColumns: [2, 5, 8]);
    }

    /// <summary>Adds a worksheet for detailed income listing related to payment methods.</summary>
    private static void AddPaymentMethodIncomesSheet(XLWorkbook workbook, PaymentMethodReportResponse report)
    {
        var ws = workbook.Worksheets.Add("Ingresos");

        var headers = new[]
        {
            "Fecha", "Título", "Método de Pago", "Moneda",
            "Proyecto", "Categoría", "Monto", "Descripción"
        };
        for (var col = 1; col <= headers.Length; col++)
            ws.Cell(1, col).Value = headers[col - 1];
        StyleTableHeader(ws.Range(1, 1, 1, headers.Length));

        var row = 2;
        foreach (var pm in report.PaymentMethods)
        {
            foreach (var inc in pm.Incomes)
            {
                ws.Cell(row, 1).Value = inc.IncomeDate.ToString("yyyy-MM-dd");
                ws.Cell(row, 2).Value = inc.Title;
                ws.Cell(row, 3).Value = pm.Name;
                ws.Cell(row, 4).Value = pm.Currency;
                ws.Cell(row, 5).Value = inc.ProjectName;
                ws.Cell(row, 6).Value = inc.CategoryName;
                ws.Cell(row, 7).Value = inc.Amount;
                ws.Cell(row, 7).Style.NumberFormat.Format = ExcelCurrencyFormat;
                ws.Cell(row, 8).Value = inc.Description ?? "";
                row++;
            }
        }

        FinalizeSheetLayout(ws, 1, Math.Max(1, row - 1), headers.Length, 1, maxColumnWidth: 42, wrapColumns: [2, 5, 8]);
    }

    /// <summary>Adds a worksheet for monthly trend analysis of payment methods.</summary>
    private static void AddMonthlyTrendSheet(XLWorkbook workbook, PaymentMethodReportResponse report)
    {
        var ws = workbook.Worksheets.Add("Tendencia Mensual");

        var headers = new[] { "Mes", "Método de Pago", "Moneda", "Total Gastado", "# Gastos", "Total Ingresos", "# Ingresos", "Balance" };
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
                ws.Cell(row, 3).Value = bm.Currency;
                ws.Cell(row, 4).Value = bm.TotalSpent;
                ws.Cell(row, 4).Style.NumberFormat.Format = ExcelCurrencyFormat;
                ws.Cell(row, 5).Value = bm.ExpenseCount;
                ws.Cell(row, 6).Value = bm.TotalIncome;
                ws.Cell(row, 6).Style.NumberFormat.Format = ExcelCurrencyFormat;
                ws.Cell(row, 7).Value = bm.IncomeCount;
                ws.Cell(row, 8).Value = bm.NetFlow;
                ws.Cell(row, 8).Style.NumberFormat.Format = ExcelCurrencyFormat;
                row++;
            }
        }

        FinalizeSheetLayout(ws, 1, Math.Max(1, row - 1), headers.Length, 1, maxColumnWidth: 36, wrapColumns: [1, 2]);
    }

    /// <summary>Adds a worksheet for top category distribution per payment method.</summary>
    private static void AddTopCategoriesSheet(XLWorkbook workbook, PaymentMethodReportResponse report)
    {
        var ws = workbook.Worksheets.Add("Top Categorías");

        var headers = new[] { "Método de Pago", "Moneda", "Categoría", "Total Gastado", "# Gastos", "% del Método" };
        for (var col = 1; col <= headers.Length; col++)
            ws.Cell(1, col).Value = headers[col - 1];
        StyleTableHeader(ws.Range(1, 1, 1, headers.Length));

        var row = 2;
        foreach (var pm in report.PaymentMethods)
        {
            foreach (var cat in pm.TopCategories)
            {
                ws.Cell(row, 1).Value = pm.Name;
                ws.Cell(row, 2).Value = pm.Currency;
                ws.Cell(row, 3).Value = cat.CategoryName;
                ws.Cell(row, 4).Value = cat.TotalAmount;
                ws.Cell(row, 4).Style.NumberFormat.Format = ExcelCurrencyFormat;
                ws.Cell(row, 5).Value = cat.ExpenseCount;
                ws.Cell(row, 6).Value = cat.Percentage;
                ws.Cell(row, 6).Style.NumberFormat.Format = ExcelPercentFormat;
                row++;
            }
        }

        FinalizeSheetLayout(ws, 1, Math.Max(1, row - 1), headers.Length, 1, maxColumnWidth: 36, wrapColumns: [1, 3]);
    }
}
