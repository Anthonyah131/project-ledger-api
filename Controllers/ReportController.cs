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
/// Project reports and insights.
///
/// All calculations are performed with application logic on existing data.
/// No external APIs are used.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/reports")]
[Authorize]
[Tags("Reports & Insights")]
[Produces("application/json")]
public class ReportController : ControllerBase
{
    private readonly IProjectService _projectService;
    private readonly IProjectAccessService _accessService;
    private readonly IPlanAuthorizationService _planAuth;
    private readonly IReportExportService _exportService;
    private readonly IReportService _reportService;
    private readonly IStringLocalizer<Messages> _localizer;

    public ReportController(
        IProjectService projectService,
        IProjectAccessService accessService,
        IPlanAuthorizationService planAuth,
        IReportExportService exportService,
        IReportService reportService,
        IStringLocalizer<Messages> localizer)
    {
        _projectService = projectService;
        _accessService = accessService;
        _planAuth = planAuth;
        _exportService = exportService;
        _reportService = reportService;
        _localizer = localizer;
    }

    // ── GET /api/projects/{projectId}/reports/summary ───────

    /// <summary>
    /// Financial summary of the project with breakdown by category and payment method.
    /// Supports optional filtering by date range.
    /// </summary>
    /// <response code="200">Project summary.</response>
    [HttpGet("summary")]
    [Authorize(Policy = "ProjectViewer")]
    [ProducesResponseType(typeof(ProjectReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSummary(
        Guid projectId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        var project = await _projectService.GetByIdAsync(projectId, ct);
        if (project is null)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["ProjectNotFound"]));

        var response = await _reportService.GetSummaryAsync(projectId, project.PrjOwnerUserId, from, to, ct);
        return Ok(response);
    }

    // ── GET /api/projects/{projectId}/reports/month-comparison

    /// <summary>
    /// Compares the current month's expenses vs the previous month.
    /// </summary>
    /// <response code="200">Monthly comparison.</response>
    /// <response code="403">Plan does not allow advanced reports.</response>
    [HttpGet("month-comparison")]
    [Authorize(Policy = "ProjectViewer")]
    [ProducesResponseType(typeof(MonthComparisonResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMonthComparison(Guid projectId, CancellationToken ct)
    {
        var project = await _projectService.GetByIdAsync(projectId, ct);
        if (project is null)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["ProjectNotFound"]));

        await _planAuth.ValidatePermissionAsync(
            project.PrjOwnerUserId, PlanPermission.CanUseAdvancedReports, ct);

        var response = await _reportService.GetMonthComparisonAsync(projectId, ct);
        return Ok(response);
    }

    // ── GET /api/projects/{projectId}/reports/category-growth

    /// <summary>
    /// Identifies the categories with the highest growth comparing current month vs previous.
    /// Ordered by highest absolute growth.
    /// </summary>
    /// <response code="200">List of categories with growth.</response>
    /// <response code="403">Plan does not allow advanced reports.</response>
    [HttpGet("category-growth")]
    [Authorize(Policy = "ProjectViewer")]
    [ProducesResponseType(typeof(CategoryGrowthEnvelopeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCategoryGrowth(Guid projectId, CancellationToken ct)
    {
        var project = await _projectService.GetByIdAsync(projectId, ct);
        if (project is null)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["ProjectNotFound"]));

        await _planAuth.ValidatePermissionAsync(
            project.PrjOwnerUserId, PlanPermission.CanUseAdvancedReports, ct);

        var response = await _reportService.GetCategoryGrowthAsync(projectId, ct);
        return Ok(response);
    }

    // ── GET /api/projects/{projectId}/reports/expenses ──────

    /// <summary>
    /// Detailed project expense report with monthly sections.
    /// Basic: expense lines + totals.
    /// Premium: + analysis of categories/budgets + obligations.
    /// Accessible for project owner, editor and viewer.
    /// </summary>
    /// <param name="format">Export format: json (default), excel, pdf.</param>
    /// <response code="200">Report generated.</response>
    /// <response code="403">Insufficient plan.</response>
    [HttpGet("expenses")]
    [Authorize(Policy = "ProjectViewer")]
    [ProducesResponseType(typeof(DetailedExpenseReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetailedExpenses(
        Guid projectId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string format = "json",
        CancellationToken ct = default)
    {
        var project = await _projectService.GetByIdAsync(projectId, ct);
        if (project is null)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["ProjectNotFound"]));

        var userId = User.GetRequiredUserId();
        var ownerId = project.PrjOwnerUserId;

        // Verify data export permission (project owner's plan)
        await _planAuth.ValidatePermissionAsync(ownerId, PlanPermission.CanExportData, ct);

        // PDF requires CanUseAdvancedReports
        if (format.Equals("pdf", StringComparison.OrdinalIgnoreCase))
            await _planAuth.ValidatePermissionAsync(ownerId, PlanPermission.CanUseAdvancedReports, ct);

        var report = await _reportService.GetDetailedExpensesAsync(projectId, userId, from, to, ct);

        // Export according to format
        return format.ToLowerInvariant() switch
        {
            "excel" => ExportExcel(
                _exportService.GenerateExpenseReportExcel(report),
                $"expense-report-{project.PrjName}"),
            "pdf" => ExportPdf(
                _exportService.GenerateExpenseReportPdf(report),
                $"expense-report-{project.PrjName}"),
            _ => ReturnJsonReport(report)
        };
    }

    // ── GET /api/projects/{projectId}/reports/incomes ────────

    /// <summary>
    /// Detailed project income report with monthly sections.
    /// Basic: income lines + totals.
    /// Premium: + analysis of categories/payment methods + partners.
    /// Accessible for project owner, editor and viewer.
    /// </summary>
    /// <param name="format">Export format: json (default), excel, pdf.</param>
    /// <response code="200">Report generated.</response>
    /// <response code="403">Insufficient plan.</response>
    [HttpGet("incomes")]
    [Authorize(Policy = "ProjectViewer")]
    [ProducesResponseType(typeof(DetailedIncomeReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetailedIncomes(
        Guid projectId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string format = "json",
        CancellationToken ct = default)
    {
        var project = await _projectService.GetByIdAsync(projectId, ct);
        if (project is null)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["ProjectNotFound"]));

        var userId = User.GetRequiredUserId();
        var ownerId = project.PrjOwnerUserId;

        await _planAuth.ValidatePermissionAsync(ownerId, PlanPermission.CanExportData, ct);

        if (format.Equals("pdf", StringComparison.OrdinalIgnoreCase))
            await _planAuth.ValidatePermissionAsync(ownerId, PlanPermission.CanUseAdvancedReports, ct);

        var report = await _reportService.GetDetailedIncomesAsync(projectId, userId, from, to, ct);

        return format.ToLowerInvariant() switch
        {
            "excel" => ExportExcel(
                _exportService.GenerateIncomeReportExcel(report),
                $"income-report-{project.PrjName}"),
            "pdf" => ExportPdf(
                _exportService.GenerateIncomeReportPdf(report),
                $"income-report-{project.PrjName}"),
            _ => ReturnJsonIncomeReport(report)
        };
    }

    // ── GET /api/projects/{projectId}/reports/partner-balances

    /// <summary>
    /// Partner balances report for the project.
    /// Includes expense/income splits, settlements and net balances per pair.
    /// Requires partners to be enabled for the project.
    /// </summary>
    /// <response code="200">Balances report.</response>
    /// <param name="format">Export format: json (default), excel, pdf.</param>
    /// <response code="400">Partners not enabled in the project.</response>
    /// <response code="403">Plan does not allow advanced reports.</response>
    [HttpGet("partner-balances")]
    [Authorize(Policy = "ProjectViewer")]
    [ProducesResponseType(typeof(PartnerBalanceReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPartnerBalances(
        Guid projectId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string format = "json",
        CancellationToken ct = default)
    {
        var project = await _projectService.GetByIdAsync(projectId, ct);
        if (project is null)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["ProjectNotFound"]));

        if (!project.PrjPartnersEnabled)
            return BadRequest(LocalizedResponse.Create("VALIDATION_ERROR", _localizer["PartnersNotEnabled"]));

        await _planAuth.ValidatePermissionAsync(
            project.PrjOwnerUserId, PlanPermission.CanUseAdvancedReports, ct);

        if (format.Equals("excel", StringComparison.OrdinalIgnoreCase) ||
            format.Equals("pdf", StringComparison.OrdinalIgnoreCase))
            await _planAuth.ValidatePermissionAsync(project.PrjOwnerUserId, PlanPermission.CanExportData, ct);

        var response = await _reportService.GetPartnerBalancesAsync(projectId, from, to, ct);

        return format.ToLowerInvariant() switch
        {
            "excel" => ExportExcel(
                _exportService.GeneratePartnerBalanceReportExcel(response),
                $"partner-balances-{project.PrjName}"),
            "pdf" => ExportPdf(
                _exportService.GeneratePartnerBalanceReportPdf(response),
                $"partner-balances-{project.PrjName}"),
            _ => Ok(response)
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

    private IActionResult ReturnJsonReport(DetailedExpenseReportResponse report)
    {
        // JSON: limit expenses to 10 per section (totals already calculated with all)
        foreach (var section in report.Sections)
            section.Expenses = section.Expenses.Take(10).ToList();

        return Ok(report);
    }

    private IActionResult ReturnJsonIncomeReport(DetailedIncomeReportResponse report)
    {
        // JSON: limit incomes to 10 per section (totals already calculated with all)
        foreach (var section in report.Sections)
            section.Incomes = section.Incomes.Take(10).ToList();

        return Ok(report);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
    }
}
