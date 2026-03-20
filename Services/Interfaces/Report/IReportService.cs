using ProjectLedger.API.DTOs.Report;

namespace ProjectLedger.API.Services;

public interface IReportService
{
    Task<ProjectReportResponse> GetSummaryAsync(
        Guid projectId, Guid ownerUserId, DateOnly? from, DateOnly? to, CancellationToken ct = default);

    Task<MonthComparisonResponse> GetMonthComparisonAsync(
        Guid projectId, CancellationToken ct = default);

    Task<CategoryGrowthEnvelopeResponse> GetCategoryGrowthAsync(
        Guid projectId, CancellationToken ct = default);

    Task<DetailedExpenseReportResponse> GetDetailedExpensesAsync(
        Guid projectId, Guid userId, DateOnly? from, DateOnly? to, CancellationToken ct = default);

    Task<PartnerBalanceReportResponse> GetPartnerBalancesAsync(
        Guid projectId, DateOnly? from, DateOnly? to, CancellationToken ct = default);

    Task<DetailedIncomeReportResponse> GetDetailedIncomesAsync(
        Guid projectId, Guid userId, DateOnly? from, DateOnly? to, CancellationToken ct = default);
}
