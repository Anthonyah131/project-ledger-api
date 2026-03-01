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
    private readonly IProjectService _projectService;
    private readonly IProjectAccessService _accessService;
    private readonly IPlanAuthorizationService _planAuth;

    public ReportController(
        IExpenseRepository expenseRepo,
        IProjectService projectService,
        IProjectAccessService accessService,
        IPlanAuthorizationService planAuth)
    {
        _expenseRepo = expenseRepo;
        _projectService = projectService;
        _accessService = accessService;
        _planAuth = planAuth;
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
            .Select(g => new CategoryBreakdown
            {
                CategoryId = g.Key.ExpCategoryId,
                CategoryName = g.Key.Name,
                TotalAmount = g.Sum(e => e.ExpConvertedAmount),
                ExpenseCount = g.Count(),
                Percentage = totalSpent > 0
                    ? Math.Round(g.Sum(e => e.ExpConvertedAmount) / totalSpent * 100, 2)
                    : 0
            })
            .OrderByDescending(c => c.TotalAmount)
            .ToList();

        var byPaymentMethod = expenses
            .GroupBy(e => new { e.ExpPaymentMethodId, Name = e.PaymentMethod?.PmtName ?? "Unknown" })
            .Select(g => new PaymentMethodBreakdown
            {
                PaymentMethodId = g.Key.ExpPaymentMethodId,
                PaymentMethodName = g.Key.Name,
                TotalAmount = g.Sum(e => e.ExpConvertedAmount),
                ExpenseCount = g.Count(),
                Percentage = totalSpent > 0
                    ? Math.Round(g.Sum(e => e.ExpConvertedAmount) / totalSpent * 100, 2)
                    : 0
            })
            .OrderByDescending(p => p.TotalAmount)
            .ToList();

        return Ok(new ProjectReportResponse
        {
            ProjectId = project.PrjId,
            ProjectName = project.PrjName,
            CurrencyCode = project.PrjCurrencyCode,
            TotalSpent = totalSpent,
            ExpenseCount = expenses.Count,
            ByCategory = byCategory,
            ByPaymentMethod = byPaymentMethod
        });
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
            CurrentMonth = new MonthSummary
            {
                Year = currentMonth.Year,
                Month = currentMonth.Month,
                TotalSpent = currentTotal,
                ExpenseCount = currentExpenses.Count
            },
            PreviousMonth = new MonthSummary
            {
                Year = previousMonth.Year,
                Month = previousMonth.Month,
                TotalSpent = previousTotal,
                ExpenseCount = previousExpenses.Count
            },
            ChangeAmount = change,
            ChangePercentage = previousTotal > 0
                ? Math.Round(change / previousTotal * 100, 2)
                : null
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
    [ProducesResponseType(typeof(IEnumerable<CategoryGrowthResponse>), StatusCodes.Status200OK)]
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
            .ToDictionary(g => g.Key, g => g.Sum(e => e.ExpConvertedAmount));

        var previousByCategory = expenses
            .Where(e => e.ExpExpenseDate.Year == previousMonth.Year
                     && e.ExpExpenseDate.Month == previousMonth.Month)
            .GroupBy(e => new { e.ExpCategoryId, Name = e.Category?.CatName ?? "Unknown" })
            .ToDictionary(g => g.Key, g => g.Sum(e => e.ExpConvertedAmount));

        // Merge all categories from both months
        var allCategories = currentByCategory.Keys
            .Union(previousByCategory.Keys)
            .ToList();

        var growth = allCategories
            .Select(cat =>
            {
                var current = currentByCategory.GetValueOrDefault(cat);
                var previous = previousByCategory.GetValueOrDefault(cat);
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
                        : null
                };
            })
            .OrderByDescending(g => g.GrowthAmount)
            .ToList();

        return Ok(growth);
    }
}
