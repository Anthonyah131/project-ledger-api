using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ProjectLedger.API.DTOs.Plan;
using ProjectLedger.API.Extensions.Mappings;
using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.Resources;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Subscription plans catalog. Read-only, public (does not require auth).
/// Plans are managed via seed/admin, not via API.
/// </summary>
[ApiController]
[Route("api/plans")]
[Tags("Plans")]
[Produces("application/json")]
[AllowAnonymous]
public class PlanController : ControllerBase
{
    private readonly IPlanService _planService;
    private readonly IStringLocalizer<Messages> _localizer;

    public PlanController(IPlanService planService,
    IStringLocalizer<Messages> localizer)
    {
        _planService = planService;
        _localizer = localizer;
    }

    // ── GET /api/plans ──────────────────────────────────────

    /// <summary>
    /// Lists all available active plans.
    /// </summary>
    /// <response code="200">List of plans.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PlanResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var plans = await _planService.GetAllActiveAsync(ct);
        return Ok(plans.ToResponse());
    }

    // ── GET /api/plans/{idOrSlug} ───────────────────────────

    /// <summary>
    /// Gets a plan by ID (GUID) or by slug (e.g., "free", "basic", "premium").
    /// </summary>
    /// <param name="idOrSlug">Plan GUID or plan slug.</param>
    /// <response code="200">Plan found.</response>
    /// <response code="404">Plan not found.</response>
    [HttpGet("{idOrSlug}")]
    [ProducesResponseType(typeof(PlanResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdOrSlug(string idOrSlug, CancellationToken ct)
    {
        Models.Plan? plan;

        if (Guid.TryParse(idOrSlug, out var id))
            plan = await _planService.GetByIdAsync(id, ct);
        else
            plan = await _planService.GetBySlugAsync(idOrSlug, ct);

        if (plan is null)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["PlanNotFound"]));

        return Ok(plan.ToResponse());
    }
}
