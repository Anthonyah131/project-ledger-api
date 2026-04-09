namespace ProjectLedger.API.DTOs.Dashboard;

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

public class ExecutiveMonthlySummaryResponse
{
    public decimal TotalSpent { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal NetBalance { get; set; }
}

public class MonthlyDailyTrendResponse
{
    public string Month { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;
    public Guid? ProjectId { get; set; }
    public List<DailyTrendPointResponse> TrendByDay { get; set; } = [];
}

public class MonthlyTopCategoriesResponse
{
    public string Month { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;
    public Guid? ProjectId { get; set; }
    public List<TopCategoryRowResponse> TopCategories { get; set; } = [];
}

public class MonthlyPaymentMethodsResponse
{
    public string Month { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;
    public Guid? ProjectId { get; set; }
    public List<PaymentMethodSplitRowResponse> PaymentMethodSplit { get; set; } = [];
}

public class MonthlyNavigationResponse
{
    public string PreviousMonth { get; set; } = null!;
    public string CurrentMonth { get; set; } = null!;
    public string NextMonth { get; set; } = null!;
    public bool IsCurrentMonth { get; set; }
    public bool HasPreviousData { get; set; }
    public bool HasNextData { get; set; }
}

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

public class MonthlyComparisonResponse
{
    public string PreviousMonth { get; set; } = null!;
    public decimal SpentDelta { get; set; }
    public decimal SpentDeltaPercentage { get; set; }
    public decimal IncomeDelta { get; set; }
    public decimal IncomeDeltaPercentage { get; set; }
    public decimal NetDelta { get; set; }
}

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

public class TopCategoryRowResponse
{
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public int ExpenseCount { get; set; }
    public decimal Percentage { get; set; }
    public List<Guid> ProjectIds { get; set; } = [];
}

public class PaymentMethodSplitRowResponse
{
    public Guid PaymentMethodId { get; set; }
    public string PaymentMethodName { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public int ExpenseCount { get; set; }
    public decimal Percentage { get; set; }
}

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
/// Proyecto ligero para el selector del dashboard.
/// Solo expone los campos necesarios para la UI del picker.
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
/// Respuesta paginada del endpoint GET /api/dashboard/projects.
/// Página 1: incluye proyectos pineados + ítems no pineados.
/// Páginas > 1: pinned[] vacío, solo ítems no pineados.
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
