using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Report;
using ProjectLedger.API.Models;
using ProjectLedger.API.Services;
using ProjectLedger.API.Services.Report;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Reportes a nivel de usuario (transversales a proyectos).
/// Solo accede a datos propios del usuario autenticado.
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
    /// Reporte de métodos de pago del usuario con estadísticas, desglose
    /// por proyecto y tendencia mensual.
    /// Solo accede a los métodos de pago y gastos del usuario autenticado.
    /// Requiere CanUseAdvancedReports.
    /// </summary>
    /// <param name="from">Fecha inicio (opcional).</param>
    /// <param name="to">Fecha fin (opcional).</param>
    /// <param name="paymentMethodId">Filtrar por un método de pago específico (opcional).</param>
    /// <param name="format">Formato de exportación: json (default), excel, pdf.</param>
    /// <response code="200">Reporte generado.</response>
    /// <response code="403">Plan no permite reportes avanzados.</response>
    [HttpGet("payment-methods")]
    [ProducesResponseType(typeof(PaymentMethodReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPaymentMethodReport(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] Guid? paymentMethodId,
        [FromQuery] string format = "json",
        CancellationToken ct = default)
    {
        var userId = User.GetRequiredUserId();

        // Requiere reportes avanzados
        await _planAuth.ValidatePermissionAsync(userId, PlanPermission.CanUseAdvancedReports, ct);

        // Excel también requiere CanExportData
        if (format.Equals("excel", StringComparison.OrdinalIgnoreCase))
            await _planAuth.ValidatePermissionAsync(userId, PlanPermission.CanExportData, ct);

        var report = await _userReportService.GetPaymentMethodReportAsync(userId, from, to, paymentMethodId, ct);

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
