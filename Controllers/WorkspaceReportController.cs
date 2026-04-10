using Microsoft.Extensions.Localization;
using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Report;
using ProjectLedger.API.Models;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Workspace level aggregated reports.
/// Consolidates data from multiple projects within a workspace.
/// </summary>
[ApiController]
[Route("api/workspaces/{workspaceId:guid}/reports")]
[Authorize]
[Tags("Reports & Insights")]
[Produces("application/json")]
public class WorkspaceReportController : ControllerBase
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IPlanAuthorizationService _planAuth;
    private readonly IWorkspaceReportService _workspaceReportService;
    private readonly IReportExportService _exportService;
    private readonly IStringLocalizer<Messages> _localizer;

    public WorkspaceReportController(
        IWorkspaceService workspaceService,
        IPlanAuthorizationService planAuth,
        IWorkspaceReportService workspaceReportService,
        IReportExportService exportService,
        IStringLocalizer<Messages> localizer)
    {
        _workspaceService = workspaceService;
        _planAuth = planAuth;
        _workspaceReportService = workspaceReportService;
        _exportService = exportService;
        _localizer = localizer;
    }

    // ── GET /api/workspaces/{workspaceId}/reports/summary ────

    /// <summary>
    /// Consolidated workspace summary with totals by project,
    /// cross-project categories and monthly trend.
    /// </summary>
    /// <param name="referenceCurrency">Reference currency to consolidate totals (optional).
    /// If all projects use the same currency, it is consolidated automatically.</param>
    /// <param name="format">Export format: json (default), excel, pdf.</param>
    /// <response code="200">Consolidated workspace report.</response>
    /// <response code="403">Plan does not allow advanced reports or user is not a member.</response>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(WorkspaceReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSummary(
        Guid workspaceId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? referenceCurrency,
        [FromQuery] string format = "json",
        CancellationToken ct = default)
    {
        var userId = User.GetRequiredUserId();

        // Verify workspace membership
        var role = await _workspaceService.GetMemberRoleAsync(workspaceId, userId, ct);
        if (role is null)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["WorkspaceNotFound"]));

        // Require advanced reports permission
        await _planAuth.ValidatePermissionAsync(userId, PlanPermission.CanUseAdvancedReports, ct);

        if (format.Equals("excel", StringComparison.OrdinalIgnoreCase) ||
            format.Equals("pdf", StringComparison.OrdinalIgnoreCase))
            await _planAuth.ValidatePermissionAsync(userId, PlanPermission.CanExportData, ct);

        var report = await _workspaceReportService.GetSummaryAsync(
            workspaceId, userId, from, to, referenceCurrency, ct);

        return format.ToLowerInvariant() switch
        {
            "excel" => ExportExcel(
                _exportService.GenerateWorkspaceReportExcel(report),
                $"workspace-report-{report.WorkspaceName}"),
            "pdf" => ExportPdf(
                _exportService.GenerateWorkspaceReportPdf(report),
                $"workspace-report-{report.WorkspaceName}"),
            _ => Ok(report)
        };
    }

    // ── Private Helpers ─────────────────────────────────────

    private FileContentResult ExportExcel(byte[] content, string baseName)
    {
        var safeFileName = SanitizeFileName(baseName);
        return File(content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"{safeFileName}.xlsx");
    }

    private FileContentResult ExportPdf(byte[] content, string baseName)
    {
        var safeFileName = SanitizeFileName(baseName);
        return File(content, "application/pdf", $"{safeFileName}.pdf");
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
    }
}
