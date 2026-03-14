using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Dashboard;
using ProjectLedger.API.Models;
using ProjectLedger.API.Repositories;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Dashboard mensual a nivel usuario (transversal a proyectos visibles).
/// Regla principal: navegacion por mes con formato YYYY-MM.
/// </summary>
[ApiController]
[Route("api/dashboard")]
[Authorize]
[Tags("Dashboard")]
[Produces("application/json")]
public class DashboardController : ControllerBase
{
    private static readonly Regex MonthPattern = new(@"^\d{4}-(0[1-9]|1[0-2])$", RegexOptions.Compiled);

    private readonly IExpenseRepository _expenseRepo;
    private readonly IIncomeRepository _incomeRepo;
    private readonly IObligationRepository _obligationRepo;
    private readonly IProjectBudgetRepository _budgetRepo;
    private readonly IProjectService _projectService;

    public DashboardController(
        IExpenseRepository expenseRepo,
        IIncomeRepository incomeRepo,
        IObligationRepository obligationRepo,
        IProjectBudgetRepository budgetRepo,
        IProjectService projectService)
    {
        _expenseRepo = expenseRepo;
        _incomeRepo = incomeRepo;
        _obligationRepo = obligationRepo;
        _budgetRepo = budgetRepo;
        _projectService = projectService;
    }

    /// <summary>
    /// Devuelve el bloque superior del dashboard mensual (navegación, resumen y alertas).
    /// </summary>
    [HttpGet("monthly-summary")]
    [ProducesResponseType(typeof(MonthlySummaryDashboardResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMonthlySummary(
        [FromQuery] string? month,
        CancellationToken ct = default)
    {
        if (!TryParseMonth(month, out var monthStart))
            return InvalidMonth();

        if (IsAdminUser())
        {
            return Ok(new MonthlySummaryDashboardResponse
            {
                Month = ToMonthKey(monthStart),
                Navigation = BuildNavigation(monthStart, false, false),
                CurrencyCode = "USD",
                GeneratedAt = DateTime.UtcNow,
                Summary = new ExecutiveMonthlySummaryResponse(),
                Comparison = new MonthlyComparisonResponse
                {
                    PreviousMonth = ToMonthKey(monthStart.AddMonths(-1))
                },
                Alerts = []
            });
        }

        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var previousMonthStart = monthStart.AddMonths(-1);
        var previousMonthEnd = previousMonthStart.AddMonths(1).AddDays(-1);
        var nextMonthStart = monthStart.AddMonths(1);
        var nextMonthEnd = nextMonthStart.AddMonths(1).AddDays(-1);
        var scope = await GetVisibleProjectScopeAsync(ct);

        var monthData = await LoadMonthDataAsync(scope.VisibleProjectIds, monthStart, monthEnd, ct);
        var previousData = await LoadMonthDataAsync(scope.VisibleProjectIds, previousMonthStart, previousMonthEnd, ct);
        var nextData = await LoadMonthDataAsync(scope.VisibleProjectIds, nextMonthStart, nextMonthEnd, ct);

        var monthExpenses = monthData.Expenses;
        var monthIncomes = monthData.Incomes;
        var previousExpenses = previousData.Expenses;
        var previousIncomes = previousData.Incomes;

        var totalSpent = monthExpenses.Sum(e => e.ExpConvertedAmount);
        var totalIncome = monthIncomes.Sum(i => i.IncConvertedAmount);
        var netBalance = totalIncome - totalSpent;

        var previousSpent = previousExpenses.Sum(e => e.ExpConvertedAmount);
        var previousIncome = previousIncomes.Sum(i => i.IncConvertedAmount);
        var previousNet = previousIncome - previousSpent;

        var budgetByProject = await LoadBudgetsByProjectAsync(scope.VisibleProjects, ct);
        var projectHealth = BuildProjectHealthRows(scope.VisibleProjects, monthExpenses, monthIncomes, budgetByProject);
        var obligationSummary = await ComputePendingObligationsAsync(scope.VisibleProjects, monthEnd, ct);

        var response = new MonthlySummaryDashboardResponse
        {
            Month = ToMonthKey(monthStart),
            Navigation = BuildNavigation(
                monthStart,
                previousExpenses.Count > 0 || previousIncomes.Count > 0,
                nextMonthStart <= CurrentMonthStart()
                    && (nextData.Expenses.Count > 0 || nextData.Incomes.Count > 0)),
            CurrencyCode = ResolveCurrencyCode(scope.VisibleProjects),
            GeneratedAt = DateTime.UtcNow,
            Summary = new ExecutiveMonthlySummaryResponse
            {
                TotalSpent = totalSpent,
                TotalIncome = totalIncome,
                NetBalance = netBalance
            },
            Comparison = new MonthlyComparisonResponse
            {
                PreviousMonth = ToMonthKey(previousMonthStart),
                SpentDelta = totalSpent - previousSpent,
                SpentDeltaPercentage = ComputeDeltaPercentage(totalSpent - previousSpent, previousSpent),
                IncomeDelta = totalIncome - previousIncome,
                IncomeDeltaPercentage = ComputeDeltaPercentage(totalIncome - previousIncome, previousIncome),
                NetDelta = netBalance - previousNet
            },
            Alerts = BuildAlerts(projectHealth, obligationSummary)
        };

        return Ok(response);
    }

    /// <summary>
    /// Devuelve tendencia diaria de gasto/ingreso para el mes seleccionado.
    /// </summary>
    [HttpGet("monthly-daily-trend")]
    [ProducesResponseType(typeof(MonthlyDailyTrendResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMonthlyDailyTrend(
        [FromQuery] string? month,
        [FromQuery] Guid? projectId,
        CancellationToken ct = default)
    {
        if (!TryParseMonth(month, out var monthStart))
            return InvalidMonth();

        if (IsAdminUser())
        {
            return Ok(new MonthlyDailyTrendResponse
            {
                Month = ToMonthKey(monthStart),
                CurrencyCode = "USD",
                ProjectId = projectId,
                TrendByDay = []
            });
        }

        var scope = await GetVisibleProjectScopeAsync(ct);
        if (!TryResolveProjectScope(scope.VisibleProjectIds, projectId, out var selectedProjectIds))
            return Forbid();

        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var monthData = await LoadMonthDataAsync(selectedProjectIds, monthStart, monthEnd, ct);

        var scopedProjects = scope.VisibleProjects.Where(p => selectedProjectIds.Contains(p.PrjId)).ToList();

        return Ok(new MonthlyDailyTrendResponse
        {
            Month = ToMonthKey(monthStart),
            CurrencyCode = ResolveCurrencyCode(scopedProjects),
            ProjectId = projectId,
            TrendByDay = BuildTrendByDay(monthStart, monthData.Expenses, monthData.Incomes)
        });
    }

    /// <summary>
    /// Devuelve top categorías de gasto del mes (todos los proyectos visibles o proyecto puntual).
    /// </summary>
    [HttpGet("monthly-top-categories")]
    [ProducesResponseType(typeof(MonthlyTopCategoriesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMonthlyTopCategories(
        [FromQuery] string? month,
        [FromQuery] Guid? projectId,
        CancellationToken ct = default)
    {
        if (!TryParseMonth(month, out var monthStart))
            return InvalidMonth();

        if (IsAdminUser())
        {
            return Ok(new MonthlyTopCategoriesResponse
            {
                Month = ToMonthKey(monthStart),
                CurrencyCode = "USD",
                ProjectId = projectId,
                TopCategories = []
            });
        }

        var scope = await GetVisibleProjectScopeAsync(ct);
        if (!TryResolveProjectScope(scope.VisibleProjectIds, projectId, out var selectedProjectIds))
            return Forbid();

        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var monthData = await LoadMonthDataAsync(selectedProjectIds, monthStart, monthEnd, ct);
        var totalSpent = monthData.Expenses.Sum(e => e.ExpConvertedAmount);
        var scopedProjects = scope.VisibleProjects.Where(p => selectedProjectIds.Contains(p.PrjId)).ToList();

        return Ok(new MonthlyTopCategoriesResponse
        {
            Month = ToMonthKey(monthStart),
            CurrencyCode = ResolveCurrencyCode(scopedProjects),
            ProjectId = projectId,
            TopCategories = BuildTopCategories(monthData.Expenses, totalSpent)
        });
    }

    /// <summary>
    /// Devuelve la distribución de gasto mensual por método de pago.
    /// </summary>
    [HttpGet("monthly-payment-methods")]
    [ProducesResponseType(typeof(MonthlyPaymentMethodsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMonthlyPaymentMethods(
        [FromQuery] string? month,
        CancellationToken ct = default)
    {
        if (!TryParseMonth(month, out var monthStart))
            return InvalidMonth();

        if (IsAdminUser())
        {
            return Ok(new MonthlyPaymentMethodsResponse
            {
                Month = ToMonthKey(monthStart),
                CurrencyCode = "USD",
                PaymentMethodSplit = []
            });
        }

        var scope = await GetVisibleProjectScopeAsync(ct);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var monthData = await LoadMonthDataAsync(scope.VisibleProjectIds, monthStart, monthEnd, ct);
        var totalSpent = monthData.Expenses.Sum(e => e.ExpConvertedAmount);

        return Ok(new MonthlyPaymentMethodsResponse
        {
            Month = ToMonthKey(monthStart),
            CurrencyCode = ResolveCurrencyCode(scope.VisibleProjects),
            PaymentMethodSplit = BuildPaymentMethodSplit(monthData.Expenses, totalSpent)
        });
    }

    /// <summary>
    /// Devuelve el overview mensual completo (compatibilidad legado).
    /// </summary>
    /// <param name="month">Mes en formato YYYY-MM.</param>
    /// <response code="200">Overview mensual generado.</response>
    /// <response code="400">Formato de month invalido.</response>
    [HttpGet("monthly-overview")]
    [Obsolete("Deprecated. Use monthly-summary, monthly-daily-trend, monthly-top-categories and monthly-payment-methods.")]
    [ProducesResponseType(typeof(MonthlyOverviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMonthlyOverview(
        [FromQuery] string? month,
        CancellationToken ct = default)
    {
        if (!TryParseMonth(month, out var monthStart))
            return InvalidMonth();

        if (IsAdminUser())
        {
            return Ok(new MonthlyOverviewResponse
            {
                Month = ToMonthKey(monthStart),
                Navigation = BuildNavigation(monthStart, false, false),
                CurrencyCode = "USD",
                GeneratedAt = DateTime.UtcNow,
                Summary = new MonthlySummaryResponse(),
                Comparison = new MonthlyComparisonResponse
                {
                    PreviousMonth = ToMonthKey(monthStart.AddMonths(-1))
                },
                TrendByDay = [],
                TopCategories = [],
                PaymentMethodSplit = [],
                ProjectHealth = [],
                Alerts = []
            });
        }

        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var previousMonthStart = monthStart.AddMonths(-1);
        var previousMonthEnd = previousMonthStart.AddMonths(1).AddDays(-1);
        var nextMonthStart = monthStart.AddMonths(1);
        var nextMonthEnd = nextMonthStart.AddMonths(1).AddDays(-1);

        var scope = await GetVisibleProjectScopeAsync(ct);

        var monthData = await LoadMonthDataAsync(scope.VisibleProjectIds, monthStart, monthEnd, ct);
        var previousData = await LoadMonthDataAsync(scope.VisibleProjectIds, previousMonthStart, previousMonthEnd, ct);
        var nextData = await LoadMonthDataAsync(scope.VisibleProjectIds, nextMonthStart, nextMonthEnd, ct);

        var monthExpenses = monthData.Expenses;
        var monthIncomes = monthData.Incomes;
        var previousExpenses = previousData.Expenses;
        var previousIncomes = previousData.Incomes;

        var totalSpent = monthExpenses.Sum(e => e.ExpConvertedAmount);
        var totalIncome = monthIncomes.Sum(i => i.IncConvertedAmount);
        var netBalance = totalIncome - totalSpent;

        var previousSpent = previousExpenses.Sum(e => e.ExpConvertedAmount);
        var previousIncome = previousIncomes.Sum(i => i.IncConvertedAmount);
        var previousNet = previousIncome - previousSpent;

        var activeProjectIds = monthExpenses
            .Select(e => e.ExpProjectId)
            .Union(monthIncomes.Select(i => i.IncProjectId))
            .Distinct()
            .ToHashSet();

        var budgetByProject = await LoadBudgetsByProjectAsync(scope.VisibleProjects, ct);
        var projectHealth = BuildProjectHealthRows(scope.VisibleProjects, monthExpenses, monthIncomes, budgetByProject);
        var obligationSummary = await ComputePendingObligationsAsync(scope.VisibleProjects, monthEnd, ct);

        var totalBudget = budgetByProject.Values
            .Where(b => b is not null && b.PjbTotalBudget > 0)
            .Sum(b => b!.PjbTotalBudget);

        var budgetUsedPercentage = totalBudget > 0
            ? Math.Round(totalSpent / totalBudget * 100m, 2)
            : 0m;

        var trendByDay = BuildTrendByDay(monthStart, monthExpenses, monthIncomes);
        var topCategories = BuildTopCategories(monthExpenses, totalSpent);
        var paymentMethodSplit = BuildPaymentMethodSplit(monthExpenses, totalSpent);
        var alerts = BuildAlerts(projectHealth, obligationSummary);

        var response = new MonthlyOverviewResponse
        {
            Month = ToMonthKey(monthStart),
            Navigation = BuildNavigation(
                monthStart,
                previousExpenses.Count > 0 || previousIncomes.Count > 0,
                nextMonthStart <= CurrentMonthStart()
                    && (nextData.Expenses.Count > 0 || nextData.Incomes.Count > 0)),
            CurrencyCode = ResolveCurrencyCode(scope.VisibleProjects),
            GeneratedAt = DateTime.UtcNow,
            Summary = new MonthlySummaryResponse
            {
                TotalSpent = totalSpent,
                TotalIncome = totalIncome,
                NetBalance = netBalance,
                ActiveProjects = activeProjectIds.Count,
                PendingObligationsCount = obligationSummary.PendingCount,
                PendingObligationsAmount = obligationSummary.PendingAmount,
                BudgetUsedPercentage = budgetUsedPercentage
            },
            Comparison = new MonthlyComparisonResponse
            {
                PreviousMonth = ToMonthKey(previousMonthStart),
                SpentDelta = totalSpent - previousSpent,
                SpentDeltaPercentage = ComputeDeltaPercentage(totalSpent - previousSpent, previousSpent),
                IncomeDelta = totalIncome - previousIncome,
                IncomeDeltaPercentage = ComputeDeltaPercentage(totalIncome - previousIncome, previousIncome),
                NetDelta = netBalance - previousNet
            },
            TrendByDay = trendByDay,
            TopCategories = topCategories,
            PaymentMethodSplit = paymentMethodSplit,
            ProjectHealth = projectHealth,
            Alerts = alerts
        };

        return Ok(response);
    }

    private async Task<MonthData> LoadMonthDataAsync(
        IReadOnlyCollection<Guid> projectIds,
        DateOnly from,
        DateOnly to,
        CancellationToken ct)
    {
        if (projectIds.Count == 0)
            return new MonthData([], []);

        var expenses = new List<Expense>();
        var incomes = new List<Income>();

        foreach (var projectId in projectIds)
        {
            var projectExpenses = await _expenseRepo.GetDetailedByProjectIdAsync(projectId, from, to, ct);
            expenses.AddRange(projectExpenses);

            var projectIncomes = await _incomeRepo.GetByProjectIdAsync(projectId, ct);
            incomes.AddRange(projectIncomes.Where(i => i.IncIncomeDate >= from && i.IncIncomeDate <= to));
        }

        return new MonthData(expenses, incomes);
    }

    private async Task<Dictionary<Guid, ProjectBudget?>> LoadBudgetsByProjectAsync(
        IReadOnlyCollection<Project> projects,
        CancellationToken ct)
    {
        var result = new Dictionary<Guid, ProjectBudget?>();

        foreach (var project in projects)
        {
            result[project.PrjId] = await _budgetRepo.GetActiveByProjectIdAsync(project.PrjId, ct);
        }

        return result;
    }

    private async Task<ObligationDashboardSummary> ComputePendingObligationsAsync(
        IReadOnlyCollection<Project> projects,
        DateOnly monthEnd,
        CancellationToken ct)
    {
        var obligations = new List<Obligation>();

        foreach (var project in projects)
        {
            var projectObligations = await _obligationRepo.GetByProjectIdWithPaymentsAsync(project.PrjId, ct);
            obligations.AddRange(projectObligations);
        }

        var pendingCount = 0;
        var pendingAmount = 0m;
        var overdueCount = 0;
        var overdueByProject = new Dictionary<Guid, int>();

        foreach (var obligation in obligations)
        {
            var paidUntilMonthEnd = obligation.Payments
                .Where(p => p.ExpExpenseDate <= monthEnd && !p.ExpIsDeleted && p.ExpIsActive)
                .Sum(p => p.ExpOriginalCurrency == obligation.OblCurrency
                    ? p.ExpOriginalAmount
                    : p.ExpObligationEquivalentAmount ?? p.ExpConvertedAmount);

            var remaining = Math.Max(0m, obligation.OblTotalAmount - paidUntilMonthEnd);
            var status = ComputeObligationStatus(obligation, paidUntilMonthEnd, monthEnd);

            if (status != "paid")
            {
                pendingCount++;
                pendingAmount += remaining;
            }

            if (status == "overdue")
            {
                overdueCount++;
                overdueByProject[obligation.OblProjectId] = overdueByProject.GetValueOrDefault(obligation.OblProjectId) + 1;
            }
        }

        var topOverdueProject = overdueByProject
            .OrderByDescending(x => x.Value)
            .Select(x => (Guid?)x.Key)
            .FirstOrDefault();

        return new ObligationDashboardSummary(
            pendingCount,
            pendingAmount,
            overdueCount,
            topOverdueProject,
            overdueByProject);
    }

    private static List<DailyTrendPointResponse> BuildTrendByDay(
        DateOnly monthStart,
        IReadOnlyCollection<Expense> expenses,
        IReadOnlyCollection<Income> incomes)
    {
        var spentByDay = expenses
            .GroupBy(e => e.ExpExpenseDate)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.ExpConvertedAmount));

        var incomeByDay = incomes
            .GroupBy(i => i.IncIncomeDate)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.IncConvertedAmount));

        var daysInMonth = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);
        var trend = new List<DailyTrendPointResponse>(daysInMonth);

        for (var day = 1; day <= daysInMonth; day++)
        {
            var date = new DateOnly(monthStart.Year, monthStart.Month, day);
            var spent = spentByDay.GetValueOrDefault(date, 0m);
            var income = incomeByDay.GetValueOrDefault(date, 0m);
            var dayExpenses = expenses.Where(e => e.ExpExpenseDate == date).ToList();
            var dayIncomes = incomes.Where(i => i.IncIncomeDate == date).ToList();
            var dayProjectIds = dayExpenses
                .Select(e => e.ExpProjectId)
                .Union(dayIncomes.Select(i => i.IncProjectId))
                .Distinct()
                .ToList();

            trend.Add(new DailyTrendPointResponse
            {
                Date = date,
                Spent = spent,
                Income = income,
                Net = income - spent,
                ProjectIds = dayProjectIds,
                ExpenseCount = dayExpenses.Count,
                IncomeCount = dayIncomes.Count
            });
        }

        return trend;
    }

    private static List<TopCategoryRowResponse> BuildTopCategories(
        IReadOnlyCollection<Expense> expenses,
        decimal totalSpent)
    {
        return expenses
            .GroupBy(e => new { e.ExpCategoryId, Name = e.Category?.CatName ?? "Unknown" })
            .Select(g =>
            {
                var groupTotal = g.Sum(e => e.ExpConvertedAmount);
                var projectIds = g.Select(e => e.ExpProjectId).Distinct().ToList();
                return new TopCategoryRowResponse
                {
                    CategoryId = g.Key.ExpCategoryId,
                    CategoryName = g.Key.Name,
                    TotalAmount = groupTotal,
                    ExpenseCount = g.Count(),
                    Percentage = totalSpent > 0
                        ? Math.Round(groupTotal / totalSpent * 100m, 2)
                        : 0m,
                    ProjectIds = projectIds
                };
            })
            .OrderByDescending(c => c.TotalAmount)
            .Take(6)
            .ToList();
    }

    private static List<PaymentMethodSplitRowResponse> BuildPaymentMethodSplit(
        IReadOnlyCollection<Expense> expenses,
        decimal totalSpent)
    {
        return expenses
            .GroupBy(e => new { e.ExpPaymentMethodId, Name = e.PaymentMethod?.PmtName ?? "Unknown" })
            .Select(g =>
            {
                var groupTotal = g.Sum(e => e.ExpConvertedAmount);
                return new PaymentMethodSplitRowResponse
                {
                    PaymentMethodId = g.Key.ExpPaymentMethodId,
                    PaymentMethodName = g.Key.Name,
                    TotalAmount = groupTotal,
                    ExpenseCount = g.Count(),
                    Percentage = totalSpent > 0
                        ? Math.Round(groupTotal / totalSpent * 100m, 2)
                        : 0m
                };
            })
            .OrderByDescending(m => m.TotalAmount)
            .ToList();
    }

    private static List<ProjectHealthRowResponse> BuildProjectHealthRows(
        IReadOnlyCollection<Project> projects,
        IReadOnlyCollection<Expense> expenses,
        IReadOnlyCollection<Income> incomes,
        IReadOnlyDictionary<Guid, ProjectBudget?> budgetByProject)
    {
        var spentByProject = expenses
            .GroupBy(e => e.ExpProjectId)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.ExpConvertedAmount));

        var incomeByProject = incomes
            .GroupBy(i => i.IncProjectId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.IncConvertedAmount));

        return projects
            .Select(project =>
            {
                var spent = spentByProject.GetValueOrDefault(project.PrjId, 0m);
                var income = incomeByProject.GetValueOrDefault(project.PrjId, 0m);
                var budget = budgetByProject.GetValueOrDefault(project.PrjId);

                var budgetUsed = budget is not null && budget.PjbTotalBudget > 0
                    ? Math.Round(spent / budget.PjbTotalBudget * 100m, 2)
                    : (decimal?)null;

                return new ProjectHealthRowResponse
                {
                    ProjectId = project.PrjId,
                    ProjectName = project.PrjName,
                    Spent = spent,
                    Income = income,
                    Net = income - spent,
                    Budget = budget?.PjbTotalBudget,
                    BudgetUsedPercentage = budgetUsed
                };
            })
            .Where(r => r.Spent > 0 || r.Income > 0 || r.Budget is not null)
            .OrderByDescending(r => r.Spent)
            .ToList();
    }

    private static List<DashboardAlertResponse> BuildAlerts(
        IReadOnlyCollection<ProjectHealthRowResponse> projectHealth,
        ObligationDashboardSummary obligationSummary)
    {
        var alerts = new List<DashboardAlertResponse>();

        var projectsOver80 = projectHealth
            .Where(p => p.BudgetUsedPercentage is >= 80m)
            .OrderByDescending(p => p.BudgetUsedPercentage)
            .ToList();

        if (projectsOver80.Count > 0)
        {
            var target = projectsOver80[0];
            alerts.Add(new DashboardAlertResponse
            {
                Type = "warning",
                Code = "BUDGET_OVER_80",
                Message = $"{projectsOver80.Count} proyectos estan sobre 80% de presupuesto",
                ProjectId = target.ProjectId,
                Priority = 80,
                Count = projectsOver80.Count
            });
        }

        if (obligationSummary.OverdueCount > 0)
        {
            var overdueProjectId = obligationSummary.TopOverdueProjectId;
            alerts.Add(new DashboardAlertResponse
            {
                Type = "warning",
                Code = "OVERDUE_OBLIGATION",
                Message = $"Hay {obligationSummary.OverdueCount} obligaciones vencidas",
                ProjectId = overdueProjectId,
                Priority = 90,
                Count = obligationSummary.OverdueCount
            });
        }

        return alerts
            .OrderByDescending(a => a.Priority)
            .ToList();
    }

    private async Task<DashboardProjectScope> GetVisibleProjectScopeAsync(CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var ownedProjects = (await _projectService.GetByOwnerUserIdAsync(userId, ct)).ToList();
        var memberProjects = (await _projectService.GetByMemberUserIdAsync(userId, ct)).ToList();

        var visibleProjects = ownedProjects
            .Union(memberProjects, new ProjectIdComparer())
            .ToList();

        return new DashboardProjectScope(
            visibleProjects,
            visibleProjects.Select(p => p.PrjId).ToHashSet());
    }

    private static bool TryResolveProjectScope(
        HashSet<Guid> visibleProjectIds,
        Guid? requestedProjectId,
        out List<Guid> selectedProjectIds)
    {
        selectedProjectIds = [];

        if (requestedProjectId.HasValue)
        {
            if (!visibleProjectIds.Contains(requestedProjectId.Value))
                return false;

            selectedProjectIds.Add(requestedProjectId.Value);
            return true;
        }

        selectedProjectIds = visibleProjectIds.ToList();
        return true;
    }

    private IActionResult InvalidMonth()
    {
        return BadRequest(new
        {
            error = new
            {
                code = "INVALID_MONTH",
                message = "month must use YYYY-MM format"
            }
        });
    }

    private static MonthlyNavigationResponse BuildNavigation(
        DateOnly monthStart,
        bool hasPreviousData,
        bool hasNextData)
    {
        var previousMonthStart = monthStart.AddMonths(-1);
        var nextMonthStart = monthStart.AddMonths(1);
        var currentMonthStart = CurrentMonthStart();

        return new MonthlyNavigationResponse
        {
            PreviousMonth = ToMonthKey(previousMonthStart),
            CurrentMonth = ToMonthKey(monthStart),
            NextMonth = ToMonthKey(nextMonthStart),
            IsCurrentMonth = monthStart.Year == currentMonthStart.Year && monthStart.Month == currentMonthStart.Month,
            HasPreviousData = hasPreviousData,
            HasNextData = hasNextData
        };
    }

    private static DateOnly CurrentMonthStart()
        => new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

    private bool IsAdminUser()
    {
        var isAdminClaim = User.FindFirst("is_admin")?.Value;
        return bool.TryParse(isAdminClaim, out var isAdmin) && isAdmin;
    }

    private static bool TryParseMonth(string? month, out DateOnly monthStart)
    {
        monthStart = default;

        if (string.IsNullOrWhiteSpace(month) || !MonthPattern.IsMatch(month))
            return false;

        return DateOnly.TryParseExact(
            $"{month}-01",
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out monthStart);
    }

    private static decimal ComputeDeltaPercentage(decimal delta, decimal previous)
        => previous == 0m ? 0m : Math.Round(delta / previous * 100m, 2);

    private static string ResolveCurrencyCode(IReadOnlyCollection<Project> projects)
    {
        return projects
            .Where(p => !string.IsNullOrWhiteSpace(p.PrjCurrencyCode))
            .GroupBy(p => p.PrjCurrencyCode)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Select(g => g.Key)
            .FirstOrDefault()
            ?? "USD";
    }

    private static string ComputeObligationStatus(Obligation obligation, decimal paidAmount, DateOnly referenceDate)
    {
        if (paidAmount >= obligation.OblTotalAmount)
            return "paid";

        if (obligation.OblDueDate.HasValue
            && obligation.OblDueDate.Value < referenceDate
            && paidAmount < obligation.OblTotalAmount)
            return "overdue";

        if (paidAmount > 0m)
            return "partially_paid";

        return "open";
    }

    private static string ToMonthKey(DateOnly date)
        => date.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    private sealed class ProjectIdComparer : IEqualityComparer<Project>
    {
        public bool Equals(Project? x, Project? y) => x?.PrjId == y?.PrjId;
        public int GetHashCode(Project obj) => obj.PrjId.GetHashCode();
    }

    private sealed record DashboardProjectScope(
        List<Project> VisibleProjects,
        HashSet<Guid> VisibleProjectIds);

    private sealed record MonthData(List<Expense> Expenses, List<Income> Incomes);
    private sealed record ObligationDashboardSummary(
        int PendingCount,
        decimal PendingAmount,
        int OverdueCount,
        Guid? TopOverdueProjectId,
        IReadOnlyDictionary<Guid, int> OverdueCountByProject);
}