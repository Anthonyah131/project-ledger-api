using ProjectLedger.API.DTOs.Report;

namespace ProjectLedger.API.Services;

/// <summary>
/// Contract for generating financial reports for a project.
/// </summary>
public interface IReportService
{
    /// <summary>
    /// Gets a comprehensive summary of project finances including expenses, income, and balances.
    /// </summary>
    Task<ProjectReportResponse> GetSummaryAsync(
        Guid projectId, Guid ownerUserId, DateOnly? from, DateOnly? to, CancellationToken ct = default);

    /// <summary>
    /// Gets a month-over-month comparison of project finances.
    /// </summary>
    Task<MonthComparisonResponse> GetMonthComparisonAsync(
        Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Analyzes the growth rate of expense categories over time.
    /// </summary>
    Task<CategoryGrowthEnvelopeResponse> GetCategoryGrowthAsync(
        Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Returns a detailed transaction-by-transaction list of expenses for the project.
    /// </summary>
    Task<DetailedExpenseReportResponse> GetDetailedExpensesAsync(
        Guid projectId, Guid userId, DateOnly? from, DateOnly? to, CancellationToken ct = default);

    /// <summary>
    /// Generates a report showing current debt/credit state for project partners.
    /// </summary>
    Task<PartnerBalanceReportResponse> GetPartnerBalancesAsync(
        Guid projectId, DateOnly? from, DateOnly? to, CancellationToken ct = default);

    /// <summary>
    /// Returns a detailed transaction-by-transaction list of income entries for the project.
    /// </summary>
    Task<DetailedIncomeReportResponse> GetDetailedIncomesAsync(
        Guid projectId, Guid userId, DateOnly? from, DateOnly? to, CancellationToken ct = default);
}
