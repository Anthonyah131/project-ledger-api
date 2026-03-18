using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Report;
using ProjectLedger.API.Models;
using ProjectLedger.API.Services;
using ProjectLedger.API.Services.Report;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Reportes e insights por proyecto.
///
/// Todos los cálculos se realizan con lógica de aplicación sobre los datos existentes.
/// No se usan APIs externas.
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

    public ReportController(
        IProjectService projectService,
        IProjectAccessService accessService,
        IPlanAuthorizationService planAuth,
        IReportExportService exportService,
        IReportService reportService)
    {
        _projectService = projectService;
        _accessService = accessService;
        _planAuth = planAuth;
        _exportService = exportService;
        _reportService = reportService;
    }

    // ── GET /api/projects/{projectId}/reports/summary ───────

    /// <summary>
    /// Resumen financiero del proyecto con desglose por categoría y método de pago.
    /// Soporta filtro opcional por rango de fechas.
    /// </summary>
    /// <response code="200">Resumen del proyecto.</response>
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
            return NotFound(new { message = "Project not found." });

        var response = await _reportService.GetSummaryAsync(projectId, project.PrjOwnerUserId, from, to, ct);
        return Ok(response);
    }

    // ── GET /api/projects/{projectId}/reports/month-comparison

    /// <summary>
    /// Compara el gasto del mes actual vs el mes anterior.
    /// </summary>
    /// <response code="200">Comparación mensual.</response>
    /// <response code="403">Plan no permite reportes avanzados.</response>
    [HttpGet("month-comparison")]
    [Authorize(Policy = "ProjectViewer")]
    [ProducesResponseType(typeof(MonthComparisonResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMonthComparison(Guid projectId, CancellationToken ct)
    {
        var project = await _projectService.GetByIdAsync(projectId, ct);
        if (project is null)
            return NotFound(new { message = "Project not found." });

        await _planAuth.ValidatePermissionAsync(
            project.PrjOwnerUserId, PlanPermission.CanUseAdvancedReports, ct);

        var response = await _reportService.GetMonthComparisonAsync(projectId, ct);
        return Ok(response);
    }

    // ── GET /api/projects/{projectId}/reports/category-growth

    /// <summary>
    /// Identifica las categorías con mayor crecimiento comparando mes actual vs anterior.
    /// Ordenado por mayor crecimiento absoluto.
    /// </summary>
    /// <response code="200">Lista de categorías con crecimiento.</response>
    /// <response code="403">Plan no permite reportes avanzados.</response>
    [HttpGet("category-growth")]
    [Authorize(Policy = "ProjectViewer")]
    [ProducesResponseType(typeof(CategoryGrowthEnvelopeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCategoryGrowth(Guid projectId, CancellationToken ct)
    {
        var project = await _projectService.GetByIdAsync(projectId, ct);
        if (project is null)
            return NotFound(new { message = "Project not found." });

        await _planAuth.ValidatePermissionAsync(
            project.PrjOwnerUserId, PlanPermission.CanUseAdvancedReports, ct);

        var response = await _reportService.GetCategoryGrowthAsync(projectId, ct);
        return Ok(response);
    }

    // ── GET /api/projects/{projectId}/reports/expenses ──────

    /// <summary>
    /// Reporte detallado de gastos del proyecto con secciones mensuales.
    /// Basic: líneas de gastos + totales.
    /// Premium: + análisis de categorías/presupuestos + obligaciones.
    /// Solo el dueño del proyecto puede generar este reporte.
    /// </summary>
    /// <param name="format">Formato de exportación: json (default), excel, pdf.</param>
    /// <response code="200">Reporte generado.</response>
    /// <response code="403">No es dueño del proyecto o plan insuficiente.</response>
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
            return NotFound(new { message = "Project not found." });

        // Solo el dueño del proyecto puede generar reportes
        var userId = User.GetRequiredUserId();
        if (project.PrjOwnerUserId != userId)
            return Forbid();

        // Verificar permiso de exportación de datos
        await _planAuth.ValidatePermissionAsync(userId, PlanPermission.CanExportData, ct);

        // PDF requiere CanUseAdvancedReports
        if (format.Equals("pdf", StringComparison.OrdinalIgnoreCase))
            await _planAuth.ValidatePermissionAsync(userId, PlanPermission.CanUseAdvancedReports, ct);

        var report = await _reportService.GetDetailedExpensesAsync(projectId, userId, from, to, ct);

        // Exportar según formato
        return format.ToLowerInvariant() switch
        {
            "excel" => ExportExcel(
                _exportService.GenerateExpenseReportExcel(report),
                $"expense-report-{project.PrjName}"),
            "pdf" => ExportPdf(
                _exportService.GenerateExpenseReportPdf(report),
                $"expense-report-{project.PrjName}"),
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
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
    }
}
