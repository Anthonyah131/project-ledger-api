using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Report;
using ProjectLedger.API.Repositories;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Reportes e insights por proyecto.
/// 
/// Todos los cálculos se realizan con lógica de aplicación sobre los datos existentes.
/// No se usan APIs externas.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/reports")]
[Authorize]
[Tags("Reports & Insights")]
[Produces("application/json")]
public class ReportController : ControllerBase
{
    private readonly IExpenseRepository _expenseRepo;
    private readonly IObligationRepository _obligationRepo;
    private readonly ICategoryRepository _categoryRepo;
    private readonly IProjectBudgetRepository _budgetRepo;
    private readonly IProjectService _projectService;
    private readonly IProjectAccessService _accessService;
    private readonly IPlanAuthorizationService _planAuth;
    private readonly IReportExportService _exportService;

    public ReportController(
        IExpenseRepository expenseRepo,
        IObligationRepository obligationRepo,
        ICategoryRepository categoryRepo,
        IProjectBudgetRepository budgetRepo,
        IProjectService projectService,
        IProjectAccessService accessService,
        IPlanAuthorizationService planAuth,
        IReportExportService exportService)
    {
        _expenseRepo = expenseRepo;
        _obligationRepo = obligationRepo;
        _categoryRepo = categoryRepo;
        _budgetRepo = budgetRepo;
        _projectService = projectService;
        _accessService = accessService;
        _planAuth = planAuth;
        _exportService = exportService;
    }

    // ── GET /api/projects/{projectId}/reports/summary ───────

    /// <summary>
    /// Resumen financiero del proyecto con desglose por categoría y método de pago.
    /// Soporta filtro opcional por rango de fechas.
    /// </summary>
    /// <response code="200">Resumen del proyecto.</response>
    [HttpGet("summary")]
    [Authorize(Policy = "ProjectViewer")]
    [ProducesResponseType(typeof(ProjectReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSummary(
        Guid projectId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        var project = await _projectService.GetByIdAsync(projectId, ct);
        if (project is null)
            return NotFound(new { message = "Project not found." });

        var expenses = (await _expenseRepo.GetByProjectIdWithDetailsAsync(projectId, ct))
            .Where(e => !e.ExpIsTemplate)
            .Where(e => from is null || e.ExpExpenseDate >= from.Value)
            .Where(e => to is null || e.ExpExpenseDate <= to.Value)
            .ToList();

        var totalSpent = expenses.Sum(e => e.ExpConvertedAmount);

        var byCategory = expenses
            .GroupBy(e => new { e.ExpCategoryId, Name = e.Category?.CatName ?? "Unknown" })
            .Select(g =>
            {
                var groupTotal = g.Sum(e => e.ExpConvertedAmount);
                var count = g.Count();
                return new CategoryBreakdown
                {
                    CategoryId = g.Key.ExpCategoryId,
                    CategoryName = g.Key.Name,
                    TotalAmount = groupTotal,
                    ExpenseCount = count,
                    Percentage = totalSpent > 0 ? Math.Round(groupTotal / totalSpent * 100, 2) : 0,
                    AverageAmount = count > 0 ? Math.Round(groupTotal / count, 2) : 0
                };
            })
            .OrderByDescending(c => c.TotalAmount)
            .ToList();

        var byPaymentMethod = expenses
            .GroupBy(e => new { e.ExpPaymentMethodId, Name = e.PaymentMethod?.PmtName ?? "Unknown" })
            .Select(g =>
            {
                var groupTotal = g.Sum(e => e.ExpConvertedAmount);
                var count = g.Count();
                return new PaymentMethodBreakdown
                {
                    PaymentMethodId = g.Key.ExpPaymentMethodId,
                    PaymentMethodName = g.Key.Name,
                    TotalAmount = groupTotal,
                    ExpenseCount = count,
                    Percentage = totalSpent > 0 ? Math.Round(groupTotal / totalSpent * 100, 2) : 0,
                    AverageAmount = count > 0 ? Math.Round(groupTotal / count, 2) : 0
                };
            })
            .OrderByDescending(p => p.TotalAmount)
            .ToList();

        var topExpenseEntity = expenses
            .OrderByDescending(e => e.ExpConvertedAmount)
            .FirstOrDefault();

        var summaryResponse = new ProjectReportResponse
        {
            ProjectId = project.PrjId,
            ProjectName = project.PrjName,
            CurrencyCode = project.PrjCurrencyCode,
            DateFrom = from,
            DateTo = to,
            GeneratedAt = DateTime.UtcNow,
            TotalSpent = totalSpent,
            ExpenseCount = expenses.Count,
            AverageExpenseAmount = expenses.Count > 0 ? Math.Round(totalSpent / expenses.Count, 2) : 0,
            TopExpense = topExpenseEntity is null ? null : new TopExpenseInfo
            {
                ExpenseId = topExpenseEntity.ExpId,
                Title = topExpenseEntity.ExpTitle,
                Amount = topExpenseEntity.ExpConvertedAmount,
                CategoryName = topExpenseEntity.Category?.CatName ?? "Unknown",
                ExpenseDate = topExpenseEntity.ExpExpenseDate
            },
            ByCategory = byCategory,
            ByPaymentMethod = byPaymentMethod
        };

        // Budget context (optional — when project has an active budget)
        var budget = await _budgetRepo.GetActiveByProjectIdAsync(projectId, ct);
        if (budget is not null)
        {
            summaryResponse.Budget = budget.PjbTotalBudget;
            summaryResponse.BudgetUsedPercentage = budget.PjbTotalBudget > 0
                ? Math.Round(totalSpent / budget.PjbTotalBudget * 100, 2)
                : null;
        }

        // Advanced plan: obligation vs. regular split
        var hasAdvanced = await _planAuth.HasPermissionAsync(
            project.PrjOwnerUserId, PlanPermission.CanUseAdvancedReports, ct);
        if (hasAdvanced)
        {
            summaryResponse.ObligationSpent = expenses
                .Where(e => e.ExpObligationId.HasValue)
                .Sum(e => e.ExpConvertedAmount);
            summaryResponse.RegularSpent = expenses
                .Where(e => !e.ExpObligationId.HasValue)
                .Sum(e => e.ExpConvertedAmount);
        }

        return Ok(summaryResponse);
    }

    // ── GET /api/projects/{projectId}/reports/month-comparison

    /// <summary>
    /// Compara el gasto del mes actual vs el mes anterior.
    /// </summary>
    /// <response code="200">Comparación mensual.</response>
    /// <response code="403">Plan no permite reportes avanzados.</response>
    [HttpGet("month-comparison")]
    [Authorize(Policy = "ProjectViewer")]
    [ProducesResponseType(typeof(MonthComparisonResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMonthComparison(Guid projectId, CancellationToken ct)
    {
        // Reportes avanzados requieren plan del owner del proyecto
        var project = await _projectService.GetByIdAsync(projectId, ct);
        if (project is null)
            return NotFound(new { message = "Project not found." });

        await _planAuth.ValidatePermissionAsync(
            project.PrjOwnerUserId, PlanPermission.CanUseAdvancedReports, ct);

        var now = DateTime.UtcNow;
        var currentMonth = new DateOnly(now.Year, now.Month, 1);
        var previousMonth = currentMonth.AddMonths(-1);

        var expenses = (await _expenseRepo.GetByProjectIdWithDetailsAsync(projectId, ct))
            .Where(e => !e.ExpIsTemplate)
            .ToList();

        var currentExpenses = expenses
            .Where(e => e.ExpExpenseDate.Year == currentMonth.Year
                     && e.ExpExpenseDate.Month == currentMonth.Month)
            .ToList();

        var previousExpenses = expenses
            .Where(e => e.ExpExpenseDate.Year == previousMonth.Year
                     && e.ExpExpenseDate.Month == previousMonth.Month)
            .ToList();

        var currentTotal = currentExpenses.Sum(e => e.ExpConvertedAmount);
        var previousTotal = previousExpenses.Sum(e => e.ExpConvertedAmount);
        var change = currentTotal - previousTotal;

        return Ok(new MonthComparisonResponse
        {
            ProjectId = projectId,
            ProjectName = project.PrjName,
            CurrencyCode = project.PrjCurrencyCode,
            GeneratedAt = DateTime.UtcNow,
            CurrentMonth = new MonthSummary
            {
                Year = currentMonth.Year,
                Month = currentMonth.Month,
                MonthLabel = $"{new DateTime(currentMonth.Year, currentMonth.Month, 1):MMMM yyyy}",
                TotalSpent = currentTotal,
                ExpenseCount = currentExpenses.Count
            },
            PreviousMonth = new MonthSummary
            {
                Year = previousMonth.Year,
                Month = previousMonth.Month,
                MonthLabel = $"{new DateTime(previousMonth.Year, previousMonth.Month, 1):MMMM yyyy}",
                TotalSpent = previousTotal,
                ExpenseCount = previousExpenses.Count
            },
            ChangeAmount = change,
            ChangePercentage = previousTotal > 0
                ? Math.Round(change / previousTotal * 100, 2)
                : null,
            HasPreviousData = previousExpenses.Count > 0
        });
    }

    // ── GET /api/projects/{projectId}/reports/category-growth

    /// <summary>
    /// Identifica las categorías con mayor crecimiento comparando mes actual vs anterior.
    /// Ordenado por mayor crecimiento absoluto.
    /// </summary>
    /// <response code="200">Lista de categorías con crecimiento.</response>
    /// <response code="403">Plan no permite reportes avanzados.</response>
    [HttpGet("category-growth")]
    [Authorize(Policy = "ProjectViewer")]
    [ProducesResponseType(typeof(CategoryGrowthEnvelopeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCategoryGrowth(Guid projectId, CancellationToken ct)
    {
        // Reportes avanzados requieren plan del owner del proyecto
        var project = await _projectService.GetByIdAsync(projectId, ct);
        if (project is null)
            return NotFound(new { message = "Project not found." });

        await _planAuth.ValidatePermissionAsync(
            project.PrjOwnerUserId, PlanPermission.CanUseAdvancedReports, ct);

        var now = DateTime.UtcNow;
        var currentMonth = new DateOnly(now.Year, now.Month, 1);
        var previousMonth = currentMonth.AddMonths(-1);

        var expenses = (await _expenseRepo.GetByProjectIdWithDetailsAsync(projectId, ct))
            .Where(e => !e.ExpIsTemplate)
            .ToList();

        var currentByCategory = expenses
            .Where(e => e.ExpExpenseDate.Year == currentMonth.Year
                     && e.ExpExpenseDate.Month == currentMonth.Month)
            .GroupBy(e => new { e.ExpCategoryId, Name = e.Category?.CatName ?? "Unknown" })
            .ToDictionary(g => g.Key, g => g.ToList());

        var previousByCategory = expenses
            .Where(e => e.ExpExpenseDate.Year == previousMonth.Year
                     && e.ExpExpenseDate.Month == previousMonth.Month)
            .GroupBy(e => new { e.ExpCategoryId, Name = e.Category?.CatName ?? "Unknown" })
            .ToDictionary(g => g.Key, g => g.ToList());

        // Merge all categories from both months
        var allCategories = currentByCategory.Keys
            .Union(previousByCategory.Keys)
            .ToList();

        var growth = allCategories
            .Select(cat =>
            {
                var currentItems = currentByCategory.GetValueOrDefault(cat) ?? [];
                var previousItems = previousByCategory.GetValueOrDefault(cat) ?? [];
                var current = currentItems.Sum(e => e.ExpConvertedAmount);
                var previous = previousItems.Sum(e => e.ExpConvertedAmount);
                var diff = current - previous;

                return new CategoryGrowthResponse
                {
                    CategoryId = cat.ExpCategoryId,
                    CategoryName = cat.Name,
                    CurrentMonthAmount = current,
                    PreviousMonthAmount = previous,
                    GrowthAmount = diff,
                    GrowthPercentage = previous > 0
                        ? Math.Round(diff / previous * 100, 2)
                        : null,
                    CurrentMonthCount = currentItems.Count,
                    PreviousMonthCount = previousItems.Count,
                    AverageAmountCurrent = currentItems.Count > 0
                        ? Math.Round(current / currentItems.Count, 2)
                        : 0,
                    AverageAmountPrevious = previousItems.Count > 0
                        ? Math.Round(previous / previousItems.Count, 2)
                        : 0,
                    IsNew = previous == 0 && current > 0,
                    IsDisappeared = current == 0 && previous > 0
                };
            })
            .OrderByDescending(g => g.GrowthAmount)
            .ToList();

        return Ok(new CategoryGrowthEnvelopeResponse
        {
            ProjectId = projectId,
            ProjectName = project.PrjName,
            CurrencyCode = project.PrjCurrencyCode,
            CurrentMonthLabel = $"{new DateTime(currentMonth.Year, currentMonth.Month, 1):MMMM yyyy}",
            PreviousMonthLabel = $"{new DateTime(previousMonth.Year, previousMonth.Month, 1):MMMM yyyy}",
            GeneratedAt = DateTime.UtcNow,
            Categories = growth
        });
    }

    // ── GET /api/projects/{projectId}/reports/expenses ──────

    /// <summary>
    /// Reporte detallado de gastos del proyecto con secciones mensuales.
    /// Basic: líneas de gastos + totales.
    /// Premium: + análisis de categorías/presupuestos + obligaciones.
    /// Solo el dueño del proyecto puede generar este reporte.
    /// </summary>
    /// <param name="format">Formato de exportación: json (default), excel, pdf.</param>
    /// <response code="200">Reporte generado.</response>
    /// <response code="403">No es dueño del proyecto o plan insuficiente.</response>
    [HttpGet("expenses")]
    [Authorize(Policy = "ProjectViewer")]
    [ProducesResponseType(typeof(DetailedExpenseReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetailedExpenses(
        Guid projectId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string format = "json",
        CancellationToken ct = default)
    {
        var project = await _projectService.GetByIdAsync(projectId, ct);
        if (project is null)
            return NotFound(new { message = "Project not found." });

        // Solo el dueño del proyecto puede generar reportes
        var userId = User.GetRequiredUserId();
        if (project.PrjOwnerUserId != userId)
            return Forbid();

        // Verificar permiso de exportación de datos
        await _planAuth.ValidatePermissionAsync(userId, PlanPermission.CanExportData, ct);

        // PDF requiere CanUseAdvancedReports
        if (format.Equals("pdf", StringComparison.OrdinalIgnoreCase))
            await _planAuth.ValidatePermissionAsync(userId, PlanPermission.CanUseAdvancedReports, ct);

        // Cargar gastos detallados
        var expenses = (await _expenseRepo.GetDetailedByProjectIdAsync(projectId, from, to, ct)).ToList();

        var totalSpent = expenses.Sum(e => e.ExpConvertedAmount);

        // Construir secciones mensuales (oldest → newest)
        var sections = expenses
            .GroupBy(e => new { e.ExpExpenseDate.Year, e.ExpExpenseDate.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g =>
            {
                var sectionTotal = g.Sum(e => e.ExpConvertedAmount);
                var sectionCount = g.Count();
                var topInSection = g.OrderByDescending(e => e.ExpConvertedAmount).First();
                return new MonthlyExpenseSection
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    MonthLabel = $"{new DateTime(g.Key.Year, g.Key.Month, 1):MMMM yyyy}",
                    SectionTotal = sectionTotal,
                    SectionCount = sectionCount,
                    PercentageOfTotal = totalSpent > 0
                        ? Math.Round(sectionTotal / totalSpent * 100, 2)
                        : 0,
                    AverageExpenseAmount = sectionCount > 0
                        ? Math.Round(sectionTotal / sectionCount, 2)
                        : 0,
                    TopExpense = new SectionTopExpense
                    {
                        Title = topInSection.ExpTitle,
                        Amount = topInSection.ExpConvertedAmount
                    },
                    Expenses = g.OrderBy(e => e.ExpExpenseDate).Select(e => new DetailedExpenseRow
                    {
                        Id = e.ExpId,
                        Title = e.ExpTitle,
                        ExpenseDate = e.ExpExpenseDate,
                        CategoryId = e.ExpCategoryId,
                        CategoryName = e.Category?.CatName ?? "Unknown",
                        PaymentMethodId = e.ExpPaymentMethodId,
                        PaymentMethodName = e.PaymentMethod?.PmtName ?? "Unknown",
                        PaymentMethodType = e.PaymentMethod?.PmtType ?? "unknown",
                        OriginalAmount = e.ExpOriginalAmount,
                        OriginalCurrency = e.ExpOriginalCurrency,
                        ExchangeRate = e.ExpExchangeRate,
                        ConvertedAmount = e.ExpConvertedAmount,
                        AltAmount = e.ExpAltAmount,
                        AltCurrency = e.ExpAltCurrency,
                        Description = e.ExpDescription,
                        ReceiptNumber = e.ExpReceiptNumber,
                        Notes = e.ExpNotes,
                        IsObligationPayment = e.ExpObligationId.HasValue,
                        ObligationId = e.ExpObligationId,
                        ObligationTitle = e.Obligation?.OblTitle
                    }).ToList()
                };
            }).ToList();

        // Root-level stats
        var largestExpense = expenses.OrderByDescending(e => e.ExpConvertedAmount).FirstOrDefault();
        var peakSection = sections.OrderByDescending(s => s.SectionTotal).FirstOrDefault();

        var report = new DetailedExpenseReportResponse
        {
            ProjectId = project.PrjId,
            ProjectName = project.PrjName,
            CurrencyCode = project.PrjCurrencyCode,
            DateFrom = from,
            DateTo = to,
            GeneratedAt = DateTime.UtcNow,
            TotalSpent = totalSpent,
            TotalExpenseCount = expenses.Count,
            AverageExpenseAmount = expenses.Count > 0 ? Math.Round(totalSpent / expenses.Count, 2) : 0,
            AverageMonthlySpend = sections.Count > 0
                ? Math.Round(sections.Sum(s => s.SectionTotal) / sections.Count, 2)
                : 0,
            PeakMonth = peakSection is null ? null : new PeakMonthInfo
            {
                MonthLabel = peakSection.MonthLabel,
                Total = peakSection.SectionTotal
            },
            LargestExpense = largestExpense is null ? null : new LargestExpenseInfo
            {
                ExpenseId = largestExpense.ExpId,
                Title = largestExpense.ExpTitle,
                Amount = largestExpense.ExpConvertedAmount,
                ExpenseDate = largestExpense.ExpExpenseDate,
                CategoryName = largestExpense.Category?.CatName ?? "Unknown",
                PaymentMethodName = largestExpense.PaymentMethod?.PmtName ?? "Unknown"
            },
            Sections = sections
        };

        // Secciones avanzadas solo si el plan lo permite
        var hasAdvanced = await _planAuth.HasPermissionAsync(
            userId, PlanPermission.CanUseAdvancedReports, ct);

        if (hasAdvanced)
        {
            // Análisis por categoría con presupuesto
            var categories = await _categoryRepo.GetByProjectIdAsync(projectId, ct);
            report.CategoryAnalysis = categories.Select(cat =>
            {
                var catExpenses = expenses.Where(e => e.ExpCategoryId == cat.CatId).ToList();
                var spent = catExpenses.Sum(e => e.ExpConvertedAmount);

                return new CategoryAnalysisRow
                {
                    CategoryId = cat.CatId,
                    CategoryName = cat.CatName,
                    IsDefault = cat.CatIsDefault,
                    BudgetAmount = cat.CatBudgetAmount,
                    SpentAmount = spent,
                    ExpenseCount = catExpenses.Count,
                    Percentage = totalSpent > 0 ? Math.Round(spent / totalSpent * 100, 2) : 0,
                    BudgetRemaining = cat.CatBudgetAmount.HasValue ? cat.CatBudgetAmount.Value - spent : null,
                    BudgetUsedPercentage = cat.CatBudgetAmount is > 0
                        ? Math.Round(spent / cat.CatBudgetAmount.Value * 100, 2)
                        : null,
                    BudgetExceeded = cat.CatBudgetAmount.HasValue ? spent > cat.CatBudgetAmount.Value : null
                };
            })
            .OrderByDescending(c => c.SpentAmount)
            .ToList();

            // Análisis por método de pago
            report.PaymentMethodAnalysis = expenses
                .GroupBy(e => new
                {
                    e.ExpPaymentMethodId,
                    Name = e.PaymentMethod?.PmtName ?? "Unknown",
                    Type = e.PaymentMethod?.PmtType ?? "unknown"
                })
                .Select(g =>
                {
                    var pmTotal = g.Sum(e => e.ExpConvertedAmount);
                    var pmCount = g.Count();
                    return new PaymentMethodAnalysisRow
                    {
                        PaymentMethodId = g.Key.ExpPaymentMethodId,
                        PaymentMethodName = g.Key.Name,
                        Type = g.Key.Type,
                        SpentAmount = pmTotal,
                        ExpenseCount = pmCount,
                        Percentage = totalSpent > 0 ? Math.Round(pmTotal / totalSpent * 100, 2) : 0,
                        AverageExpenseAmount = pmCount > 0 ? Math.Round(pmTotal / pmCount, 2) : 0
                    };
                })
                .OrderByDescending(r => r.SpentAmount)
                .ToList();

            // Resumen de obligaciones
            var obligations = (await _obligationRepo.GetByProjectIdWithPaymentsAsync(projectId, ct)).ToList();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var oblRows = obligations.Select(o =>
            {
                var paid = o.Payments.Sum(p => p.ExpConvertedAmount);
                var remaining = o.OblTotalAmount - paid;
                var status = ComputeObligationStatus(o, paid, today);

                return new ObligationReportRow
                {
                    OblId = o.OblId,
                    Title = o.OblTitle,
                    Description = o.OblDescription,
                    TotalAmount = o.OblTotalAmount,
                    PaidAmount = paid,
                    RemainingAmount = remaining,
                    Currency = o.OblCurrency,
                    DueDate = o.OblDueDate,
                    Status = status,
                    PaymentCount = o.Payments.Count,
                    LastPaymentDate = o.Payments
                        .OrderByDescending(p => p.ExpExpenseDate)
                        .Select(p => (DateOnly?)p.ExpExpenseDate)
                        .FirstOrDefault()
                };
            }).ToList();

            var byStatus = oblRows
                .GroupBy(o => o.Status)
                .Select(g => new ObligationStatusGroup
                {
                    Status = g.Key,
                    Count = g.Count(),
                    TotalAmount = g.Sum(o => o.TotalAmount),
                    TotalPaid = g.Sum(o => o.PaidAmount),
                    Obligations = g.OrderByDescending(o => o.TotalAmount).ToList()
                })
                .OrderBy(g => g.Status switch
                {
                    "overdue" => 0, "open" => 1, "partially_paid" => 2, "paid" => 3, _ => 4
                })
                .ToList();

            report.ObligationSummary = new ObligationSummarySection
            {
                TotalObligations = obligations.Count,
                TotalAmount = oblRows.Sum(o => o.TotalAmount),
                TotalPaid = oblRows.Sum(o => o.PaidAmount),
                TotalPending = oblRows.Sum(o => o.RemainingAmount),
                OverdueCount = oblRows.Count(o => o.Status == "overdue"),
                OverdueAmount = oblRows.Where(o => o.Status == "overdue").Sum(o => o.RemainingAmount),
                ByStatus = byStatus
            };
        }

        // Exportar según formato
        return format.ToLowerInvariant() switch
        {
            "excel" => ExportExcel(
                _exportService.GenerateExpenseReportExcel(report),
                $"expense-report-{project.PrjName}"),
            "pdf" => ExportPdf(
                _exportService.GenerateExpenseReportPdf(report),
                $"expense-report-{project.PrjName}"),
            _ => Ok(report)
        };
    }

    // ── Private Helpers ─────────────────────────────────────

    private static string ComputeObligationStatus(Models.Obligation o, decimal paid, DateOnly today)
    {
        if (paid >= o.OblTotalAmount) return "paid";
        if (o.OblDueDate.HasValue && o.OblDueDate.Value < today && paid < o.OblTotalAmount) return "overdue";
        if (paid > 0) return "partially_paid";
        return "open";
    }

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
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
    }
}
