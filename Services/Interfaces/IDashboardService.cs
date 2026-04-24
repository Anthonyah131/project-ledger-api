using ProjectLedger.API.DTOs.Dashboard;

namespace ProjectLedger.API.Services;

public interface IDashboardService
{
    /// <summary>
    /// Gets a monthly financial summary (Income, Expense, Balance) for a project.
    /// </summary>
    Task<MonthlySummaryDashboardResponse> GetMonthlySummaryAsync(
        Guid userId, DateOnly monthStart, Guid projectId, int comparisonMonths, CancellationToken ct = default);

    /// <summary>
    /// Gets a daily trend of expenses and income for the specific month and project.
    /// </summary>
    Task<MonthlyDailyTrendResponse> GetMonthlyDailyTrendAsync(
        Guid userId, DateOnly monthStart, Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Gets the top expense categories for the project in the specified month.
    /// </summary>
    Task<MonthlyTopCategoriesResponse> GetMonthlyTopCategoriesAsync(
        Guid userId, DateOnly monthStart, Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Gets a breakdown of usage for different payment methods in the project during the month.
    /// </summary>
    Task<MonthlyPaymentMethodsResponse> GetMonthlyPaymentMethodsAsync(
        Guid userId, DateOnly monthStart, Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Gets a high-level overview of all the user's projects for the specified month.
    /// </summary>
    Task<MonthlyOverviewResponse> GetMonthlyOverviewAsync(
        Guid userId, DateOnly monthStart, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a paginated and searchable list of projects specifically for the dashboard project selector.
    /// Handles both pinned and unpinned projects.
    /// </summary>
    Task<DashboardProjectsPagedResponse> GetDashboardProjectsAsync(
        Guid userId, int page, int pageSize, string? q, CancellationToken ct = default);

    Task<MonthlyTopTransactionsResponse> GetMonthlyTopTransactionsAsync(
        Guid userId, DateOnly monthStart, Guid projectId, int limit, string type, CancellationToken ct = default);
}
