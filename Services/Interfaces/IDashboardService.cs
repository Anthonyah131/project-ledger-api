using ProjectLedger.API.DTOs.Dashboard;

namespace ProjectLedger.API.Services;

public interface IDashboardService
{
    Task<MonthlySummaryDashboardResponse> GetMonthlySummaryAsync(
        Guid userId, DateOnly monthStart, Guid projectId, CancellationToken ct = default);

    Task<MonthlyDailyTrendResponse> GetMonthlyDailyTrendAsync(
        Guid userId, DateOnly monthStart, Guid projectId, CancellationToken ct = default);

    Task<MonthlyTopCategoriesResponse> GetMonthlyTopCategoriesAsync(
        Guid userId, DateOnly monthStart, Guid projectId, CancellationToken ct = default);

    Task<MonthlyPaymentMethodsResponse> GetMonthlyPaymentMethodsAsync(
        Guid userId, DateOnly monthStart, Guid projectId, CancellationToken ct = default);

    Task<MonthlyOverviewResponse> GetMonthlyOverviewAsync(
        Guid userId, DateOnly monthStart, CancellationToken ct = default);

    Task<DashboardProjectsPagedResponse> GetDashboardProjectsAsync(
        Guid userId, int page, int pageSize, string? q, CancellationToken ct = default);
}
