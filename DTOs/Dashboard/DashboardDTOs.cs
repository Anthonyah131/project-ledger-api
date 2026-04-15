namespace ProjectLedger.API.DTOs.Dashboard;

/// <summary>
/// Full monthly overview for the dashboard: summary, comparison, trends, categories,
/// payment method split, project health, and alerts — all in the project's currency.
/// </summary>
public class MonthlyOverviewResponse
{
    public string Month { get; set; } = null!;
    public MonthlyNavigationResponse Navigation { get; set; } = new();
    public string CurrencyCode { get; set; } = null!;
    public DateTime GeneratedAt { get; set; }
    public MonthlySummaryResponse Summary { get; set; } = new();
    public MonthlyComparisonResponse Comparison { get; set; } = new();
    public List<DailyTrendPointResponse> TrendByDay { get; set; } = [];
    public List<TopCategoryRowResponse> TopCategories { get; set; } = [];
    public List<PaymentMethodSplitRowResponse> PaymentMethodSplit { get; set; } = [];
    public List<ProjectHealthRowResponse> ProjectHealth { get; set; } = [];
    public List<DashboardAlertResponse> Alerts { get; set; } = [];
}

/// <summary>
/// Compact monthly summary for a specific project (or all projects when <c>ProjectId</c> is null).
/// Returns executive totals, comparison with the previous month, and active alerts.
/// </summary>
public class MonthlySummaryDashboardResponse
{
    public string Month { get; set; } = null!;
    public MonthlyNavigationResponse Navigation { get; set; } = new();
    public string CurrencyCode { get; set; } = null!;
    public Guid? ProjectId { get; set; }
    public DateTime GeneratedAt { get; set; }
    public ExecutiveMonthlySummaryResponse Summary { get; set; } = new();
    public MonthlyComparisonResponse Comparison { get; set; } = new();
    public List<DashboardAlertResponse> Alerts { get; set; } = [];
}

/// <summary>High-level financial totals for a given month: total spent, total income, and net balance.</summary>
public class ExecutiveMonthlySummaryResponse
{
    public decimal TotalSpent { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal NetBalance { get; set; }
}

/// <summary>Daily spending/income trend data for a given month and optional project filter.</summary>
public class MonthlyDailyTrendResponse
{
    public string Month { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;
    public Guid? ProjectId { get; set; }
    public List<DailyTrendPointResponse> TrendByDay { get; set; } = [];
}

/// <summary>Top expense categories for a given month and optional project filter.</summary>
public class MonthlyTopCategoriesResponse
{
    public string Month { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;
    public Guid? ProjectId { get; set; }
    public List<TopCategoryRowResponse> TopCategories { get; set; } = [];
}

/// <summary>Payment method spending breakdown for a given month and optional project filter.</summary>
public class MonthlyPaymentMethodsResponse
{
    public string Month { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;
    public Guid? ProjectId { get; set; }
    public List<PaymentMethodSplitRowResponse> PaymentMethodSplit { get; set; } = [];
}

/// <summary>Navigation context for month-based dashboard views: links to adjacent months and whether data exists.</summary>
public class MonthlyNavigationResponse
{
    public string PreviousMonth { get; set; } = null!;
    public string CurrentMonth { get; set; } = null!;
    public string NextMonth { get; set; } = null!;
    public bool IsCurrentMonth { get; set; }
    public bool HasPreviousData { get; set; }
    public bool HasNextData { get; set; }
}

/// <summary>
/// Extended monthly summary including active project count, pending obligations,
/// and budget utilization percentage — used on the full overview dashboard.
/// </summary>
public class MonthlySummaryResponse
{
    public decimal TotalSpent { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal NetBalance { get; set; }
    public int ActiveProjects { get; set; }
    public int PendingObligationsCount { get; set; }
    public decimal PendingObligationsAmount { get; set; }
    public decimal BudgetUsedPercentage { get; set; }
}

/// <summary>Month-over-month comparison deltas for spending, income, and net balance.</summary>
public class MonthlyComparisonResponse
{
    public string PreviousMonth { get; set; } = null!;
    public decimal SpentDelta { get; set; }
    public decimal SpentDeltaPercentage { get; set; }
    public decimal IncomeDelta { get; set; }
    public decimal IncomeDeltaPercentage { get; set; }
    public decimal NetDelta { get; set; }
}

/// <summary>Aggregated spending and income totals for a single calendar day.</summary>
public class DailyTrendPointResponse
{
    public DateOnly Date { get; set; }
    public decimal Spent { get; set; }
    public decimal Income { get; set; }
    public decimal Net { get; set; }
    public List<Guid> ProjectIds { get; set; } = [];
    public int ExpenseCount { get; set; }
    public int IncomeCount { get; set; }
}

/// <summary>A single row in the top-categories breakdown: category totals and its share of overall spending.</summary>
public class TopCategoryRowResponse
{
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public int ExpenseCount { get; set; }
    public decimal Percentage { get; set; }
    public List<Guid> ProjectIds { get; set; } = [];
}

/// <summary>A single row in the payment-method breakdown: totals per method and its share of overall spending.</summary>
public class PaymentMethodSplitRowResponse
{
    public Guid PaymentMethodId { get; set; }
    public string PaymentMethodName { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public int ExpenseCount { get; set; }
    public decimal Percentage { get; set; }
}

/// <summary>Health snapshot for a single project: net balance and optional budget utilization.</summary>
public class ProjectHealthRowResponse
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public decimal Spent { get; set; }
    public decimal Income { get; set; }
    public decimal Net { get; set; }
    public decimal? Budget { get; set; }
    public decimal? BudgetUsedPercentage { get; set; }
}

/// <summary>
/// A dashboard alert surfaced to the user (e.g. overdue obligations, budget exceeded).
/// Alerts are prioritized and may reference a specific project or payment method.
/// </summary>
public class DashboardAlertResponse
{
    public string Type { get; set; } = null!;
    public string Code { get; set; } = null!;
    public Guid? ProjectId { get; set; }
    public Guid? PaymentMethodId { get; set; }
    public int Priority { get; set; }
    public int Count { get; set; }
}

// ── Dashboard Project Selector ──────────────────────────────

/// <summary>
/// Lightweight project for the dashboard selector.
/// Exposes only the fields needed for the picker UI.
/// </summary>
public record DashboardProjectItemDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public bool IsPinned { get; init; }
    public DateTime? PinnedAt { get; init; }
}

/// <summary>
/// Paginated response from GET /api/dashboard/projects.
/// Page 1: includes pinned projects + non-pinned items.
/// Pages > 1: pinned[] empty, only non-pinned items.
/// </summary>
public record DashboardProjectsPagedResponse
{
    public IReadOnlyList<DashboardProjectItemDto> Pinned { get; init; } = [];
    public IReadOnlyList<DashboardProjectItemDto> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
