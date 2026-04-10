using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.Resources;
using ProjectLedger.API.DTOs.Partner;
using ProjectLedger.API.DTOs.Report;
using ProjectLedger.API.Extensions.Mappings;
using ProjectLedger.API.Models;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Authenticated user's partners (financial contacts) controller.
///
/// Route: /api/partners
/// - OwnerUserId is ALWAYS obtained from the JWT, never from the body.
/// - Only the owner can view/edit/delete their partners.
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
    private readonly IStringLocalizer<Messages> _localizer;

    public PartnerController(
        IPartnerService partnerService,
        IPlanAuthorizationService planAuth,
        IPartnerReportService partnerReportService,
        IReportExportService exportService,
        IStringLocalizer<Messages> localizer)
    {
        _partnerService = partnerService;
        _planAuth = planAuth;
        _partnerReportService = partnerReportService;
        _exportService = exportService;
        _localizer = localizer;
    }

    // ── GET /api/partners ───────────────────────────────────

    /// <summary>
    /// Lists the authenticated user's partners. Supports search and pagination.
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
    /// Gets a partner by ID. To access their payment methods and projects,
    /// use the paginated endpoints: /payment-methods and /projects.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PartnerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var partner = await _partnerService.GetByIdAsync(id, ct);

        if (partner is null || partner.PtrOwnerUserId != userId)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["PartnerNotFound"]));

        return Ok(partner.ToResponse());
    }

    // ── POST /api/partners ──────────────────────────────────

    /// <summary>
    /// Creates a partner for the authenticated user.
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
    /// Updates a partner. Does not allow changing owner_user_id.
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
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["PartnerNotFound"]));

        partner.ApplyUpdate(request);
        await _partnerService.UpdateAsync(partner, ct);

        return Ok(partner.ToResponse());
    }

    // ── DELETE /api/partners/{id} ───────────────────────────

    /// <summary>
    /// Soft-deletes a partner. Cannot be deleted if it has payment methods
    /// linked to active projects.
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
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["PartnerNotFound"]));

        try
        {
            await _partnerService.SoftDeleteAsync(id, userId, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(LocalizedResponse.Create("CONFLICT", _localizer[ex.Message]));
        }
    }

    // ── GET /api/partners/{id}/payment-methods ──────────────

    /// <summary>
    /// Paginated list of the partner's payment methods.
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
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["PartnerNotFound"]));

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
    /// Paginated list of projects linked to the partner through their payment methods.
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
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["PartnerNotFound"]));

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
    /// General partner report: consolidated activity by project and payment method.
    /// Includes balances, transactions with splits, and settlements for each project.
    /// Project amounts are in the project's base currency.
    /// Payment method amounts are in the payment method's currency.
    /// </summary>
    /// <param name="format">Format: json (default) | excel</param>
    /// <response code="200">Report generated.</response>
    /// <response code="403">Plan does not allow advanced reports or exportation.</response>
    /// <response code="404">Partner not found.</response>
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
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["PartnerNotFound"]));

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
