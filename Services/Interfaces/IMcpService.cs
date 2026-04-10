using ProjectLedger.API.DTOs.Mcp;

namespace ProjectLedger.API.Services;

public interface IMcpService
{
    /// <summary>
    /// Retrieves the initial context for the MCP assistant, including visible projects, permissions, and limits.
    /// </summary>
    Task<McpContextResponse> GetContextAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Gets a paginated list of projects in the user's portfolio with aggregated financial status and activity.
    /// </summary>
    Task<McpPagedResponse<McpProjectPortfolioItemResponse>> GetProjectPortfolioAsync(
        Guid userId,
        McpProjectPortfolioQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Returns upcoming financial deadlines (obligations) across the selected project scope.
    /// </summary>
    Task<McpPagedResponse<McpProjectDeadlineItemResponse>> GetProjectDeadlinesAsync(
        Guid userId,
        McpProjectDeadlinesQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Analyzes the project activity split (Active, Completed, At Risk, Inactive).
    /// </summary>
    Task<McpProjectActivitySplitResponse> GetProjectActivitySplitAsync(
        Guid userId,
        McpProjectActivitySplitQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves pending payment obligations that are not yet fully paid.
    /// </summary>
    Task<McpPagedResponse<McpPaymentObligationItemResponse>> GetPendingPaymentsAsync(
        Guid userId,
        McpPendingPaymentsQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// List historical income entries (received payments) for the selected period.
    /// </summary>
    Task<McpPagedResponse<McpReceivedPaymentItemResponse>> GetReceivedPaymentsAsync(
        Guid userId,
        McpReceivedPaymentsQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Returns overdue obligations that have passed their due date and remain unpaid.
    /// </summary>
    Task<McpPagedResponse<McpPaymentObligationItemResponse>> GetOverduePaymentsAsync(
        Guid userId,
        McpOverduePaymentsQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Analyzes the usage of different payment methods across income and expenses.
    /// </summary>
    Task<McpPaymentMethodUsageResponse> GetPaymentMethodUsageAsync(
        Guid userId,
        McpPaymentMethodUsageQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Calculates expense totals, averages, and comparisons with previous periods.
    /// </summary>
    Task<McpExpenseTotalsResponse> GetExpenseTotalsAsync(
        Guid userId,
        McpExpenseTotalsQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Breaks down expenses by category for the specified scope and time range.
    /// </summary>
    Task<McpExpenseByCategoryResponse> GetExpenseByCategoryAsync(
        Guid userId,
        McpExpenseByCategoryQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Compares expense amounts across different projects.
    /// </summary>
    Task<McpExpenseByProjectResponse> GetExpenseByProjectAsync(
        Guid userId,
        McpExpenseByProjectQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Generates expense trends over time based on the requested granularity (day, week, month).
    /// </summary>
    Task<McpExpenseTrendsResponse> GetExpenseTrendsAsync(
        Guid userId,
        McpExpenseTrendsQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Generates income trends over time based on the requested granularity.
    /// </summary>
    Task<McpIncomeByPeriodResponse> GetIncomeByPeriodAsync(
        Guid userId,
        McpIncomeByPeriodQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Compares income amounts across different projects.
    /// </summary>
    Task<McpIncomeByProjectResponse> GetIncomeByProjectAsync(
        Guid userId,
        McpIncomeByProjectQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves obligations due within a specific upcoming window.
    /// </summary>
    Task<McpPagedResponse<McpObligationItemResponse>> GetUpcomingObligationsAsync(
        Guid userId,
        McpUpcomingObligationsQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Lists all obligations that have a remaining balance.
    /// </summary>
    Task<McpPagedResponse<McpObligationItemResponse>> GetUnpaidObligationsAsync(
        Guid userId,
        McpUnpaidObligationsQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Calculates high-level financial health scores and signals (Burn rate, Overdue count, Risks).
    /// </summary>
    Task<McpFinancialHealthResponse> GetFinancialHealthAsync(
        Guid userId,
        McpFinancialHealthQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Generates a comprehensive monthly financial overview, including top categories and payment splits.
    /// </summary>
    Task<McpMonthlyOverviewResponse> GetMonthlyOverviewAsync(
        Guid userId,
        McpMonthlyOverviewQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves automated financial alerts based on budget thresholds and overdue items.
    /// </summary>
    Task<McpAlertsResponse> GetAlertsAsync(
        Guid userId,
        McpAlertsQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Calculates the current debt/credit balance for all project partners.
    /// </summary>
    Task<McpPartnerBalancesResponse> GetPartnerBalancesAsync(
        Guid userId,
        McpPartnerBalancesQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Lists historical settlements between partners.
    /// </summary>
    Task<McpPagedResponse<McpPartnerSettlementItemResponse>> GetPartnerSettlementsAsync(
        Guid userId,
        McpPartnerSettlementsQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves a unified list of recent income and expense movements.
    /// </summary>
    Task<McpRecentMovementsResponse> GetRecentMovementsAsync(
        Guid userId,
        McpRecentMovementsQuery query,
        CancellationToken ct = default);
}
