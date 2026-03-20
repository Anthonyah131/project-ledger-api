using ProjectLedger.API.DTOs.Report;

namespace ProjectLedger.API.Services;

public interface IWorkspaceReportService
{
    Task<WorkspaceReportResponse> GetSummaryAsync(
        Guid workspaceId, Guid userId, DateOnly? from, DateOnly? to, string? referenceCurrency, CancellationToken ct = default);
}
