using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.DTOs.Partner;
using ProjectLedger.API.DTOs.Report;
using ProjectLedger.API.Extensions.Mappings;
using ProjectLedger.API.Models;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Controlador de partners (contactos financieros) del usuario autenticado.
///
/// Ruta: /api/partners
/// - OwnerUserId se obtiene SIEMPRE del JWT, nunca del body.
/// - Solo el dueño puede ver/editar/eliminar sus partners.
/// </summary>
[ApiController]
[Route("api/partners")]
[Authorize]
[Tags("Partners")]
[Produces("application/json")]
public class PartnerController : ControllerBase
{
    private readonly IPartnerService _partnerService;
    private readonly IPlanAuthorizationService _planAuth;
    private readonly IPartnerReportService _partnerReportService;
    private readonly IReportExportService _exportService;

    public PartnerController(
        IPartnerService partnerService,
        IPlanAuthorizationService planAuth,
        IPartnerReportService partnerReportService,
        IReportExportService exportService)
    {
        _partnerService = partnerService;
        _planAuth = planAuth;
        _partnerReportService = partnerReportService;
        _exportService = exportService;
    }

    // ── GET /api/partners ───────────────────────────────────

    /// <summary>
    /// Lista los partners del usuario autenticado. Soporta búsqueda y paginación.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<PartnerResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyPartners(
        [FromQuery] string? search,
        [FromQuery] PagedRequest pagination,
        CancellationToken ct = default)
    {
        var userId = User.GetRequiredUserId();
        var (items, totalCount) = await _partnerService.SearchAsync(userId, search, pagination.Skip, pagination.PageSize, ct);

        var response = PagedResponse<PartnerResponse>.Create(
            items.ToResponse().ToList(), totalCount, pagination);

        return Ok(response);
    }

    // ── GET /api/partners/{id} ──────────────────────────────

    /// <summary>
    /// Obtiene un partner por ID. Para acceder a sus métodos de pago y proyectos
    /// use los endpoints paginados: /payment-methods y /projects.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PartnerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var partner = await _partnerService.GetByIdAsync(id, ct);

        if (partner is null || partner.PtrOwnerUserId != userId)
            return NotFound(new { message = "Partner not found." });

        return Ok(partner.ToResponse());
    }

    // ── POST /api/partners ──────────────────────────────────

    /// <summary>
    /// Crea un partner para el usuario autenticado.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PartnerResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePartnerRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        var partner = request.ToEntity(userId);
        await _partnerService.CreateAsync(partner, ct);

        return CreatedAtAction(
            nameof(GetById),
            new { id = partner.PtrId },
            partner.ToResponse());
    }

    // ── PATCH /api/partners/{id} ────────────────────────────

    /// <summary>
    /// Actualiza un partner. No permite cambiar owner_user_id.
    /// </summary>
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(PartnerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdatePartnerRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        var partner = await _partnerService.GetByIdAsync(id, ct);

        if (partner is null || partner.PtrOwnerUserId != userId)
            return NotFound(new { message = "Partner not found." });

        partner.ApplyUpdate(request);
        await _partnerService.UpdateAsync(partner, ct);

        return Ok(partner.ToResponse());
    }

    // ── DELETE /api/partners/{id} ───────────────────────────

    /// <summary>
    /// Soft-delete de un partner. No se puede eliminar si tiene payment methods
    /// vinculados a proyectos activos.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var partner = await _partnerService.GetByIdAsync(id, ct);

        if (partner is null || partner.PtrOwnerUserId != userId)
            return NotFound(new { message = "Partner not found." });

        try
        {
            await _partnerService.SoftDeleteAsync(id, userId, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // ── GET /api/partners/{id}/payment-methods ──────────────

    /// <summary>
    /// Lista paginada de los métodos de pago del partner.
    /// </summary>
    [HttpGet("{id:guid}/payment-methods")]
    [ProducesResponseType(typeof(PagedResponse<PartnerPaymentMethodResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPaymentMethods(
        Guid id,
        [FromQuery] PagedRequest pagination,
        CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var partner = await _partnerService.GetByIdAsync(id, ct);

        if (partner is null || partner.PtrOwnerUserId != userId)
            return NotFound(new { message = "Partner not found." });

        var (paymentMethods, totalCount) = await _partnerService.GetPaymentMethodsPagedAsync(
            id, pagination.Skip, pagination.PageSize, ct);

        var items = paymentMethods.Select(pm => new PartnerPaymentMethodResponse
        {
            Id = pm.PmtId,
            Name = pm.PmtName,
            Type = pm.PmtType,
            Currency = pm.PmtCurrency,
            BankName = pm.PmtBankName
        }).ToList();

        var response = PagedResponse<PartnerPaymentMethodResponse>.Create(items, totalCount, pagination);
        return Ok(response);
    }

    // ── GET /api/partners/{id}/projects ─────────────────────

    /// <summary>
    /// Lista paginada de los proyectos vinculados al partner a través de sus métodos de pago.
    /// </summary>
    [HttpGet("{id:guid}/projects")]
    [ProducesResponseType(typeof(PagedResponse<PartnerProjectResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProjects(
        Guid id,
        [FromQuery] PagedRequest pagination,
        CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var partner = await _partnerService.GetByIdAsync(id, ct);

        if (partner is null || partner.PtrOwnerUserId != userId)
            return NotFound(new { message = "Partner not found." });

        var (projects, totalCount) = await _partnerService.GetProjectsPagedAsync(
            id, pagination.Skip, pagination.PageSize, ct);

        var items = projects.Select(p => new PartnerProjectResponse
        {
            Id = p.PrjId,
            Name = p.PrjName,
            CurrencyCode = p.PrjCurrencyCode,
            Description = p.PrjDescription,
            WorkspaceId = p.PrjWorkspaceId,
            WorkspaceName = p.Workspace?.WksName
        }).ToList();

        var response = PagedResponse<PartnerProjectResponse>.Create(items, totalCount, pagination);
        return Ok(response);
    }

    // ── GET /api/partners/{id}/reports/general ───────────────

    /// <summary>
    /// Reporte general del partner: actividad consolidada por proyecto y método de pago.
    /// Incluye balances, transacciones con splits y settlements de cada proyecto.
    /// Los montos por proyecto están en la moneda base del proyecto.
    /// Los montos por método de pago están en la moneda del método de pago.
    /// </summary>
    /// <param name="format">Formato: json (default) | excel</param>
    /// <response code="200">Reporte generado.</response>
    /// <response code="403">Plan no permite reportes avanzados o exportación.</response>
    /// <response code="404">Partner no encontrado.</response>
    [HttpGet("{id:guid}/reports/general")]
    [Tags("Reports & Insights")]
    [ProducesResponseType(typeof(PartnerGeneralReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGeneralReport(
        Guid id,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string format = "json",
        CancellationToken ct = default)
    {
        var userId = User.GetRequiredUserId();
        var partner = await _partnerService.GetByIdAsync(id, ct);

        if (partner is null || partner.PtrOwnerUserId != userId)
            return NotFound(new { message = "Partner not found." });

        await _planAuth.ValidatePermissionAsync(userId, PlanPermission.CanUseAdvancedReports, ct);

        if (format.Equals("excel", StringComparison.OrdinalIgnoreCase))
            await _planAuth.ValidatePermissionAsync(userId, PlanPermission.CanExportData, ct);

        var report = await _partnerReportService.GetGeneralReportAsync(id, from, to, ct);

        if (format.Equals("excel", StringComparison.OrdinalIgnoreCase))
        {
            var bytes = _exportService.GeneratePartnerGeneralReportExcel(report);
            var safeFileName = SanitizeFileName($"partner-report-{partner.PtrName}");
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"{safeFileName}.xlsx");
        }

        return Ok(report);
    }

    // ── Private Helpers ─────────────────────────────────────

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
    }
}
