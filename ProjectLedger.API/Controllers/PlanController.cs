using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Plan;
using ProjectLedger.API.Extensions.Mappings;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Catálogo de planes de suscripción. Solo lectura, público (no requiere auth).
/// Los planes se gestionan por seed/admin, no por API.
/// </summary>
[ApiController]
[Route("api/plans")]
[Tags("Plans")]
[Produces("application/json")]
public class PlanController : ControllerBase
{
    private readonly IPlanService _planService;

    public PlanController(IPlanService planService)
    {
        _planService = planService;
    }

    // ── GET /api/plans ──────────────────────────────────────

    /// <summary>
    /// Lista todos los planes activos disponibles.
    /// </summary>
    /// <response code="200">Lista de planes.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PlanResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var plans = await _planService.GetAllActiveAsync(ct);
        return Ok(plans.ToResponse());
    }

    // ── GET /api/plans/{idOrSlug} ───────────────────────────

    /// <summary>
    /// Obtiene un plan por ID (GUID) o por slug (e.g. "free", "basic", "premium").
    /// </summary>
    /// <param name="idOrSlug">GUID del plan o slug del plan.</param>
    /// <response code="200">Plan encontrado.</response>
    /// <response code="404">Plan no encontrado.</response>
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
            return NotFound(new { message = $"Plan '{idOrSlug}' not found." });

        return Ok(plan.ToResponse());
    }
}
