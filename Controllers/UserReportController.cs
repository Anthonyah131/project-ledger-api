using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Report;
using ProjectLedger.API.Models;
using ProjectLedger.API.Services;

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
    /// Reporte de métodos de pago del usuario con estadísticas individualizadas
    /// por método, desglose por proyecto y tendencia mensual.
    /// Todos los montos se expresan en la moneda de cada método de pago.
    /// JSON devuelve los últimos 10 movimientos por método; Excel/PDF incluyen todos.
    /// </summary>
    /// <param name="from">Fecha inicio (opcional).</param>
    /// <param name="to">Fecha fin (opcional).</param>
    /// <param name="paymentMethodIds">Filtrar por métodos específicos (opcional, vacío = todos).</param>
    /// <param name="format">Formato de exportación: json (default), excel, pdf.</param>
    /// <response code="200">Reporte generado.</response>
    /// <response code="403">Plan no permite reportes avanzados.</response>
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

        // Requiere reportes avanzados
        await _planAuth.ValidatePermissionAsync(userId, PlanPermission.CanUseAdvancedReports, ct);

        // Excel/PDF también requieren CanExportData
        var isExport = format.Equals("excel", StringComparison.OrdinalIgnoreCase)
                    || format.Equals("pdf", StringComparison.OrdinalIgnoreCase);

        if (isExport)
            await _planAuth.ValidatePermissionAsync(userId, PlanPermission.CanExportData, ct);

        // JSON: 10 movimientos por método; Export: todos
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
