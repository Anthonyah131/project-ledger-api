using System.ComponentModel.DataAnnotations;
using ProjectLedger.API.DTOs.Common;

namespace ProjectLedger.API.DTOs.Mcp;

public class McpContextResponse
{
    public Guid UserId { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public string DefaultCurrencyCode { get; set; } = "USD";
    public List<McpVisibleProjectResponse> VisibleProjects { get; set; } = [];
    public Dictionary<string, bool> Permissions { get; set; } = new();
    public Dictionary<string, int?> Limits { get; set; } = new();
}

public class McpVisibleProjectResponse
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;
    public string UserRole { get; set; } = null!;
}

public class McpProjectPortfolioQuery : PagedRequest
{
    public Guid? ProjectId { get; set; }

    [RegularExpression("^(active|completed|at_risk|inactive)$", ErrorMessage = "Status must be active, completed, at_risk or inactive.")]
    public string? Status { get; set; }

    [Range(1, 3650)]
    public int? ActivityDays { get; set; }

    [Range(1, 3650)]
    public int? DueInDays { get; set; }
}

public class McpProjectPortfolioItemResponse
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public string UserRole { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;
    public DateTime? LastActivityAtUtc { get; set; }
    public DateOnly? NextDeadline { get; set; }
    public string Status { get; set; } = null!;
    public decimal ProgressPercent { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal NetBalance { get; set; }
    public decimal? BudgetUsedPercentage { get; set; }
    public int OpenObligations { get; set; }
    public int OverdueObligations { get; set; }
}

public class McpProjectDeadlinesQuery : PagedRequest
{
    public Guid? ProjectId { get; set; }
    public DateOnly? DueFrom { get; set; }
    public DateOnly? DueTo { get; set; }
    public bool? IncludeOverdue { get; set; }
}

public class McpProjectDeadlineItemResponse
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public string DeadlineType { get; set; } = "obligation_due";
    public Guid ObligationId { get; set; }
    public string Title { get; set; } = null!;
    public DateOnly DueDate { get; set; }
    public int DaysUntilDue { get; set; }
    public decimal RemainingAmount { get; set; }
    public string Currency { get; set; } = null!;
    public string Status { get; set; } = null!;
}

public class McpProjectActivitySplitQuery
{
    public Guid? ProjectId { get; set; }

    [Range(1, 3650)]
    public int? ActivityDays { get; set; }
}

public class McpProjectActivitySplitResponse
{
    public int ActiveCount { get; set; }
    public int CompletedCount { get; set; }
    public int AtRiskCount { get; set; }
    public int InactiveCount { get; set; }
    public List<McpProjectActivityItemResponse> Items { get; set; } = [];
}

public class McpProjectActivityItemResponse
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public string Status { get; set; } = null!;
}

public class McpReceivedPaymentsQuery : PagedRequest
{
    public Guid? ProjectId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public Guid? PaymentMethodId { get; set; }
    public Guid? CategoryId { get; set; }
    [Range(0, 99999999999999.99)]
    public decimal? MinAmount { get; set; }
}

public class McpPendingPaymentsQuery : PagedRequest
{
    public Guid? ProjectId { get; set; }
    public DateOnly? DueBefore { get; set; }
    public DateOnly? DueAfter { get; set; }
    [Range(0, 99999999999999.99)]
    public decimal? MinRemainingAmount { get; set; }
}

public class McpOverduePaymentsQuery : PagedRequest
{
    public Guid? ProjectId { get; set; }
    [Range(0, 3650)]
    public int? OverdueDaysMin { get; set; }
    [Range(0, 99999999999999.99)]
    public decimal? MinRemainingAmount { get; set; }
}

public class McpPaymentObligationItemResponse
{
    public Guid ObligationId { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public string Title { get; set; } = null!;
    public DateOnly? DueDate { get; set; }
    public int? DaysOverdue { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public string Currency { get; set; } = null!;
    public string Status { get; set; } = null!;
}

public class McpReceivedPaymentItemResponse
{
    public Guid IncomeId { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public Guid PaymentMethodId { get; set; }
    public string PaymentMethodName { get; set; } = null!;
    public DateOnly IncomeDate { get; set; }
    public string Title { get; set; } = null!;
    public decimal OriginalAmount { get; set; }
    public string OriginalCurrency { get; set; } = null!;
    public decimal ConvertedAmount { get; set; }
}

public class McpPaymentMethodUsageQuery
{
    public Guid? ProjectId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }

    [RegularExpression("^(expense|income|both)$", ErrorMessage = "Direction must be expense, income or both.")]
    public string? Direction { get; set; }

    [Range(1, 100)]
    public int? Top { get; set; }
}

public class McpPaymentMethodUsageResponse
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string Direction { get; set; } = null!;
    public List<McpPaymentMethodUsageItemResponse> Items { get; set; } = [];
}

public class McpPaymentMethodUsageItemResponse
{
    public Guid PaymentMethodId { get; set; }
    public string PaymentMethodName { get; set; } = null!;
    public string PaymentMethodType { get; set; } = null!;
    public decimal TotalOutgoing { get; set; }
    public decimal TotalIncoming { get; set; }
    public decimal NetFlow { get; set; }
    public int ExpenseCount { get; set; }
    public int IncomeCount { get; set; }
    public decimal UsagePercentage { get; set; }
}

public class McpExpenseTotalsQuery
{
    public Guid? ProjectId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public bool? ComparePreviousPeriod { get; set; }
}

public class McpExpenseTotalsResponse
{
    public Guid? ProjectId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public decimal TotalSpent { get; set; }
    public int TransactionCount { get; set; }
    public decimal AverageExpense { get; set; }
    public decimal? PreviousPeriodTotal { get; set; }
    public decimal? DeltaAmount { get; set; }
    public decimal? DeltaPercentage { get; set; }
}

public class McpExpenseByCategoryQuery
{
    public Guid? ProjectId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    [Range(1, 100)]
    public int? Top { get; set; }
    public bool? IncludeOthers { get; set; }
    public bool? IncludeTrend { get; set; }
}

public class McpExpenseByCategoryResponse
{
    public Guid? ProjectId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public decimal TotalSpent { get; set; }
    public List<McpExpenseByCategoryItemResponse> Items { get; set; } = [];
}

public class McpExpenseByCategoryItemResponse
{
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public int ExpenseCount { get; set; }
    public decimal Percentage { get; set; }
    public decimal? TrendDelta { get; set; }
}

public class McpExpenseByProjectQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    [Range(1, 100)]
    public int? Top { get; set; }
    public bool? IncludeBudgetContext { get; set; }
}

public class McpExpenseByProjectResponse
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public decimal TotalSpent { get; set; }
    public List<McpExpenseByProjectItemResponse> Items { get; set; } = [];
}

public class McpExpenseByProjectItemResponse
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;
    public decimal TotalSpent { get; set; }
    public int ExpenseCount { get; set; }
    public decimal? Budget { get; set; }
    public decimal? BudgetUsedPercentage { get; set; }
}

public class McpExpenseTrendsQuery
{
    public Guid? ProjectId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }

    [RegularExpression("^(day|week|month)$", ErrorMessage = "Granularity must be day, week or month.")]
    public string? Granularity { get; set; }

    public Guid? CategoryId { get; set; }
}

public class McpExpenseTrendsResponse
{
    public Guid? ProjectId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string Granularity { get; set; } = null!;
    public List<McpExpenseTrendPointResponse> Points { get; set; } = [];
}

public class McpExpenseTrendPointResponse
{
    public DateOnly PeriodStart { get; set; }
    public string PeriodLabel { get; set; } = null!;
    public decimal TotalSpent { get; set; }
    public int ExpenseCount { get; set; }
}

public class McpIncomeByPeriodQuery
{
    public Guid? ProjectId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }

    [RegularExpression("^(day|week|month)$", ErrorMessage = "Granularity must be day, week or month.")]
    public string? Granularity { get; set; }

    public bool? ComparePreviousPeriod { get; set; }
}

public class McpIncomeByPeriodResponse
{
    public Guid? ProjectId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string Granularity { get; set; } = null!;
    public decimal TotalIncome { get; set; }
    public int IncomeCount { get; set; }
    public decimal? PreviousPeriodTotal { get; set; }
    public decimal? DeltaAmount { get; set; }
    public decimal? DeltaPercentage { get; set; }
    public List<McpIncomePeriodPointResponse> Points { get; set; } = [];
}

public class McpIncomePeriodPointResponse
{
    public DateOnly PeriodStart { get; set; }
    public string PeriodLabel { get; set; } = null!;
    public decimal TotalIncome { get; set; }
    public int IncomeCount { get; set; }
}

public class McpIncomeByProjectQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    [Range(1, 100)]
    public int? Top { get; set; }
}

public class McpIncomeByProjectResponse
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public decimal TotalIncome { get; set; }
    public List<McpIncomeByProjectItemResponse> Items { get; set; } = [];
}

public class McpIncomeByProjectItemResponse
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;
    public decimal TotalIncome { get; set; }
    public int IncomeCount { get; set; }
}

public class McpUpcomingObligationsQuery : PagedRequest
{
    public Guid? ProjectId { get; set; }
    [Range(1, 3650)]
    public int? DueWithinDays { get; set; }
    [Range(0, 99999999999999.99)]
    public decimal? MinRemainingAmount { get; set; }
}

public class McpUnpaidObligationsQuery : PagedRequest
{
    public Guid? ProjectId { get; set; }

    [RegularExpression("^(open|partially_paid|overdue)$", ErrorMessage = "Status must be open, partially_paid or overdue.")]
    public string? Status { get; set; }
}

public class McpObligationItemResponse
{
    public Guid ObligationId { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public string Title { get; set; } = null!;
    public DateOnly? DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public string Currency { get; set; } = null!;
    public string Status { get; set; } = null!;
    public int? DaysUntilDue { get; set; }
    public int? DaysOverdue { get; set; }
}

public class McpFinancialHealthQuery
{
    public Guid? ProjectId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public class McpFinancialHealthResponse
{
    public Guid? ProjectId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int Score { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal NetBalance { get; set; }
    public decimal BurnRatePerDay { get; set; }
    public int BudgetRiskProjects { get; set; }
    public int OverdueObligationsCount { get; set; }
    public List<string> KeySignals { get; set; } = [];
}

public class McpMonthlyOverviewQuery
{
    [RegularExpression("^\\d{4}-(0[1-9]|1[0-2])$", ErrorMessage = "Month must use YYYY-MM format.")]
    public string? Month { get; set; }
    public Guid? ProjectId { get; set; }
}

public class McpMonthlyOverviewResponse
{
    public string Month { get; set; } = null!;
    public Guid? ProjectId { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public DateTime GeneratedAtUtc { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal NetBalance { get; set; }
    public int ExpenseCount { get; set; }
    public int IncomeCount { get; set; }
    public List<McpExpenseByCategoryItemResponse> TopCategories { get; set; } = [];
    public List<McpPaymentMethodUsageItemResponse> PaymentMethodSplit { get; set; } = [];
    public List<McpProjectHealthItemResponse> ProjectHealth { get; set; } = [];
    public List<McpAlertResponse> Alerts { get; set; } = [];
}

public class McpProjectHealthItemResponse
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public decimal Spent { get; set; }
    public decimal Income { get; set; }
    public decimal Net { get; set; }
    public decimal? Budget { get; set; }
    public decimal? BudgetUsedPercentage { get; set; }
}

public class McpAlertsQuery
{
    [RegularExpression("^\\d{4}-(0[1-9]|1[0-2])$", ErrorMessage = "Month must use YYYY-MM format.")]
    public string? Month { get; set; }
    public Guid? ProjectId { get; set; }
    [Range(0, 100)]
    public int? MinPriority { get; set; }
}

public class McpAlertsResponse
{
    public string? Month { get; set; }
    public Guid? ProjectId { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public List<McpAlertResponse> Items { get; set; } = [];
}

public class McpAlertResponse
{
    public string Code { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string Message { get; set; } = null!;
    public int Priority { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? ObligationId { get; set; }
}
