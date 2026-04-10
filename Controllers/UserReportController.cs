using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Report;
using ProjectLedger.API.Models;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// User level reports (cross-project).
/// Only accesses the authenticated user's own data.
/// </summary>
[ApiController]
[Route("api/reports")]
[Authorize]
[Tags("Reports & Insights")]
[Produces("application/json")]
public class UserReportController : ControllerBase
{
    private readonly IPlanAuthorizationService _planAuth;
    private readonly IReportExportService _exportService;
    private readonly IUserReportService _userReportService;

    public UserReportController(
        IPlanAuthorizationService planAuth,
        IReportExportService exportService,
        IUserReportService userReportService)
    {
        _planAuth = planAuth;
        _exportService = exportService;
        _userReportService = userReportService;
    }

    // ── GET /api/reports/payment-methods ────────────────────

    /// <summary>
    /// User payment methods report with individualized statistics
    /// per method, breakdown by project and monthly trend.
    /// All amounts are expressed in the currency of each payment method.
    /// JSON returns the last 10 movements per method; Excel/PDF include all.
    /// </summary>
    /// <param name="from">Start date (optional).</param>
    /// <param name="to">End date (optional).</param>
    /// <param name="paymentMethodIds">Filter by specific methods (optional, empty = all).</param>
    /// <param name="format">Export format: json (default), excel, pdf.</param>
    /// <response code="200">Report generated.</response>
    /// <response code="403">Plan does not allow advanced reports.</response>
    [HttpGet("payment-methods")]
    [ProducesResponseType(typeof(PaymentMethodReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPaymentMethodReport(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] List<Guid>? paymentMethodIds,
        [FromQuery] string format = "json",
        CancellationToken ct = default)
    {
        var userId = User.GetRequiredUserId();

        // Requires advanced reports
        await _planAuth.ValidatePermissionAsync(userId, PlanPermission.CanUseAdvancedReports, ct);

        // Excel/PDF also require CanExportData
        var isExport = format.Equals("excel", StringComparison.OrdinalIgnoreCase)
                    || format.Equals("pdf", StringComparison.OrdinalIgnoreCase);

        if (isExport)
            await _planAuth.ValidatePermissionAsync(userId, PlanPermission.CanExportData, ct);

        // JSON: 10 movements per method; Export: all
        int? maxMovements = isExport ? null : 10;

        var report = await _userReportService.GetPaymentMethodReportAsync(
            userId, from, to, paymentMethodIds, maxMovements, ct);

        return format.ToLowerInvariant() switch
        {
            "excel" => ExportExcel(
                _exportService.GeneratePaymentMethodReportExcel(report),
                "payment-method-report"),
            "pdf" => ExportPdf(
                _exportService.GeneratePaymentMethodReportPdf(report),
                "payment-method-report"),
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
