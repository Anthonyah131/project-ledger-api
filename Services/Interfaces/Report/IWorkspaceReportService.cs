using ProjectLedger.API.DTOs.Report;

namespace ProjectLedger.API.Services;

public interface IWorkspaceReportService
{
    /// <summary>
    /// Generates a consolidated summary of all projects within a workspace, 
    /// converting financial totals to a reference currency if specified.
    /// </summary>
    Task<WorkspaceReportResponse> GetSummaryAsync(
        Guid workspaceId, Guid userId, DateOnly? from, DateOnly? to, string? referenceCurrency, CancellationToken ct = default);
}
