using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.Resources;
using ProjectLedger.API.DTOs.Obligation;
using ProjectLedger.API.Extensions.Mappings;
using ProjectLedger.API.Repositories;
using ProjectLedger.API.Services;


namespace ProjectLedger.API.Controllers;

/// <summary>
/// Project obligations/debts controller.
/// 
/// Nested route: /api/projects/{projectId}/obligations
/// - ProjectId ALWAYS comes from the route, never from the body.
/// - CreatedByUserId comes from the JWT, never from the body.
/// - Viewer+ can list/view. Editor+ can create/edit/delete.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/obligations")]
[Authorize]
[Tags("Obligations")]
[Produces("application/json")]
public class ObligationController : ControllerBase
{
    private readonly IObligationService _obligationService;
    private readonly IExpenseRepository _expenseRepo;
    private readonly IProjectAccessService _accessService;
    private readonly IPlanAuthorizationService _planAuth;
    private readonly IStringLocalizer<Messages> _localizer;

    public ObligationController(
        IObligationService obligationService,
        IExpenseRepository expenseRepo,
        IProjectAccessService accessService,
        IPlanAuthorizationService planAuth,
        IStringLocalizer<Messages> localizer)
    {
        _obligationService = obligationService;
        _expenseRepo = expenseRepo;
        _accessService = accessService;
        _planAuth = planAuth;
        _localizer = localizer;
    }

    // ── GET /api/projects/{projectId}/obligations ───────────

    /// <summary>
    /// Lists all project obligations with calculated paid amounts (paginated).
    /// </summary>
    /// <response code="200">Paginated list of obligations.</response>
    [HttpGet]
    [Authorize(Policy = "ProjectViewer")]
    [ProducesResponseType(typeof(PagedResponse<ObligationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByProject(
        Guid projectId,
        [FromQuery] PagedRequest pagination,
        CancellationToken ct)
    {
        var (items, totalCount) = await _obligationService.GetByProjectIdPagedAsync(
            projectId, pagination.Skip, pagination.PageSize,
            pagination.SortBy, pagination.IsDescending, ct);

        // Single batch query instead of N+1
        var obligationIds = items.Select(o => o.OblId).ToList();
        var paidAmounts = await _expenseRepo.GetPaidAmountsByObligationIdsAsync(obligationIds, ct);

        var responses = items.Select(obl =>
        {
            paidAmounts.TryGetValue(obl.OblId, out var paidAmount);
            return obl.ToResponse(paidAmount);
        }).ToList();

        var response = PagedResponse<ObligationResponse>.Create(
            responses, totalCount, pagination);

        return Ok(response);
    }

    // ── GET /api/projects/{projectId}/obligations/{obligationId}

    /// <summary>
    /// Gets an obligation by ID with calculated paid amount.
    /// </summary>
    /// <response code="200">Obligation found.</response>
    /// <response code="404">Obligation not found or does not belong to the project.</response>
    [HttpGet("{obligationId:guid}")]
    [Authorize(Policy = "ProjectViewer")]
    [ProducesResponseType(typeof(ObligationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid projectId, Guid obligationId, CancellationToken ct)
    {
        var obl = await _obligationService.GetByIdAsync(obligationId, ct);
        if (obl is null || obl.OblProjectId != projectId)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["ObligationNotFound"]));

        var paidAmount = await CalculatePaidAmountAsync(obligationId, obl.OblCurrency, ct);
        return Ok(obl.ToResponse(paidAmount));
    }

    // ── POST /api/projects/{projectId}/obligations ──────────

    /// <summary>
    /// Creates an obligation in the project. Requires editor+.
    /// ProjectId from the route, CreatedByUserId from the JWT.
    /// </summary>
    /// <response code="201">Obligation created.</response>
    [HttpPost]
    [Authorize(Policy = "ProjectEditor")]
    [ProducesResponseType(typeof(ObligationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        Guid projectId,
        [FromBody] CreateObligationRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        await _planAuth.ValidateProjectWriteAccessAsync(projectId, userId, ct);

        var obligation = request.ToEntity(projectId, userId);
        await _obligationService.CreateAsync(obligation, ct);

        return CreatedAtAction(
            nameof(GetById),
            new { projectId, obligationId = obligation.OblId },
            obligation.ToResponse());
    }

    // ── PUT /api/projects/{projectId}/obligations/{obligationId}

    /// <summary>
    /// Updates an obligation. Requires editor+.
    /// </summary>
    /// <response code="200">Obligation updated.</response>
    /// <response code="404">Obligation not found.</response>
    [HttpPut("{obligationId:guid}")]
    [Authorize(Policy = "ProjectEditor")]
    [ProducesResponseType(typeof(ObligationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid projectId,
        Guid obligationId,
        [FromBody] UpdateObligationRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        await _planAuth.ValidateProjectWriteAccessAsync(projectId, userId, ct);

        var obl = await _obligationService.GetByIdAsync(obligationId, ct);
        if (obl is null || obl.OblProjectId != projectId)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["ObligationNotFound"]));

        obl.ApplyUpdate(request);
        await _obligationService.UpdateAsync(obl, ct);

        var paidAmount = await CalculatePaidAmountAsync(obligationId, obl.OblCurrency, ct);
        return Ok(obl.ToResponse(paidAmount));
    }

    // ── DELETE /api/projects/{projectId}/obligations/{obligationId}

    /// <summary>
    /// Soft-deletes an obligation. Requires editor+.
    /// </summary>
    /// <response code="204">Obligation deleted.</response>
    /// <response code="404">Obligation not found.</response>
    [HttpDelete("{obligationId:guid}")]
    [Authorize(Policy = "ProjectEditor")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid projectId,
        Guid obligationId,
        CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        await _planAuth.ValidateProjectWriteAccessAsync(projectId, userId, ct);

        var obl = await _obligationService.GetByIdAsync(obligationId, ct);
        if (obl is null || obl.OblProjectId != projectId)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["ObligationNotFound"]));

        await _obligationService.SoftDeleteAsync(obligationId, userId, ct);
        return NoContent();
    }

    // ── Private Helpers ─────────────────────────────────────

    /// <summary>
    /// Calculates the paid amount of an obligation by summing linked expenses,
    /// converting each payment to the obligation's currency.
    /// </summary>
    private async Task<decimal> CalculatePaidAmountAsync(
        Guid obligationId, string obligationCurrency, CancellationToken ct)
    {
        var expenses = await _expenseRepo.GetByObligationIdAsync(obligationId, ct);
        return expenses.Sum(e =>
            e.ExpOriginalCurrency == obligationCurrency
                ? e.ExpOriginalAmount
                : e.ExpObligationEquivalentAmount
                    ?? e.ExpConvertedAmount); // legacy fallback for old records
    }
}
