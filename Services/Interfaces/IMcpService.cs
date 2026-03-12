using ProjectLedger.API.DTOs.Mcp;

namespace ProjectLedger.API.Services;

public interface IMcpService
{
    Task<McpContextResponse> GetContextAsync(Guid userId, CancellationToken ct = default);

    Task<McpPagedResponse<McpProjectPortfolioItemResponse>> GetProjectPortfolioAsync(
        Guid userId,
        McpProjectPortfolioQuery query,
        CancellationToken ct = default);

    Task<McpPagedResponse<McpProjectDeadlineItemResponse>> GetProjectDeadlinesAsync(
        Guid userId,
        McpProjectDeadlinesQuery query,
        CancellationToken ct = default);

    Task<McpProjectActivitySplitResponse> GetProjectActivitySplitAsync(
        Guid userId,
        McpProjectActivitySplitQuery query,
        CancellationToken ct = default);

    Task<McpPagedResponse<McpPaymentObligationItemResponse>> GetPendingPaymentsAsync(
        Guid userId,
        McpPendingPaymentsQuery query,
        CancellationToken ct = default);

    Task<McpPagedResponse<McpReceivedPaymentItemResponse>> GetReceivedPaymentsAsync(
        Guid userId,
        McpReceivedPaymentsQuery query,
        CancellationToken ct = default);

    Task<McpPagedResponse<McpPaymentObligationItemResponse>> GetOverduePaymentsAsync(
        Guid userId,
        McpOverduePaymentsQuery query,
        CancellationToken ct = default);

    Task<McpPaymentMethodUsageResponse> GetPaymentMethodUsageAsync(
        Guid userId,
        McpPaymentMethodUsageQuery query,
        CancellationToken ct = default);

    Task<McpExpenseTotalsResponse> GetExpenseTotalsAsync(
        Guid userId,
        McpExpenseTotalsQuery query,
        CancellationToken ct = default);

    Task<McpExpenseByCategoryResponse> GetExpenseByCategoryAsync(
        Guid userId,
        McpExpenseByCategoryQuery query,
        CancellationToken ct = default);

    Task<McpExpenseByProjectResponse> GetExpenseByProjectAsync(
        Guid userId,
        McpExpenseByProjectQuery query,
        CancellationToken ct = default);

    Task<McpExpenseTrendsResponse> GetExpenseTrendsAsync(
        Guid userId,
        McpExpenseTrendsQuery query,
        CancellationToken ct = default);

    Task<McpIncomeByPeriodResponse> GetIncomeByPeriodAsync(
        Guid userId,
        McpIncomeByPeriodQuery query,
        CancellationToken ct = default);

    Task<McpIncomeByProjectResponse> GetIncomeByProjectAsync(
        Guid userId,
        McpIncomeByProjectQuery query,
        CancellationToken ct = default);

    Task<McpPagedResponse<McpObligationItemResponse>> GetUpcomingObligationsAsync(
        Guid userId,
        McpUpcomingObligationsQuery query,
        CancellationToken ct = default);

    Task<McpPagedResponse<McpObligationItemResponse>> GetUnpaidObligationsAsync(
        Guid userId,
        McpUnpaidObligationsQuery query,
        CancellationToken ct = default);

    Task<McpFinancialHealthResponse> GetFinancialHealthAsync(
        Guid userId,
        McpFinancialHealthQuery query,
        CancellationToken ct = default);

    Task<McpMonthlyOverviewResponse> GetMonthlyOverviewAsync(
        Guid userId,
        McpMonthlyOverviewQuery query,
        CancellationToken ct = default);

    Task<McpAlertsResponse> GetAlertsAsync(
        Guid userId,
        McpAlertsQuery query,
        CancellationToken ct = default);
}
