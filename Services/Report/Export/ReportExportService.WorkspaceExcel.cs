using ClosedXML.Excel;
using ProjectLedger.API.DTOs.Report;

namespace ProjectLedger.API.Services.Report;

public partial class ReportExportService
{
    // ════════════════════════════════════════════════════════
    //  WORKSPACE REPORT — EXCEL
    // ════════════════════════════════════════════════════════

    public byte[] GenerateWorkspaceReportExcel(WorkspaceReportResponse report)
    {
        using var workbook = new XLWorkbook();
        ApplyWorkbookDefaults(workbook, "Reporte de Workspace", "Reporte consolidado de workspace.");

        AddWorkspaceSummarySheet(workbook, report);

        if (report.ConsolidatedByCategory.Count > 0)
            AddWorkspaceCategorySheet(workbook, report);

        if (report.MonthlyTrend.Count > 0)
        {
            AddWorkspaceTrendSheet(workbook, report);

            var hasProjectBreakdown = report.MonthlyTrend.Any(m => m.ByProject.Count > 0);
            if (hasProjectBreakdown)
                AddWorkspaceMonthlyByProjectSheet(workbook, report);
        }

        return WorkbookToBytes(workbook);
    }

    private static void AddWorkspaceSummarySheet(XLWorkbook workbook, WorkspaceReportResponse report)
    {
        var ws = workbook.Worksheets.Add("Resumen");

        // ── Summary block ───────────────────────────────────
        ws.Cell(1, 1).Value = "Workspace";   ws.Cell(1, 2).Value = report.WorkspaceName;
        ws.Cell(2, 1).Value = "Período";     ws.Cell(2, 2).Value = FormatDateRange(report.DateFrom, report.DateTo);
        ws.Cell(3, 1).Value = "Proyectos";   ws.Cell(3, 2).Value = report.ProjectCount;
        ws.Cell(4, 1).Value = "Generado";    ws.Cell(4, 2).Value = report.GeneratedAt.ToString("yyyy-MM-dd HH:mm UTC");

        if (report.ConsolidatedTotals is not null)
        {
            ws.Cell(5,  1).Value = "Moneda Ref.";   ws.Cell(5,  2).Value = report.ReferenceCurrency ?? "—";
            ws.Cell(6,  1).Value = "Total Gastado"; ws.Cell(6,  2).Value = report.ConsolidatedTotals.TotalSpent;
            ws.Cell(6,  2).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(7,  1).Value = "Total Ingresos"; ws.Cell(7, 2).Value = report.ConsolidatedTotals.TotalIncome;
            ws.Cell(7,  2).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(8,  1).Value = "Balance Neto";   ws.Cell(8, 2).Value = report.ConsolidatedTotals.NetBalance;
            ws.Cell(8,  2).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(9,  1).Value = "# Gastos";       ws.Cell(9, 2).Value = report.ConsolidatedTotals.TotalExpenseCount;
            ws.Cell(10, 1).Value = "# Ingresos";     ws.Cell(10, 2).Value = report.ConsolidatedTotals.TotalIncomeCount;
        }

        StyleHeaderRange(ws.Range(1, 1, 10, 1));

        // ── Insights (cols D-E) ──────────────────────────────
        if (report.Projects.Count > 0)
        {
            var topProject = report.Projects.OrderByDescending(p => p.TotalSpent).First();
            ws.Cell(1, 4).Value = "Proyecto Mayor Gasto";
            ws.Cell(1, 5).Value = $"{topProject.ProjectName} ({topProject.TotalSpent:N2} {topProject.CurrencyCode})";

            var topIncomeProject = report.Projects.OrderByDescending(p => p.TotalIncome).First();
            ws.Cell(2, 4).Value = "Proyecto Mayor Ingreso";
            ws.Cell(2, 5).Value = $"{topIncomeProject.ProjectName} ({topIncomeProject.TotalIncome:N2} {topIncomeProject.CurrencyCode})";

            var totalExpenses = report.Projects.Sum(p => p.ExpenseCount);
            var totalIncomes  = report.Projects.Sum(p => p.IncomeCount);
            ws.Cell(3, 4).Value = "Total Transacciones";
            ws.Cell(3, 5).Value = totalExpenses + totalIncomes;
        }

        StyleHeaderRange(ws.Range(1, 4, 3, 4));

        // ── Projects table ──────────────────────────────────
        const int headerRow = 13;
        string[] headers =
        [
            "Proyecto", "Moneda", "Total Gastado", "Total Ingresos",
            "Balance Neto", "# Gastos", "# Ingresos", "% del Workspace"
        ];

        for (var c = 0; c < headers.Length; c++)
            ws.Cell(headerRow, c + 1).Value = headers[c];

        StyleTableHeader(ws.Range(headerRow, 1, headerRow, headers.Length));

        var row = headerRow + 1;
        foreach (var p in report.Projects)
        {
            ws.Cell(row, 1).Value = p.ProjectName;
            ws.Cell(row, 2).Value = p.CurrencyCode;
            ws.Cell(row, 3).Value = p.TotalSpent;
            ws.Cell(row, 3).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 4).Value = p.TotalIncome;
            ws.Cell(row, 4).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 5).Value = p.NetBalance;
            ws.Cell(row, 5).Style.NumberFormat.Format = ExcelCurrencyFormat;

            if (p.NetBalance < 0)
                ws.Cell(row, 5).Style.Font.FontColor = XLColor.Red;
            else if (p.NetBalance > 0)
                ws.Cell(row, 5).Style.Font.FontColor = XLColor.DarkGreen;

            ws.Cell(row, 6).Value = p.ExpenseCount;
            ws.Cell(row, 7).Value = p.IncomeCount;

            if (p.Percentage.HasValue)
            {
                ws.Cell(row, 8).Value = p.Percentage.Value;
                ws.Cell(row, 8).Style.NumberFormat.Format = ExcelPercentFormat;
            }

            row++;
        }

        // Totals row
        if (report.ConsolidatedTotals is not null)
        {
            ws.Cell(row, 3).Value = report.ConsolidatedTotals.TotalSpent;
            ws.Cell(row, 3).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 3).Style.Font.Bold = true;
            ws.Cell(row, 4).Value = report.ConsolidatedTotals.TotalIncome;
            ws.Cell(row, 4).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 4).Style.Font.Bold = true;
            ws.Cell(row, 5).Value = report.ConsolidatedTotals.NetBalance;
            ws.Cell(row, 5).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 5).Style.Font.Bold = true;
            ws.Cell(row, 6).Value = report.ConsolidatedTotals.TotalExpenseCount;
            ws.Cell(row, 6).Style.Font.Bold = true;
            ws.Cell(row, 7).Value = report.ConsolidatedTotals.TotalIncomeCount;
            ws.Cell(row, 7).Style.Font.Bold = true;
            ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.LightGray;
            ws.Range(row, 1, row, headers.Length).Style.Border.TopBorder = XLBorderStyleValues.Thin;
        }

        FinalizeSheetLayout(ws, headerRow, row, headers.Length, headerRow, maxColumnWidth: 40);
    }

    private static void AddWorkspaceCategorySheet(XLWorkbook workbook, WorkspaceReportResponse report)
    {
        var ws = workbook.Worksheets.Add("Categorías");

        string[] headers = ["Categoría", "Total Gastado", "% del Workspace", "# Proyectos", "# Gastos", "Prom. por Proyecto"];

        for (var c = 0; c < headers.Length; c++)
            ws.Cell(1, c + 1).Value = headers[c];

        StyleTableHeader(ws.Range(1, 1, 1, headers.Length));

        var row = 2;
        foreach (var cat in report.ConsolidatedByCategory)
        {
            ws.Cell(row, 1).Value = cat.CategoryName;
            ws.Cell(row, 2).Value = cat.TotalAmount;
            ws.Cell(row, 2).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 3).Value = cat.Percentage;
            ws.Cell(row, 3).Style.NumberFormat.Format = ExcelPercentFormat;
            ws.Cell(row, 4).Value = cat.ProjectCount;
            ws.Cell(row, 5).Value = cat.ExpenseCount;
            ws.Cell(row, 6).Value = cat.ProjectCount > 0 ? cat.TotalAmount / cat.ProjectCount : 0m;
            ws.Cell(row, 6).Style.NumberFormat.Format = ExcelCurrencyFormat;
            row++;
        }

        // Totals row
        ws.Cell(row, 2).Value = report.ConsolidatedByCategory.Sum(c => c.TotalAmount);
        ws.Cell(row, 2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(row, 2).Style.Font.Bold = true;
        ws.Cell(row, 5).Value = report.ConsolidatedByCategory.Sum(c => c.ExpenseCount);
        ws.Cell(row, 5).Style.Font.Bold = true;
        ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.LightGray;
        ws.Range(row, 1, row, headers.Length).Style.Border.TopBorder = XLBorderStyleValues.Thin;

        FinalizeSheetLayout(ws, 1, Math.Max(1, row), headers.Length, 1, maxColumnWidth: 36);
    }

    private static void AddWorkspaceTrendSheet(XLWorkbook workbook, WorkspaceReportResponse report)
    {
        var ws = workbook.Worksheets.Add("Tendencia Mensual");

        string[] headers =
        [
            "Mes", "Total Gastado", "Total Ingresos", "Balance",
            "# Gastos", "# Ingresos"
        ];

        for (var c = 0; c < headers.Length; c++)
            ws.Cell(1, c + 1).Value = headers[c];

        StyleTableHeader(ws.Range(1, 1, 1, headers.Length));

        var row = 2;
        foreach (var m in report.MonthlyTrend)
        {
            ws.Cell(row, 1).Value = m.MonthLabel;
            ws.Cell(row, 2).Value = m.TotalSpent;
            ws.Cell(row, 2).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 3).Value = m.TotalIncome;
            ws.Cell(row, 3).Style.NumberFormat.Format = ExcelCurrencyFormat;
            ws.Cell(row, 4).Value = m.NetBalance;
            ws.Cell(row, 4).Style.NumberFormat.Format = ExcelCurrencyFormat;

            if (m.NetBalance < 0)
                ws.Cell(row, 4).Style.Font.FontColor = XLColor.Red;
            else if (m.NetBalance > 0)
                ws.Cell(row, 4).Style.Font.FontColor = XLColor.DarkGreen;

            ws.Cell(row, 5).Value = m.ExpenseCount;
            ws.Cell(row, 6).Value = m.IncomeCount;
            row++;
        }

        // Totals row
        ws.Cell(row, 2).Value = report.MonthlyTrend.Sum(m => m.TotalSpent);
        ws.Cell(row, 2).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(row, 2).Style.Font.Bold = true;
        ws.Cell(row, 3).Value = report.MonthlyTrend.Sum(m => m.TotalIncome);
        ws.Cell(row, 3).Style.NumberFormat.Format = ExcelCurrencyFormat;
        ws.Cell(row, 3).Style.Font.Bold = true;
        ws.Cell(row, 5).Value = report.MonthlyTrend.Sum(m => m.ExpenseCount);
        ws.Cell(row, 5).Style.Font.Bold = true;
        ws.Cell(row, 6).Value = report.MonthlyTrend.Sum(m => m.IncomeCount);
        ws.Cell(row, 6).Style.Font.Bold = true;
        ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.LightGray;
        ws.Range(row, 1, row, headers.Length).Style.Border.TopBorder = XLBorderStyleValues.Thin;

        FinalizeSheetLayout(ws, 1, Math.Max(1, row), headers.Length, 1);
    }

    private static void AddWorkspaceMonthlyByProjectSheet(XLWorkbook workbook, WorkspaceReportResponse report)
    {
        var ws = workbook.Worksheets.Add("Desglose por Proyecto");

        string[] headers = ["Mes", "Proyecto", "Moneda", "Total Gastado", "Total Ingresos", "Balance"];

        for (var c = 0; c < headers.Length; c++)
            ws.Cell(1, c + 1).Value = headers[c];

        StyleTableHeader(ws.Range(1, 1, 1, headers.Length));

        var row = 2;
        foreach (var m in report.MonthlyTrend)
        {
            foreach (var bp in m.ByProject)
            {
                ws.Cell(row, 1).Value = m.MonthLabel;
                ws.Cell(row, 2).Value = bp.ProjectName;
                ws.Cell(row, 3).Value = bp.CurrencyCode;
                ws.Cell(row, 4).Value = bp.TotalSpent;
                ws.Cell(row, 4).Style.NumberFormat.Format = ExcelCurrencyFormat;
                ws.Cell(row, 5).Value = bp.TotalIncome;
                ws.Cell(row, 5).Style.NumberFormat.Format = ExcelCurrencyFormat;
                ws.Cell(row, 6).Value = bp.NetBalance;
                ws.Cell(row, 6).Style.NumberFormat.Format = ExcelCurrencyFormat;

                if (bp.NetBalance < 0)
                    ws.Cell(row, 6).Style.Font.FontColor = XLColor.Red;
                else if (bp.NetBalance > 0)
                    ws.Cell(row, 6).Style.Font.FontColor = XLColor.DarkGreen;

                row++;
            }
        }

        FinalizeSheetLayout(ws, 1, Math.Max(1, row - 1), headers.Length, 1, maxColumnWidth: 36, wrapColumns: [1, 2]);
    }
}
