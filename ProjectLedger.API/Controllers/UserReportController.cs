using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Report;
using ProjectLedger.API.Repositories;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Reportes a nivel de usuario (transversales a proyectos).
/// Solo accede a datos propios del usuario autenticado.
/// </summary>
[ApiController]
[Route("api/reports")]
[Authorize]
[Tags("Reports & Insights")]
[Produces("application/json")]
public class UserReportController : ControllerBase
{
    private readonly IExpenseRepository _expenseRepo;
    private readonly IPaymentMethodService _paymentMethodService;
    private readonly IPlanAuthorizationService _planAuth;
    private readonly IReportExportService _exportService;

    public UserReportController(
        IExpenseRepository expenseRepo,
        IPaymentMethodService paymentMethodService,
        IPlanAuthorizationService planAuth,
        IReportExportService exportService)
    {
        _expenseRepo = expenseRepo;
        _paymentMethodService = paymentMethodService;
        _planAuth = planAuth;
        _exportService = exportService;
    }

    // ── GET /api/reports/payment-methods ────────────────────

    /// <summary>
    /// Reporte de métodos de pago del usuario con estadísticas, desglose
    /// por proyecto y tendencia mensual.
    /// Solo accede a los métodos de pago y gastos del usuario autenticado.
    /// Requiere CanUseAdvancedReports.
    /// </summary>
    /// <param name="from">Fecha inicio (opcional).</param>
    /// <param name="to">Fecha fin (opcional).</param>
    /// <param name="paymentMethodId">Filtrar por un método de pago específico (opcional).</param>
    /// <param name="format">Formato de exportación: json (default), excel, pdf.</param>
    /// <response code="200">Reporte generado.</response>
    /// <response code="403">Plan no permite reportes avanzados.</response>
    [HttpGet("payment-methods")]
    [ProducesResponseType(typeof(PaymentMethodReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPaymentMethodReport(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] Guid? paymentMethodId,
        [FromQuery] string format = "json",
        CancellationToken ct = default)
    {
        var userId = User.GetRequiredUserId();

        // Requiere reportes avanzados
        await _planAuth.ValidatePermissionAsync(userId, PlanPermission.CanUseAdvancedReports, ct);

        // PDF requiere CanUseAdvancedReports (ya validado arriba)
        // Excel también requiere CanExportData
        if (format.Equals("excel", StringComparison.OrdinalIgnoreCase))
            await _planAuth.ValidatePermissionAsync(userId, PlanPermission.CanExportData, ct);

        // Obtener solo los métodos de pago del usuario autenticado
        var userPaymentMethods = (await _paymentMethodService.GetByOwnerUserIdAsync(userId, ct)).ToList();

        // Si se filtra por un método específico, validar que pertenece al usuario
        if (paymentMethodId.HasValue)
        {
            var exists = userPaymentMethods.Any(pm => pm.PmtId == paymentMethodId.Value);
            if (!exists)
                return NotFound(new { message = "Payment method not found." });

            userPaymentMethods = userPaymentMethods
                .Where(pm => pm.PmtId == paymentMethodId.Value)
                .ToList();
        }

        var pmIds = userPaymentMethods.Select(pm => pm.PmtId).ToList();

        // Obtener todos los gastos vinculados a esos métodos de pago
        var allExpenses = (await _expenseRepo.GetByPaymentMethodIdsWithDetailsAsync(pmIds, from, to, ct))
            .ToList();

        // Filtrar: solo gastos de proyectos donde el usuario es owner
        var ownerExpenses = allExpenses
            .Where(e => e.Project?.PrjOwnerUserId == userId)
            .ToList();

        var grandTotal = ownerExpenses.Sum(e => e.ExpConvertedAmount);

        // Construir filas por método de pago
        var pmRows = userPaymentMethods.Select(pm =>
        {
            var pmExpenses = ownerExpenses
                .Where(e => e.ExpPaymentMethodId == pm.PmtId)
                .ToList();

            // pmTotal: sum in the payment method's own currency (for per-method stats)
            var pmTotal = pmExpenses.Sum(e => e.ExpOriginalAmount);
            // pmTotalConverted: sum in project currency (for cross-method % comparisons)
            var pmTotalConverted = pmExpenses.Sum(e => e.ExpConvertedAmount);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var lastUsed = pmExpenses
                .OrderByDescending(e => e.ExpExpenseDate)
                .Select(e => (DateOnly?)e.ExpExpenseDate)
                .FirstOrDefault();

            var daysSinceLastUse = lastUsed.HasValue
                ? today.DayNumber - lastUsed.Value.DayNumber
                : int.MaxValue;

            // Top expense: pick the largest by original (native) amount
            var topExpenseEntity = pmExpenses.OrderByDescending(e => e.ExpOriginalAmount).FirstOrDefault();

            // Top categories (top 5 by total original amount in method's native currency)
            var topCategories = pmExpenses
                .GroupBy(e => e.Category?.CatName ?? "Unknown")
                .Select(g =>
                {
                    var catTotal = g.Sum(e => e.ExpOriginalAmount);
                    return new PaymentMethodTopCategory
                    {
                        CategoryName = g.Key,
                        TotalAmount = catTotal,
                        ExpenseCount = g.Count(),
                        Percentage = pmTotal > 0 ? Math.Round(catTotal / pmTotal * 100, 2) : 0
                    };
                })
                .OrderByDescending(c => c.TotalAmount)
                .Take(5)
                .ToList();

            // Desglose por proyecto
            var projects = pmExpenses
                .GroupBy(e => new
                {
                    e.ExpProjectId,
                    Name = e.Project?.PrjName ?? "Unknown",
                    Currency = e.Project?.PrjCurrencyCode ?? "USD"
                })
                .Select(g => new PaymentMethodProjectBreakdown
                {
                    ProjectId = g.Key.ExpProjectId,
                    ProjectName = g.Key.Name,
                    ProjectCurrency = g.Key.Currency,
                    TotalSpent = g.Sum(e => e.ExpConvertedAmount),
                    ExpenseCount = g.Count(),
                    Percentage = pmTotalConverted > 0
                        ? Math.Round(g.Sum(e => e.ExpConvertedAmount) / pmTotalConverted * 100, 2)
                        : 0
                })
                .OrderByDescending(p => p.TotalSpent)
                .ToList();

            // Cap expenses at 20 most recent
            const int maxExpenses = 20;
            var expenseRows = pmExpenses
                .OrderByDescending(e => e.ExpExpenseDate)
                .Take(maxExpenses)
                .Select(e => new PaymentMethodExpenseRow
                {
                    ExpenseId = e.ExpId,
                    Title = e.ExpTitle,
                    ExpenseDate = e.ExpExpenseDate,
                    ProjectId = e.ExpProjectId,
                    ProjectName = e.Project?.PrjName ?? "Unknown",
                    CategoryId = e.ExpCategoryId,
                    CategoryName = e.Category?.CatName ?? "Unknown",
                    OriginalAmount = e.ExpOriginalAmount,
                    OriginalCurrency = e.ExpOriginalCurrency,
                    ConvertedAmount = e.ExpConvertedAmount,
                    ProjectCurrency = e.Project?.PrjCurrencyCode ?? "USD",
                    Description = e.ExpDescription
                })
                .ToList();

            return new PaymentMethodReportRow
            {
                PaymentMethodId = pm.PmtId,
                Name = pm.PmtName,
                Type = pm.PmtType,
                Currency = pm.PmtCurrency,
                BankName = pm.PmtBankName,
                TotalSpent = pmTotal,
                ExpenseCount = pmExpenses.Count,
                Percentage = grandTotal > 0 ? Math.Round(pmTotalConverted / grandTotal * 100, 2) : 0,
                AverageExpenseAmount = pmExpenses.Count > 0
                    ? Math.Round(pmTotal / pmExpenses.Count, 2)
                    : 0,
                FirstUseDate = pmExpenses
                    .OrderBy(e => e.ExpExpenseDate)
                    .Select(e => (DateOnly?)e.ExpExpenseDate)
                    .FirstOrDefault(),
                LastUseDate = lastUsed,
                DaysSinceLastUse = lastUsed.HasValue ? daysSinceLastUse : 0,
                IsInactive = lastUsed.HasValue && daysSinceLastUse > 90,
                TopExpense = topExpenseEntity is null ? null : new PaymentMethodTopExpense
                {
                    ExpenseId = topExpenseEntity.ExpId,
                    Title = topExpenseEntity.ExpTitle,
                    Amount = topExpenseEntity.ExpOriginalAmount,
                    ExpenseDate = topExpenseEntity.ExpExpenseDate,
                    ProjectName = topExpenseEntity.Project?.PrjName ?? "Unknown",
                    CategoryName = topExpenseEntity.Category?.CatName ?? "Unknown"
                },
                TopCategories = topCategories,
                Projects = projects,
                Expenses = expenseRows,
                TotalExpensesInPeriod = pmExpenses.Count,
                ExpensesShown = expenseRows.Count
            };
        })
        .OrderByDescending(pm => pm.TotalSpent)
        .ToList();

        // Tendencia mensual
        var monthlyTrend = ownerExpenses
            .GroupBy(e => new { e.ExpExpenseDate.Year, e.ExpExpenseDate.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g =>
            {
                var monthTotal = g.Sum(e => e.ExpConvertedAmount);
                return new PaymentMethodMonthlyRow
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    MonthLabel = $"{new DateTime(g.Key.Year, g.Key.Month, 1):MMMM yyyy}",
                    TotalSpent = monthTotal,
                    ExpenseCount = g.Count(),
                    ByMethod = g
                        .GroupBy(e => new { e.ExpPaymentMethodId, Name = e.PaymentMethod?.PmtName ?? "Unknown" })
                        .Select(mg =>
                        {
                            var mgTotal = mg.Sum(e => e.ExpConvertedAmount);
                            return new PaymentMethodMonthBreakdown
                            {
                                PaymentMethodId = mg.Key.ExpPaymentMethodId,
                                Name = mg.Key.Name,
                                TotalSpent = mgTotal,
                                ExpenseCount = mg.Count(),
                                Percentage = monthTotal > 0
                                    ? Math.Round(mgTotal / monthTotal * 100, 2)
                                    : 0
                            };
                        })
                        .OrderByDescending(m => m.TotalSpent)
                        .ToList()
                };
            })
            .ToList();

        // Root-level aggregate stats
        var peakTrendMonth = monthlyTrend.OrderByDescending(m => m.TotalSpent).FirstOrDefault();
        var mostUsed = pmRows.OrderByDescending(p => p.ExpenseCount).FirstOrDefault();
        var highestSpend = pmRows.OrderByDescending(p => p.TotalSpent).FirstOrDefault();

        var report = new PaymentMethodReportResponse
        {
            UserId = userId,
            DateFrom = from,
            DateTo = to,
            GeneratedAt = DateTime.UtcNow,
            GrandTotalSpent = grandTotal,
            GrandTotalExpenseCount = ownerExpenses.Count,
            GrandAverageExpenseAmount = ownerExpenses.Count > 0
                ? Math.Round(grandTotal / ownerExpenses.Count, 2)
                : 0,
            AverageMonthlySpend = monthlyTrend.Count > 0
                ? Math.Round(monthlyTrend.Sum(m => m.TotalSpent) / monthlyTrend.Count, 2)
                : 0,
            PeakMonth = peakTrendMonth is null ? null : new PeakMonthInfo
            {
                MonthLabel = peakTrendMonth.MonthLabel,
                Total = peakTrendMonth.TotalSpent
            },
            MostUsedMethod = mostUsed is null ? null : new MethodReference
            {
                PaymentMethodId = mostUsed.PaymentMethodId,
                Name = mostUsed.Name
            },
            HighestSpendMethod = highestSpend is null ? null : new MethodReference
            {
                PaymentMethodId = highestSpend.PaymentMethodId,
                Name = highestSpend.Name
            },
            PaymentMethods = pmRows,
            MonthlyTrend = monthlyTrend
        };

        return format.ToLowerInvariant() switch
        {
            "excel" => ExportExcel(
                _exportService.GeneratePaymentMethodReportExcel(report),
                "payment-method-report"),
            "pdf" => ExportPdf(
                _exportService.GeneratePaymentMethodReportPdf(report),
                "payment-method-report"),
            _ => Ok(report)
        };
    }

    // ── Private Helpers ─────────────────────────────────────

    private FileContentResult ExportExcel(byte[] content, string baseName)
    {
        var safeFileName = SanitizeFileName(baseName);
        return File(content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"{safeFileName}.xlsx");
    }

    private FileContentResult ExportPdf(byte[] content, string baseName)
    {
        var safeFileName = SanitizeFileName(baseName);
        return File(content, "application/pdf", $"{safeFileName}.pdf");
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
    }
}
