using ClosedXML.Excel;
using ProjectLedger.API.DTOs.Report;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ProjectLedger.API.Services;

/// <summary>
/// Genera reportes en Excel (ClosedXML) y PDF (QuestPDF).
/// </summary>
public partial class ReportExportService : IReportExportService
{
    private const string ExcelFontName = "Arial";
    private const double ExcelFontSize = 10;
    private const string ExcelCurrencyFormat = "#,##0.00;(#,##0.00);-";
    private const string ExcelExchangeRateFormat = "#,##0.0000;(#,##0.0000);-";
    private const string ExcelPercentFormat = "0.0\"%\";(0.0\"%\");-";

    // ════════════════════════════════════════════════════════
    //  EXPENSE REPORT — EXCEL
    // ════════════════════════════════════════════════════════

    public byte[] GenerateExpenseReportExcel(DetailedExpenseReportResponse report)
    {
        using var workbook = new XLWorkbook();
        ApplyWorkbookDefaults(workbook, "Reporte de Gastos", "Reporte financiero detallado de gastos por proyecto.");

        // ── Hoja 1: Gastos detallados ───────────────────────
        var ws = workbook.Worksheets.Add("Gastos");

        // Encabezado del proyecto
        ws.Cell(1, 1).Value = "Proyecto";
        ws.Cell(1, 2).Value = report.ProjectName;
        ws.Cell(2, 1).Value = "Moneda";
        ws.Cell(2, 2).Value = report.CurrencyCode;
        ws.Cell(3, 1).Value = "Período";
        ws.Cell(3, 2).Value = FormatDateRange(report.DateFrom, report.DateTo);
        ws.Cell(4, 1).Value = "Total Gastado";
        ws.Cell(4, 2).Value = report.TotalSpent;
        ws.Cell(4, 2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(5, 1).Value = "Total Ingresos";
        ws.Cell(5, 2).Value = report.TotalIncome;
        ws.Cell(5, 2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(6, 1).Value = "Balance Neto";
        ws.Cell(6, 2).Value = report.NetBalance;
        ws.Cell(6, 2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(7, 1).Value = "Total Gastos";
        ws.Cell(7, 2).Value = report.TotalExpenseCount;
        ws.Cell(8, 1).Value = "Total Ingresos (reg.)";
        ws.Cell(8, 2).Value = report.TotalIncomeCount;
        ws.Cell(9, 1).Value = "Generado";
        ws.Cell(9, 2).Value = report.GeneratedAt.ToString("yyyy-MM-dd HH:mm UTC");

        ws.Cell(1, 4).Value = "Mes Mayor Gasto";
        ws.Cell(1, 5).Value = GetPeakExpenseMonthLabel(report);
        ws.Cell(2, 4).Value = "Top Categoría";
        ws.Cell(2, 5).Value = GetTopExpenseCategoryLabel(report);
        ws.Cell(3, 4).Value = "Obligaciones Vencidas";
        ws.Cell(3, 5).Value = report.ObligationSummary?.OverdueCount ?? 0;
        ws.Cell(4, 4).Value = "Monto Vencido";
        ws.Cell(4, 5).Value = report.ObligationSummary?.OverdueAmount ?? 0m;
        ws.Cell(4, 5).Style.NumberFormat.Format = ExcelCurrencyFormat;

        StyleHeaderRange(ws.Range(1, 1, 9, 1));
        StyleHeaderRange(ws.Range(1, 4, 4, 4));

        // Columnas de la tabla de gastos
        var row = 11;
        var headers = new[]
        {
            "Fecha", "Título", "Categoría", "Método de Pago", "Tipo",
            "Monto Original", "Moneda Orig.", "Tasa Cambio", "Monto Convertido",
            "Descripción", "Nro. Recibo", "Notas", "Pago Obligación", "Obligación"
        };

        for (var col = 1; col <= headers.Length; col++)
        {
            ws.Cell(row, col).Value = headers[col - 1];
        }
        StyleTableHeader(ws.Range(row, 1, row, headers.Length));
        row++;

        foreach (var section in report.Sections)
        {
            // Fila de sección mensual
            ws.Cell(row, 1).Value = $"── {section.MonthLabel} ──";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.Italic = true;
            ws.Range(row, 1, row, headers.Length).Merge();
            ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.LightGray;
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
                ws.Cell(row, 10).Value = exp.Description ?? "";
                ws.Cell(row, 11).Value = exp.ReceiptNumber ?? "";
                ws.Cell(row, 12).Value = exp.Notes ?? "";
                ws.Cell(row, 13).Value = exp.IsObligationPayment ? "Sí" : "No";
                ws.Cell(row, 14).Value = exp.ObligationTitle ?? "";
                row++;
            }

            // Subtotal de sección
            ws.Cell(row, 8).Value = "Subtotal:";
            ws.Cell(row, 8).Style.Font.Bold = true;
            ws.Cell(row, 9).Value = section.SectionTotal;
            ws.Cell(row, 9).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 9).Style.Font.Bold = true;
            row++;

            ws.Cell(row, 8).Value = "Ingresos:";
            ws.Cell(row, 8).Style.Font.Bold = true;
            ws.Cell(row, 9).Value = section.SectionIncomeTotal;
            ws.Cell(row, 9).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 9).Style.Font.Bold = true;
            row++;

            ws.Cell(row, 8).Value = "Balance Neto:";
            ws.Cell(row, 8).Style.Font.Bold = true;
            ws.Cell(row, 9).Value = section.SectionNetBalance;
            ws.Cell(row, 9).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 9).Style.Font.Bold = true;
            row++;
        }

        FinalizeSheetLayout(
            ws,
            headerRow: 11,
            lastRow: Math.Max(11, row - 1),
            lastColumn: headers.Length,
            freezeRows: 11,
            enableAutoFilter: false,
            maxColumnWidth: 44,
            wrapColumns: [10, 12, 14]);

        // ── Hoja 2: Análisis por categoría (si es premium) ──
        if (report.CategoryAnalysis is { Count: > 0 })
        {
            var catWs = workbook.Worksheets.Add("Categorías");
            var catHeaders = new[]
            {
                "Categoría", "Es Default", "Presupuesto", "Gastado",
                "Nro. Gastos", "% del Total", "Restante", "% Usado", "Excedido"
            };

            for (var col = 1; col <= catHeaders.Length; col++)
                catWs.Cell(1, col).Value = catHeaders[col - 1];
            StyleTableHeader(catWs.Range(1, 1, 1, catHeaders.Length));

            var catRow = 2;
            foreach (var cat in report.CategoryAnalysis)
            {
                catWs.Cell(catRow, 1).Value = cat.CategoryName;
                catWs.Cell(catRow, 2).Value = cat.IsDefault ? "Sí" : "No";
                catWs.Cell(catRow, 3).Value = cat.BudgetAmount ?? 0;
                catWs.Cell(catRow, 3).Style.NumberFormat.Format = ExcelCurrencyFormat;
                catWs.Cell(catRow, 4).Value = cat.SpentAmount;
                catWs.Cell(catRow, 4).Style.NumberFormat.Format = ExcelCurrencyFormat;
                catWs.Cell(catRow, 5).Value = cat.ExpenseCount;
                catWs.Cell(catRow, 6).Value = cat.Percentage;
                catWs.Cell(catRow, 6).Style.NumberFormat.Format = ExcelPercentFormat;
                catWs.Cell(catRow, 7).Value = cat.BudgetRemaining ?? 0;
                catWs.Cell(catRow, 7).Style.NumberFormat.Format = ExcelCurrencyFormat;
                catWs.Cell(catRow, 8).Value = cat.BudgetUsedPercentage ?? 0;
                catWs.Cell(catRow, 8).Style.NumberFormat.Format = ExcelPercentFormat;
                catWs.Cell(catRow, 9).Value = cat.BudgetExceeded == true ? "⚠ Sí" : "No";

                if (cat.BudgetExceeded == true)
                    catWs.Range(catRow, 1, catRow, catHeaders.Length).Style.Fill.BackgroundColor = XLColor.LightCoral;

                catRow++;
            }

            FinalizeSheetLayout(
                catWs,
                headerRow: 1,
                lastRow: Math.Max(1, catRow - 1),
                lastColumn: catHeaders.Length,
                freezeRows: 1,
                maxColumnWidth: 36,
                wrapColumns: [1]);
        }

        // ── Hoja 3: Obligaciones (si es premium) ────────────
        if (report.ObligationSummary is not null)
        {
            var oblWs = workbook.Worksheets.Add("Obligaciones");

            // Resumen
            oblWs.Cell(1, 1).Value = "Total Obligaciones";
            oblWs.Cell(1, 2).Value = report.ObligationSummary.TotalObligations;
            oblWs.Cell(2, 1).Value = "Monto Total";
            oblWs.Cell(2, 2).Value = report.ObligationSummary.TotalAmount;
            oblWs.Cell(2, 2).Style.NumberFormat.Format = ExcelCurrencyFormat;
            oblWs.Cell(3, 1).Value = "Total Pagado";
            oblWs.Cell(3, 2).Value = report.ObligationSummary.TotalPaid;
            oblWs.Cell(3, 2).Style.NumberFormat.Format = ExcelCurrencyFormat;
            oblWs.Cell(4, 1).Value = "Total Pendiente";
            oblWs.Cell(4, 2).Value = report.ObligationSummary.TotalPending;
            oblWs.Cell(4, 2).Style.NumberFormat.Format = ExcelCurrencyFormat;
            oblWs.Cell(5, 1).Value = "Vencidas";
            oblWs.Cell(5, 2).Value = report.ObligationSummary.OverdueCount;

            StyleHeaderRange(oblWs.Range(1, 1, 5, 1));

            var oblHeaders = new[]
            {
                "Estado", "Título", "Descripción", "Monto Total",
                "Pagado", "Restante", "Moneda", "Fecha Vencimiento",
                "Nro. Pagos", "Último Pago"
            };

            var oblRow = 7;
            for (var col = 1; col <= oblHeaders.Length; col++)
                oblWs.Cell(oblRow, col).Value = oblHeaders[col - 1];
            StyleTableHeader(oblWs.Range(oblRow, 1, oblRow, oblHeaders.Length));
            oblRow++;

            foreach (var group in report.ObligationSummary.ByStatus)
            {
                foreach (var obl in group.Obligations)
                {
                    oblWs.Cell(oblRow, 1).Value = FormatStatus(obl.Status);
                    oblWs.Cell(oblRow, 2).Value = obl.Title;
                    oblWs.Cell(oblRow, 3).Value = obl.Description ?? "";
                    oblWs.Cell(oblRow, 4).Value = obl.TotalAmount;
                    oblWs.Cell(oblRow, 4).Style.NumberFormat.Format = ExcelCurrencyFormat;
                    oblWs.Cell(oblRow, 5).Value = obl.PaidAmount;
                    oblWs.Cell(oblRow, 5).Style.NumberFormat.Format = ExcelCurrencyFormat;
                    oblWs.Cell(oblRow, 6).Value = obl.RemainingAmount;
                    oblWs.Cell(oblRow, 6).Style.NumberFormat.Format = ExcelCurrencyFormat;
                    oblWs.Cell(oblRow, 7).Value = obl.Currency;
                    oblWs.Cell(oblRow, 8).Value = obl.DueDate?.ToString("yyyy-MM-dd") ?? "—";
                    oblWs.Cell(oblRow, 9).Value = obl.PaymentCount;
                    oblWs.Cell(oblRow, 10).Value = obl.LastPaymentDate?.ToString("yyyy-MM-dd") ?? "—";

                    if (obl.Status == "overdue")
                        oblWs.Range(oblRow, 1, oblRow, oblHeaders.Length).Style.Fill.BackgroundColor = XLColor.LightCoral;

                    oblRow++;
                }
            }

            FinalizeSheetLayout(
                oblWs,
                headerRow: 7,
                lastRow: Math.Max(7, oblRow - 1),
                lastColumn: oblHeaders.Length,
                freezeRows: 7,
                maxColumnWidth: 40,
                wrapColumns: [2, 3]);
        }

        return WorkbookToBytes(workbook);
    }

    // ════════════════════════════════════════════════════════
    //  PAYMENT METHOD REPORT — EXCEL
    // ════════════════════════════════════════════════════════

    public byte[] GeneratePaymentMethodReportExcel(PaymentMethodReportResponse report)
    {
        using var workbook = new XLWorkbook();
        ApplyWorkbookDefaults(workbook, "Reporte de Metodos de Pago", "Reporte de gastos por metodos de pago del usuario.");

        // ── Hoja 1: Resumen por método de pago ──────────────
        var ws = workbook.Worksheets.Add("Métodos de Pago");

        ws.Cell(1, 1).Value = "Período";
        ws.Cell(1, 2).Value = FormatDateRange(report.DateFrom, report.DateTo);
        ws.Cell(2, 1).Value = "Total Gastado";
        ws.Cell(2, 2).Value = report.GrandTotalSpent;
        ws.Cell(2, 2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(3, 1).Value = "Total Ingresos";
        ws.Cell(3, 2).Value = report.GrandTotalIncome;
        ws.Cell(3, 2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(4, 1).Value = "Balance Neto";
        ws.Cell(4, 2).Value = report.GrandNetFlow;
        ws.Cell(4, 2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(5, 1).Value = "Total Gastos";
        ws.Cell(5, 2).Value = report.GrandTotalExpenseCount;
        ws.Cell(6, 1).Value = "Total Ingresos (reg.)";
        ws.Cell(6, 2).Value = report.GrandTotalIncomeCount;
        ws.Cell(7, 1).Value = "Generado";
        ws.Cell(7, 2).Value = report.GeneratedAt.ToString("yyyy-MM-dd HH:mm UTC");

        ws.Cell(1, 4).Value = "Método Líder";
        ws.Cell(1, 5).Value = GetTopPaymentMethodLabel(report);
        ws.Cell(2, 4).Value = "Mes Pico";
        ws.Cell(2, 5).Value = GetPeakTrendMonthLabel(report);
        ws.Cell(3, 4).Value = "Prom. Ingreso / Transacción";
        ws.Cell(3, 5).Value = report.GrandTotalIncomeCount > 0
            ? report.GrandTotalIncome / report.GrandTotalIncomeCount
            : 0m;
        ws.Cell(3, 5).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(4, 4).Value = "Prom. Gasto / Transacción";
        ws.Cell(4, 5).Value = report.GrandTotalExpenseCount > 0
            ? report.GrandTotalSpent / report.GrandTotalExpenseCount
            : 0m;
        ws.Cell(4, 5).Style.NumberFormat.Format = ExcelCurrencyFormat;

        StyleHeaderRange(ws.Range(1, 1, 7, 1));
        StyleHeaderRange(ws.Range(1, 4, 4, 4));

        var headers = new[]
        {
            "Método de Pago", "Tipo", "Moneda", "Banco",
            "Total Gastado", "Nro. Gastos", "Total Ingresos", "Nro. Ingresos",
            "Balance Neto", "% del Gasto", "Promedio Gasto", "Promedio Ingreso",
            "Primer Uso", "Último Uso"
        };

        var row = 9;
        for (var col = 1; col <= headers.Length; col++)
            ws.Cell(row, col).Value = headers[col - 1];
        StyleTableHeader(ws.Range(row, 1, row, headers.Length));
        row++;

        foreach (var pm in report.PaymentMethods)
        {
            ws.Cell(row, 1).Value = pm.Name;
            ws.Cell(row, 2).Value = pm.Type;
            ws.Cell(row, 3).Value = pm.Currency;
            ws.Cell(row, 4).Value = pm.BankName ?? "—";
            ws.Cell(row, 5).Value = pm.TotalSpent;
            ws.Cell(row, 5).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 6).Value = pm.ExpenseCount;
            ws.Cell(row, 7).Value = pm.TotalIncome;
            ws.Cell(row, 7).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 8).Value = pm.IncomeCount;
            ws.Cell(row, 9).Value = pm.NetFlow;
            ws.Cell(row, 9).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 10).Value = pm.Percentage;
            ws.Cell(row, 10).Style.NumberFormat.Format = ExcelPercentFormat;
            ws.Cell(row, 11).Value = pm.AverageExpenseAmount;
            ws.Cell(row, 11).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 12).Value = pm.AverageIncomeAmount;
            ws.Cell(row, 12).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 13).Value = pm.FirstUseDate?.ToString("yyyy-MM-dd") ?? "—";
            ws.Cell(row, 14).Value = pm.LastUseDate?.ToString("yyyy-MM-dd") ?? "—";
            row++;
        }

        FinalizeSheetLayout(
            ws,
            headerRow: 9,
            lastRow: Math.Max(9, row - 1),
            lastColumn: headers.Length,
            freezeRows: 9,
            maxColumnWidth: 36,
            wrapColumns: [1, 4]);

        // ── Hoja 2: Desglose por proyecto ───────────────────
        var projWs = workbook.Worksheets.Add("Por Proyecto");
        var projHeaders = new[]
        {
            "Método de Pago", "Proyecto", "Moneda Proyecto",
            "Total Gastado", "Nro. Gastos", "% del Método"
        };

        for (var col = 1; col <= projHeaders.Length; col++)
            projWs.Cell(1, col).Value = projHeaders[col - 1];
        StyleTableHeader(projWs.Range(1, 1, 1, projHeaders.Length));

        var projRow = 2;
        foreach (var pm in report.PaymentMethods)
        {
            foreach (var proj in pm.Projects)
            {
                projWs.Cell(projRow, 1).Value = pm.Name;
                projWs.Cell(projRow, 2).Value = proj.ProjectName;
                projWs.Cell(projRow, 3).Value = proj.ProjectCurrency;
                projWs.Cell(projRow, 4).Value = proj.TotalSpent;
                projWs.Cell(projRow, 4).Style.NumberFormat.Format = ExcelCurrencyFormat;
                projWs.Cell(projRow, 5).Value = proj.ExpenseCount;
                projWs.Cell(projRow, 6).Value = proj.Percentage;
                projWs.Cell(projRow, 6).Style.NumberFormat.Format = ExcelPercentFormat;
                projRow++;
            }
        }

        FinalizeSheetLayout(
            projWs,
            headerRow: 1,
            lastRow: Math.Max(1, projRow - 1),
            lastColumn: projHeaders.Length,
            freezeRows: 1,
            maxColumnWidth: 36,
            wrapColumns: [1, 2]);

        // ── Hoja 3: Gastos detallados ───────────────────────
        var expWs = workbook.Worksheets.Add("Gastos");
        var expHeaders = new[]
        {
            "Fecha", "Título", "Método de Pago", "Proyecto",
            "Categoría", "Monto Original", "Mon. Original",
            "Monto Convertido", "Mon. Proyecto", "Descripción"
        };

        for (var col = 1; col <= expHeaders.Length; col++)
            expWs.Cell(1, col).Value = expHeaders[col - 1];
        StyleTableHeader(expWs.Range(1, 1, 1, expHeaders.Length));

        var expRow = 2;
        foreach (var pm in report.PaymentMethods)
        {
            foreach (var exp in pm.Expenses)
            {
                expWs.Cell(expRow, 1).Value = exp.ExpenseDate.ToString("yyyy-MM-dd");
                expWs.Cell(expRow, 2).Value = exp.Title;
                expWs.Cell(expRow, 3).Value = pm.Name;
                expWs.Cell(expRow, 4).Value = exp.ProjectName;
                expWs.Cell(expRow, 5).Value = exp.CategoryName;
                expWs.Cell(expRow, 6).Value = exp.OriginalAmount;
                expWs.Cell(expRow, 6).Style.NumberFormat.Format = ExcelCurrencyFormat;
                expWs.Cell(expRow, 7).Value = exp.OriginalCurrency;
                expWs.Cell(expRow, 8).Value = exp.ConvertedAmount;
                expWs.Cell(expRow, 8).Style.NumberFormat.Format = ExcelCurrencyFormat;
                expWs.Cell(expRow, 9).Value = exp.ProjectCurrency;
                expWs.Cell(expRow, 10).Value = exp.Description ?? "";
                expRow++;
            }
        }

        FinalizeSheetLayout(
            expWs,
            headerRow: 1,
            lastRow: Math.Max(1, expRow - 1),
            lastColumn: expHeaders.Length,
            freezeRows: 1,
            maxColumnWidth: 42,
            wrapColumns: [2, 4, 10]);

        // ── Hoja 4: Ingresos detallados ─────────────────────
        var incWs = workbook.Worksheets.Add("Ingresos");
        var incHeaders = new[]
        {
            "Fecha", "Título", "Método de Pago", "Proyecto",
            "Categoría", "Monto Original", "Mon. Original",
            "Monto Cuenta", "Mon. Cuenta",
            "Monto Convertido", "Mon. Proyecto", "Descripción"
        };

        for (var col = 1; col <= incHeaders.Length; col++)
            incWs.Cell(1, col).Value = incHeaders[col - 1];
        StyleTableHeader(incWs.Range(1, 1, 1, incHeaders.Length));

        var incRow = 2;
        foreach (var pm in report.PaymentMethods)
        {
            foreach (var inc in pm.Incomes)
            {
                incWs.Cell(incRow, 1).Value = inc.IncomeDate.ToString("yyyy-MM-dd");
                incWs.Cell(incRow, 2).Value = inc.Title;
                incWs.Cell(incRow, 3).Value = pm.Name;
                incWs.Cell(incRow, 4).Value = inc.ProjectName;
                incWs.Cell(incRow, 5).Value = inc.CategoryName;
                incWs.Cell(incRow, 6).Value = inc.OriginalAmount;
                incWs.Cell(incRow, 6).Style.NumberFormat.Format = ExcelCurrencyFormat;
                incWs.Cell(incRow, 7).Value = inc.OriginalCurrency;
                incWs.Cell(incRow, 8).Value = inc.AccountAmount;
                incWs.Cell(incRow, 8).Style.NumberFormat.Format = ExcelCurrencyFormat;
                incWs.Cell(incRow, 9).Value = inc.AccountCurrency ?? "—";
                incWs.Cell(incRow, 10).Value = inc.ConvertedAmount;
                incWs.Cell(incRow, 10).Style.NumberFormat.Format = ExcelCurrencyFormat;
                incWs.Cell(incRow, 11).Value = inc.ProjectCurrency;
                incWs.Cell(incRow, 12).Value = inc.Description ?? "";
                incRow++;
            }
        }

        FinalizeSheetLayout(
            incWs,
            headerRow: 1,
            lastRow: Math.Max(1, incRow - 1),
            lastColumn: incHeaders.Length,
            freezeRows: 1,
            maxColumnWidth: 42,
            wrapColumns: [2, 4, 12]);

        // ── Hoja 5: Tendencia mensual ───────────────────────
        var trendWs = workbook.Worksheets.Add("Tendencia Mensual");
        var trendHeaders = new[]
        {
            "Mes", "Total Gastado", "Nro. Gastos",
            "Total Ingresos", "Nro. Ingresos", "Balance Neto"
        };

        for (var col = 1; col <= trendHeaders.Length; col++)
            trendWs.Cell(1, col).Value = trendHeaders[col - 1];
        StyleTableHeader(trendWs.Range(1, 1, 1, trendHeaders.Length));

        var trendRow = 2;
        foreach (var m in report.MonthlyTrend)
        {
            trendWs.Cell(trendRow, 1).Value = m.MonthLabel;
            trendWs.Cell(trendRow, 2).Value = m.TotalSpent;
            trendWs.Cell(trendRow, 2).Style.NumberFormat.Format = ExcelCurrencyFormat;
            trendWs.Cell(trendRow, 3).Value = m.ExpenseCount;
            trendWs.Cell(trendRow, 4).Value = m.TotalIncome;
            trendWs.Cell(trendRow, 4).Style.NumberFormat.Format = ExcelCurrencyFormat;
            trendWs.Cell(trendRow, 5).Value = m.IncomeCount;
            trendWs.Cell(trendRow, 6).Value = m.NetBalance;
            trendWs.Cell(trendRow, 6).Style.NumberFormat.Format = ExcelCurrencyFormat;
            trendRow++;
        }

        FinalizeSheetLayout(
            trendWs,
            headerRow: 1,
            lastRow: Math.Max(1, trendRow - 1),
            lastColumn: trendHeaders.Length,
            freezeRows: 1,
            maxColumnWidth: 28,
            wrapColumns: []);

        return WorkbookToBytes(workbook);
    }

    // ════════════════════════════════════════════════════════
    //  EXPENSE REPORT — PDF
    // ════════════════════════════════════════════════════════

    public byte[] GenerateExpenseReportPdf(DetailedExpenseReportResponse report)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Element(h => ComposeExpenseReportHeader(h, report));
                page.Content().Element(c => ComposeExpenseReportContent(c, report));
                page.Footer().Element(ComposeFooter);
            });
        });

        return document.GeneratePdf();
    }

    private static void ComposeExpenseReportHeader(IContainer container, DetailedExpenseReportResponse report)
    {
        var peakMonth = GetPeakExpenseMonthLabel(report);
        var topCategory = GetTopExpenseCategoryLabel(report);

        container.Column(col =>
        {
            col.Item().Text($"Reporte de Gastos — {report.ProjectName}")
                .FontSize(16).Bold().FontColor(Colors.Blue.Darken3);

            col.Item().Row(row =>
            {
                row.RelativeItem().Text($"Moneda: {report.CurrencyCode}").FontSize(9);
                row.RelativeItem().Text($"Período: {FormatDateRange(report.DateFrom, report.DateTo)}").FontSize(9);
                row.RelativeItem().AlignRight().Text($"Generado: {report.GeneratedAt:yyyy-MM-dd HH:mm} UTC").FontSize(8);
            });

            col.Item().PaddingTop(5).Row(row =>
            {
                row.RelativeItem().Background(Colors.Blue.Lighten4).Padding(8).Column(c =>
                {
                    c.Item().Text("Total Gastado").FontSize(8).FontColor(Colors.Grey.Darken2);
                    c.Item().Text($"{report.CurrencyCode} {report.TotalSpent:N2}").FontSize(14).Bold();
                });
                row.ConstantItem(10);
                row.RelativeItem().Background(Colors.Grey.Lighten3).Padding(8).Column(c =>
                {
                    c.Item().Text("Transacciones").FontSize(8).FontColor(Colors.Grey.Darken2);
                    c.Item().Text($"{report.TotalExpenseCount}").FontSize(14).Bold();
                });
                row.ConstantItem(10);
                row.RelativeItem().Background(Colors.Grey.Lighten3).Padding(8).Column(c =>
                {
                    c.Item().Text("Mes Pico").FontSize(8).FontColor(Colors.Grey.Darken2);
                    c.Item().Text(peakMonth).FontSize(10).Bold();
                });
            });

            col.Item().PaddingTop(6).Text($"Top categoría: {topCategory}")
                .FontSize(8).FontColor(Colors.Grey.Darken1);

            col.Item().PaddingTop(2)
                .Text($"Total ingresos: {FormatCurrency(report.CurrencyCode, report.TotalIncome)}  ·  Balance neto: {FormatCurrency(report.CurrencyCode, report.NetBalance)}")
                .FontSize(8).FontColor(Colors.Grey.Darken1);

            col.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        });
    }

    private static void ComposeExpenseReportContent(IContainer container, DetailedExpenseReportResponse report)
    {
        container.Column(col =>
        {
            if (report.Sections.Count == 0)
            {
                ComposePdfEmptyState(col, "No se encontraron gastos para el período seleccionado.");
                return;
            }

            // Secciones mensuales de gastos
            foreach (var section in report.Sections)
            {
                col.Item().PaddingTop(8).Text($"{section.MonthLabel}")
                    .FontSize(12).Bold().FontColor(Colors.Blue.Darken2);

                col.Item().Text($"Subtotal: {FormatCurrency(report.CurrencyCode, section.SectionTotal)}  ·  {section.SectionCount} gastos")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);

                col.Item().PaddingTop(4).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(65);  // Fecha
                        columns.RelativeColumn(3);   // Título
                        columns.RelativeColumn(2);   // Categoría
                        columns.RelativeColumn(2);   // Método de Pago
                        columns.ConstantColumn(70);  // Monto
                    });

                    // Header
                    table.Header(header =>
                    {
                        PdfTableHeaderCell(header, "Fecha");
                        PdfTableHeaderCell(header, "Título");
                        PdfTableHeaderCell(header, "Categoría");
                        PdfTableHeaderCell(header, "Método de Pago");
                        PdfTableHeaderCell(header, "Monto", true);
                    });

                    foreach (var exp in section.Expenses)
                    {
                        PdfTableCell(table, exp.ExpenseDate.ToString("yyyy-MM-dd"));
                        PdfTableCell(table, exp.Title);
                        PdfTableCell(table, exp.CategoryName);
                        PdfTableCell(table, exp.PaymentMethodName);
                        PdfTableCell(table, FormatCurrency(report.CurrencyCode, exp.ConvertedAmount), true);
                    }
                });
            }

            // Análisis de categorías (premium)
            if (report.CategoryAnalysis is { Count: > 0 })
            {
                col.Item().PaddingTop(15).Text("Análisis por Categoría")
                    .FontSize(14).Bold().FontColor(Colors.Blue.Darken3);

                col.Item().PaddingTop(4).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);   // Categoría
                        columns.ConstantColumn(70);  // Presupuesto
                        columns.ConstantColumn(70);  // Gastado
                        columns.ConstantColumn(50);  // %
                        columns.ConstantColumn(70);  // Restante
                        columns.ConstantColumn(55);  // Estado
                    });

                    table.Header(header =>
                    {
                        PdfTableHeaderCell(header, "Categoría");
                        PdfTableHeaderCell(header, "Presupuesto", true);
                        PdfTableHeaderCell(header, "Gastado", true);
                        PdfTableHeaderCell(header, "%", true);
                        PdfTableHeaderCell(header, "Restante", true);
                        PdfTableHeaderCell(header, "Estado");
                    });

                    foreach (var cat in report.CategoryAnalysis)
                    {
                        PdfTableCell(table, cat.CategoryName);
                        PdfTableCell(table, cat.BudgetAmount.HasValue ? FormatCurrency(report.CurrencyCode, cat.BudgetAmount.Value) : "—", true);
                        PdfTableCell(table, FormatCurrency(report.CurrencyCode, cat.SpentAmount), true);
                        PdfTableCell(table, $"{cat.Percentage:N1}%", true);
                        PdfTableCell(table, cat.BudgetRemaining.HasValue ? FormatCurrency(report.CurrencyCode, cat.BudgetRemaining.Value) : "—", true);

                        var status = cat.BudgetExceeded == true ? "⚠ Excedido" : "OK";
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                            .PaddingVertical(3).PaddingHorizontal(4)
                            .Text(status).FontSize(8)
                            .FontColor(cat.BudgetExceeded == true ? Colors.Red.Darken1 : Colors.Green.Darken2);
                    }
                });
            }

            // Obligaciones (premium)
            if (report.ObligationSummary is not null)
            {
                col.Item().PaddingTop(15).Text("Resumen de Obligaciones")
                    .FontSize(14).Bold().FontColor(Colors.Blue.Darken3);

                col.Item().PaddingTop(4).Row(row =>
                {
                    void SummaryBox(IContainer c, string label, string value, string color)
                    {
                        c.Background(color).Padding(6).Column(inner =>
                        {
                            inner.Item().Text(label).FontSize(7).FontColor(Colors.Grey.Darken2);
                            inner.Item().Text(value).FontSize(11).Bold();
                        });
                    }

                    var obl = report.ObligationSummary;
                    row.RelativeItem().Element(c => SummaryBox(c, "Total", FormatCurrency(report.CurrencyCode, obl.TotalAmount), Colors.Grey.Lighten3));
                    row.ConstantItem(6);
                    row.RelativeItem().Element(c => SummaryBox(c, "Pagado", FormatCurrency(report.CurrencyCode, obl.TotalPaid), Colors.Green.Lighten4));
                    row.ConstantItem(6);
                    row.RelativeItem().Element(c => SummaryBox(c, "Pendiente", FormatCurrency(report.CurrencyCode, obl.TotalPending), Colors.Orange.Lighten4));
                    row.ConstantItem(6);
                    row.RelativeItem().Element(c => SummaryBox(c, "Vencidas", $"{obl.OverdueCount}", Colors.Red.Lighten4));
                });

                col.Item().PaddingTop(6).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(60);  // Estado
                        columns.RelativeColumn(3);   // Título
                        columns.ConstantColumn(70);  // Total
                        columns.ConstantColumn(70);  // Pagado
                        columns.ConstantColumn(70);  // Restante
                        columns.ConstantColumn(65);  // Vence
                    });

                    table.Header(header =>
                    {
                        PdfTableHeaderCell(header, "Estado");
                        PdfTableHeaderCell(header, "Título");
                        PdfTableHeaderCell(header, "Total", true);
                        PdfTableHeaderCell(header, "Pagado", true);
                        PdfTableHeaderCell(header, "Restante", true);
                        PdfTableHeaderCell(header, "Vence");
                    });

                    foreach (var group in report.ObligationSummary.ByStatus)
                    {
                        foreach (var obl in group.Obligations)
                        {
                            var statusColor = obl.Status switch
                            {
                                "overdue" => Colors.Red.Darken1,
                                "paid" => Colors.Green.Darken2,
                                "partially_paid" => Colors.Orange.Darken2,
                                _ => Colors.Grey.Darken1
                            };

                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                                .PaddingVertical(3).PaddingHorizontal(4)
                                .Text(FormatStatus(obl.Status)).FontSize(8).FontColor(statusColor);

                            PdfTableCell(table, obl.Title);
                            PdfTableCell(table, FormatCurrency(obl.Currency, obl.TotalAmount), true);
                            PdfTableCell(table, FormatCurrency(obl.Currency, obl.PaidAmount), true);
                            PdfTableCell(table, FormatCurrency(obl.Currency, obl.RemainingAmount), true);
                            PdfTableCell(table, obl.DueDate?.ToString("yyyy-MM-dd") ?? "—");
                        }
                    }
                });
            }
        });
    }

    // ════════════════════════════════════════════════════════
    //  PAYMENT METHOD REPORT — PDF
    // ════════════════════════════════════════════════════════

    public byte[] GeneratePaymentMethodReportPdf(PaymentMethodReportResponse report)
    {
        var topMethod = GetTopPaymentMethodLabel(report);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Text("Reporte de Métodos de Pago")
                        .FontSize(16).Bold().FontColor(Colors.Blue.Darken3);

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"Período: {FormatDateRange(report.DateFrom, report.DateTo)}").FontSize(9);
                        row.RelativeItem().AlignRight().Text($"Generado: {report.GeneratedAt:yyyy-MM-dd HH:mm} UTC").FontSize(8);
                    });

                    col.Item().PaddingTop(5).Row(row =>
                    {
                        row.RelativeItem().Background(Colors.Blue.Lighten4).Padding(8).Column(c =>
                        {
                            c.Item().Text("Total Gastado").FontSize(8).FontColor(Colors.Grey.Darken2);
                            c.Item().Text($"{report.GrandTotalSpent:N2}").FontSize(14).Bold();
                        });
                        row.ConstantItem(10);
                        row.RelativeItem().Background(Colors.Grey.Lighten3).Padding(8).Column(c =>
                        {
                            c.Item().Text("Transacciones").FontSize(8).FontColor(Colors.Grey.Darken2);
                            c.Item().Text($"{report.GrandTotalExpenseCount}").FontSize(14).Bold();
                        });
                        row.ConstantItem(10);
                        row.RelativeItem().Background(Colors.Grey.Lighten3).Padding(8).Column(c =>
                        {
                            c.Item().Text("Método Líder").FontSize(8).FontColor(Colors.Grey.Darken2);
                            c.Item().Text(topMethod).FontSize(10).Bold();
                        });
                    });

                    col.Item().PaddingTop(6).Row(row =>
                    {
                        row.RelativeItem().Background(Colors.Green.Lighten4).Padding(8).Column(c =>
                        {
                            c.Item().Text("Total Ingresos").FontSize(8).FontColor(Colors.Grey.Darken2);
                            c.Item().Text($"{report.GrandTotalIncome:N2}").FontSize(12).Bold();
                        });
                        row.ConstantItem(10);
                        row.RelativeItem().Background(Colors.Orange.Lighten4).Padding(8).Column(c =>
                        {
                            c.Item().Text("Balance Neto").FontSize(8).FontColor(Colors.Grey.Darken2);
                            c.Item().Text($"{report.GrandNetFlow:N2}").FontSize(12).Bold();
                        });
                    });

                    col.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().Column(col =>
                {
                    if (report.PaymentMethods.Count == 0)
                    {
                        ComposePdfEmptyState(col, "No se encontraron gastos para métodos de pago en el período seleccionado.");
                        return;
                    }

                    // Tabla resumen por método
                    col.Item().Text("Resumen por Método de Pago")
                        .FontSize(12).Bold().FontColor(Colors.Blue.Darken2);

                    col.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);   // Nombre
                            columns.ConstantColumn(45);  // Tipo
                            columns.ConstantColumn(40);  // Moneda
                            columns.ConstantColumn(70);  // Total
                            columns.ConstantColumn(70);  // Ingresos
                            columns.ConstantColumn(70);  // Balance
                            columns.ConstantColumn(40);  // Gastos
                            columns.ConstantColumn(40);  // Ingresos (count)
                        });

                        table.Header(header =>
                        {
                            PdfTableHeaderCell(header, "Método");
                            PdfTableHeaderCell(header, "Tipo");
                            PdfTableHeaderCell(header, "Moneda");
                            PdfTableHeaderCell(header, "Total", true);
                            PdfTableHeaderCell(header, "Ingresos", true);
                            PdfTableHeaderCell(header, "Balance", true);
                            PdfTableHeaderCell(header, "Gastos", true);
                            PdfTableHeaderCell(header, "Ingr.", true);
                        });

                        foreach (var pm in report.PaymentMethods)
                        {
                            PdfTableCell(table, pm.Name);
                            PdfTableCell(table, pm.Type);
                            PdfTableCell(table, pm.Currency);
                            PdfTableCell(table, FormatCurrency(pm.Currency, pm.TotalSpent), true);
                            PdfTableCell(table, FormatCurrency(pm.Currency, pm.TotalIncome), true);
                            PdfTableCell(table, FormatCurrency(pm.Currency, pm.NetFlow), true);
                            PdfTableCell(table, $"{pm.ExpenseCount}", true);
                            PdfTableCell(table, $"{pm.IncomeCount}", true);
                        }
                    });

                    // Desglose por proyecto
                    if (report.PaymentMethods.Any(pm => pm.Projects.Count > 0))
                    {
                        col.Item().PaddingTop(12).Text("Desglose por Proyecto")
                            .FontSize(12).Bold().FontColor(Colors.Blue.Darken2);

                        foreach (var pm in report.PaymentMethods.Where(pm => pm.Projects.Count > 0))
                        {
                            col.Item().PaddingTop(6).Text($"{pm.Name} ({pm.Type})")
                                .FontSize(10).Bold();

                            col.Item().PaddingTop(2).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3);   // Proyecto
                                    columns.ConstantColumn(50);  // Moneda
                                    columns.ConstantColumn(70);  // Total
                                    columns.ConstantColumn(40);  // Gastos
                                    columns.ConstantColumn(40);  // %
                                });

                                table.Header(header =>
                                {
                                    PdfTableHeaderCell(header, "Proyecto");
                                    PdfTableHeaderCell(header, "Moneda");
                                    PdfTableHeaderCell(header, "Total", true);
                                    PdfTableHeaderCell(header, "Gastos", true);
                                    PdfTableHeaderCell(header, "%", true);
                                });

                                foreach (var proj in pm.Projects)
                                {
                                    PdfTableCell(table, proj.ProjectName);
                                    PdfTableCell(table, proj.ProjectCurrency);
                                    PdfTableCell(table, FormatCurrency(proj.ProjectCurrency, proj.TotalSpent), true);
                                    PdfTableCell(table, $"{proj.ExpenseCount}", true);
                                    PdfTableCell(table, $"{proj.Percentage:N1}%", true);
                                }
                            });
                        }
                    }

                    // Tendencia mensual
                    if (report.MonthlyTrend.Count > 0)
                    {
                        col.Item().PaddingTop(12).Text("Tendencia Mensual")
                            .FontSize(12).Bold().FontColor(Colors.Blue.Darken2);

                        col.Item().PaddingTop(4).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);   // Mes
                                columns.ConstantColumn(70);  // Total
                                columns.ConstantColumn(50);  // Gastos
                                columns.ConstantColumn(70);  // Ingresos
                                columns.ConstantColumn(50);  // Ingresos count
                                columns.ConstantColumn(70);  // Balance
                            });

                            table.Header(header =>
                            {
                                PdfTableHeaderCell(header, "Mes");
                                PdfTableHeaderCell(header, "Total", true);
                                PdfTableHeaderCell(header, "Gastos", true);
                                PdfTableHeaderCell(header, "Ingresos", true);
                                PdfTableHeaderCell(header, "Ingr.", true);
                                PdfTableHeaderCell(header, "Balance", true);
                            });

                            foreach (var m in report.MonthlyTrend)
                            {
                                PdfTableCell(table, m.MonthLabel);
                                PdfTableCell(table, $"{m.TotalSpent:N2}", true);
                                PdfTableCell(table, $"{m.ExpenseCount}", true);
                                PdfTableCell(table, $"{m.TotalIncome:N2}", true);
                                PdfTableCell(table, $"{m.IncomeCount}", true);
                                PdfTableCell(table, $"{m.NetBalance:N2}", true);
                            }
                        });
                    }
                });

                page.Footer().Element(ComposeFooter);
            });
        });

        return document.GeneratePdf();
    }
}
