using System.ComponentModel.DataAnnotations;
using ProjectLedger.API.DTOs.Common;

namespace ProjectLedger.API.DTOs.Mcp;

public class McpProjectPortfolioQuery : PagedRequest
{
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public string? Status { get; set; }

    [Range(1, 3650)]
    public int? ActivityDays { get; set; }
}

public class McpProjectDeadlinesQuery : PagedRequest
{
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public DateOnly? DueFrom { get; set; }
    public DateOnly? DueTo { get; set; }
    public bool? IncludeOverdue { get; set; }
    public string? Search { get; set; }
}

public class McpProjectActivitySplitQuery
{
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }

    [Range(1, 3650)]
    public int? ActivityDays { get; set; }
}

public class McpReceivedPaymentsQuery : PagedRequest
{
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public Guid? PaymentMethodId { get; set; }
    public string? PaymentMethodName { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }

    [Range(0, 99999999999999.99)]
    public decimal? MinAmount { get; set; }

    public string? Search { get; set; }
}

public class McpPendingPaymentsQuery : PagedRequest
{
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public DateOnly? DueBefore { get; set; }
    public DateOnly? DueAfter { get; set; }

    [Range(0, 99999999999999.99)]
    public decimal? MinRemainingAmount { get; set; }

    public string? Search { get; set; }
}

public class McpOverduePaymentsQuery : PagedRequest
{
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }

    [Range(0, 3650)]
    public int? OverdueDaysMin { get; set; }

    [Range(0, 99999999999999.99)]
    public decimal? MinRemainingAmount { get; set; }

    public string? Search { get; set; }
}

public class McpPaymentMethodUsageQuery
{
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }

    public string? Direction { get; set; }

    [Range(1, 100)]
    public int? Top { get; set; }
}

public class McpExpenseTotalsQuery
{
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public bool? ComparePreviousPeriod { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
}

public class McpExpenseByCategoryQuery
{
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }

    [Range(1, 100)]
    public int? Top { get; set; }

    public bool? IncludeOthers { get; set; }
    public bool? IncludeTrend { get; set; }
}

public class McpExpenseByProjectQuery
{
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }

    [Range(1, 100)]
    public int? Top { get; set; }

    public bool? IncludeBudgetContext { get; set; }
}

public class McpExpenseTrendsQuery
{
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }

    public string? Granularity { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
}

public class McpIncomeByPeriodQuery
{
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string? Granularity { get; set; }
    public bool? ComparePreviousPeriod { get; set; }
}

public class McpIncomeByProjectQuery
{
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }

    [Range(1, 100)]
    public int? Top { get; set; }
}

public class McpUpcomingObligationsQuery : PagedRequest
{
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }

    [Range(1, 3650)]
    public int? DueWithinDays { get; set; }

    [Range(0, 99999999999999.99)]
    public decimal? MinRemainingAmount { get; set; }

    public string? Search { get; set; }
}

public class McpUnpaidObligationsQuery : PagedRequest
{
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }

    public string? Status { get; set; }

    public string? Search { get; set; }
}

public class McpFinancialHealthQuery
{
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public class McpMonthlyOverviewQuery
{
    [RegularExpression("^\\d{4}-(0[1-9]|1[0-2])$", ErrorMessage = "Month must use YYYY-MM format.")]
    public string? Month { get; set; }

    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
}

public class McpAlertsQuery
{
    [RegularExpression("^\\d{4}-(0[1-9]|1[0-2])$", ErrorMessage = "Month must use YYYY-MM format.")]
    public string? Month { get; set; }

    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }

    [Range(0, 100)]
    public int? MinPriority { get; set; }
}
