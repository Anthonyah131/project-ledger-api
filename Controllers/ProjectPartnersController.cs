using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.DTOs.Partner;
using ProjectLedger.API.Extensions;
using ProjectLedger.API.Extensions.Mappings;
using ProjectLedger.API.Models;
using ProjectLedger.API.Resources;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Project partners balances, history, and settlements.
/// Only accessible when <c>prj_partners_enabled = true</c>.
/// Requires <c>owner</c> or <c>editor</c> role.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}")]
[Authorize]
[Tags("Project Partners — Balances & Settlements")]
[Produces("application/json")]
public class ProjectPartnersController : ControllerBase
{
    private readonly IProjectService _projectService;
    private readonly IProjectAccessService _accessService;
    private readonly IPartnerBalanceService _balanceService;
    private readonly IPartnerSettlementService _settlementService;
    private readonly IStringLocalizer<Messages> _localizer;

    public ProjectPartnersController(
        IProjectService projectService,
        IProjectAccessService accessService,
        IPartnerBalanceService balanceService,
        IPartnerSettlementService settlementService,
        IStringLocalizer<Messages> localizer)
    {
        _projectService = projectService;
        _accessService = accessService;
        _balanceService = balanceService;
        _settlementService = settlementService;
        _localizer = localizer;
    }

    // ── Helper ───────────────────────────────────────────────

    private async Task<Project> GetProjectWithPartnersEnabledAsync(Guid projectId, string role, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        await _accessService.ValidateAccessAsync(userId, projectId, role, ct);

        var project = await _projectService.GetByIdAsync(projectId, ct)
            ?? throw new KeyNotFoundException("ProjectNotFound");

        if (!project.PrjPartnersEnabled)
            throw new InvalidOperationException("PartnersNotEnabled");

        return project;
    }

    // ── GET /projects/:id/partners/balance ───────────────────

    /// <summary>
    /// Returns the balances of all project partners in the project's base currency.
    ///
    /// Balance logic:
    ///   netBalance = (othersOweHim - heOwesOthers) + (settlementsPaid - settlementsReceived)
    ///
    /// Positive = others owe him. Negative = he owes others.
    ///
    /// Registered settlements (partner_settlements) settle debt between partners without going through
    /// project payment methods. When paying a settlement, debt is reduced in the net balance.
    /// Only available when <c>partners_enabled = true</c>.
    /// </summary>
    [HttpGet("partners/balance")]
    [ProducesResponseType(typeof(PartnerBalanceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetPartnersBalance(Guid projectId, CancellationToken ct)
    {
        var project = await GetProjectWithPartnersEnabledAsync(projectId, "viewer", ct);
        var balance = await _balanceService.GetBalancesAsync(projectId, project.PrjCurrencyCode, ct);
        return Ok(balance);
    }

    // ── GET /projects/:id/partners/settlement-suggestions ────

    /// <summary>
    /// Returns the minimum transfers needed for all balances to reach zero.
    ///
    /// Greedy algorithm: pairs the largest creditor with the largest debtor and repeats.
    /// Minimizes the number of transfers.
    /// Only available when <c>partners_enabled = true</c>.
    /// </summary>
    [HttpGet("partners/settlement-suggestions")]
    [ProducesResponseType(typeof(SettlementSuggestionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetSettlementSuggestions(Guid projectId, CancellationToken ct)
    {
        var project = await GetProjectWithPartnersEnabledAsync(projectId, "viewer", ct);
        var suggestions = await _balanceService.GetSettlementSuggestionsAsync(projectId, project.PrjCurrencyCode, ct);
        return Ok(suggestions);
    }

    // ── GET /projects/:id/partners/:partnerId/history ────────

    /// <summary>
    /// Lists all transactions with splits for a partner and their project settlements.
    /// Transactions are paginated. Settlements are returned in full.
    /// </summary>
    [HttpGet("partners/{partnerId:guid}/history")]
    [ProducesResponseType(typeof(PartnerHistoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetPartnerHistory(
        Guid projectId,
        Guid partnerId,
        [FromQuery] PagedRequest pagination,
        CancellationToken ct)
    {
        await GetProjectWithPartnersEnabledAsync(projectId, "viewer", ct);
        var history = await _balanceService.GetPartnerHistoryAsync(projectId, partnerId, pagination, ct);
        return Ok(history);
    }

    // ── POST /projects/:id/partner-settlements ───────────────

    /// <summary>
    /// Registers a direct settlement between two project partners.
    ///
    /// When partner A pays partner B:
    ///   - A record is created with from_partner_id=A, to_partner_id=B, amount=X
    ///   - A's balance improves: their settlementsPaid goes up, netBalance approaches 0
    ///   - B's balance is reduced: their settlementsReceived goes up, netBalance approaches 0
    ///
    /// Settlements do not affect project payment methods.
    /// </summary>
    [HttpPost("partner-settlements")]
    [ProducesResponseType(typeof(SettlementResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateSettlement(
        Guid projectId,
        [FromBody] CreateSettlementRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (request.Amount <= 0)
            return BadRequest(LocalizedResponse.Create("VALIDATION_ERROR", _localizer["AmountMustBePositive"]));

        if (request.ExchangeRate <= 0)
            return BadRequest(LocalizedResponse.Create("VALIDATION_ERROR", _localizer["ExchangeRateMustBePositive"]));

        if (request.FromPartnerId == request.ToPartnerId)
            return BadRequest(LocalizedResponse.Create("VALIDATION_ERROR", _localizer["PartnersMustBeDifferent"]));

        await GetProjectWithPartnersEnabledAsync(projectId, "editor", ct);

        var userId = User.GetRequiredUserId();
        var entity = request.ToEntity(projectId, userId);
        var created = await _settlementService.CreateAsync(entity, request.CurrencyExchanges, ct);

        return CreatedAtAction(
            nameof(GetSettlements),
            new { projectId },
            created.ToResponse());
    }

    // ── PATCH /projects/:id/partner-settlements/:id ──────────

    /// <summary>
    /// Updates the editable fields of an existing settlement.
    /// Only the fields included in the body are applied (semantic PATCH).
    /// When changing amount or exchangeRate, convertedAmount is automatically recalculated.
    /// </summary>
    [HttpPatch("partner-settlements/{settlementId:guid}")]
    [ProducesResponseType(typeof(SettlementResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateSettlement(
        Guid projectId,
        Guid settlementId,
        [FromBody] UpdateSettlementRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (request.Amount.HasValue && request.Amount.Value <= 0)
            return BadRequest(LocalizedResponse.Create("VALIDATION_ERROR", _localizer["AmountMustBePositive"]));

        if (request.ExchangeRate.HasValue && request.ExchangeRate.Value <= 0)
            return BadRequest(LocalizedResponse.Create("VALIDATION_ERROR", _localizer["ExchangeRateMustBePositive"]));

        await GetProjectWithPartnersEnabledAsync(projectId, "editor", ct);

        var updated = await _settlementService.UpdateAsync(settlementId, projectId, request, ct);
        return Ok(updated.ToResponse());
    }

    // ── GET /projects/:id/partner-settlements ────────────────

    /// <summary>
    /// Lists the project's active settlements (paginated).
    /// </summary>
    [HttpGet("partner-settlements")]
    [ProducesResponseType(typeof(PagedResponse<SettlementResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetSettlements(
        Guid projectId,
        [FromQuery] PagedRequest pagination,
        CancellationToken ct)
    {
        await GetProjectWithPartnersEnabledAsync(projectId, "viewer", ct);

        var (items, total) = await _settlementService.GetPagedByProjectIdAsync(
            projectId, pagination.Skip, pagination.PageSize, ct);

        var response = PagedResponse<SettlementResponse>.Create(
            items.Select(s => s.ToResponse()).ToList(),
            total,
            pagination);

        return Ok(response);
    }

    // ── DELETE /projects/:id/partner-settlements/:id ─────────

    /// <summary>
    /// Soft-deletes a settlement. Reverts its effect on the balance.
    /// </summary>
    [HttpDelete("partner-settlements/{settlementId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DeleteSettlement(Guid projectId, Guid settlementId, CancellationToken ct)
    {
        await GetProjectWithPartnersEnabledAsync(projectId, "editor", ct);
        var userId = User.GetRequiredUserId();
        await _settlementService.SoftDeleteAsync(settlementId, projectId, userId, ct);
        return NoContent();
    }
}
