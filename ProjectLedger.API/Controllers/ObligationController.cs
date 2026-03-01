using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.DTOs.Obligation;
using ProjectLedger.API.Extensions.Mappings;
using ProjectLedger.API.Repositories;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Controlador de obligaciones/deudas de un proyecto.
/// 
/// Ruta anidada: /api/projects/{projectId}/obligations
/// - ProjectId viene SIEMPRE de la ruta, nunca del body.
/// - CreatedByUserId viene del JWT, nunca del body.
/// - Viewer+ puede listar/ver. Editor+ puede crear/editar/eliminar.
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

    public ObligationController(
        IObligationService obligationService,
        IExpenseRepository expenseRepo,
        IProjectAccessService accessService,
        IPlanAuthorizationService planAuth)
    {
        _obligationService = obligationService;
        _expenseRepo = expenseRepo;
        _accessService = accessService;
        _planAuth = planAuth;
    }

    // ── GET /api/projects/{projectId}/obligations ───────────

    /// <summary>
    /// Lista todas las obligaciones del proyecto con montos pagados calculados (paginado).
    /// </summary>
    /// <response code="200">Lista paginada de obligaciones.</response>
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

        var responses = new List<ObligationResponse>();
        foreach (var obl in items)
        {
            paidAmounts.TryGetValue(obl.OblId, out var paidAmount);
            responses.Add(obl.ToResponse(paidAmount));
        }

        var response = PagedResponse<ObligationResponse>.Create(
            responses, totalCount, pagination);

        return Ok(response);
    }

    // ── GET /api/projects/{projectId}/obligations/{obligationId}

    /// <summary>
    /// Obtiene una obligación por ID con monto pagado calculado.
    /// </summary>
    /// <response code="200">Obligación encontrada.</response>
    /// <response code="404">Obligación no encontrada o no pertenece al proyecto.</response>
    [HttpGet("{obligationId:guid}")]
    [Authorize(Policy = "ProjectViewer")]
    [ProducesResponseType(typeof(ObligationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid projectId, Guid obligationId, CancellationToken ct)
    {
        var obl = await _obligationService.GetByIdAsync(obligationId, ct);
        if (obl is null || obl.OblProjectId != projectId)
            return NotFound(new { message = "Obligation not found in this project." });

        var paidAmount = await CalculatePaidAmountAsync(obligationId, ct);
        return Ok(obl.ToResponse(paidAmount));
    }

    // ── POST /api/projects/{projectId}/obligations ──────────

    /// <summary>
    /// Crea una obligación en el proyecto. Requiere editor+.
    /// ProjectId de la ruta, CreatedByUserId del JWT.
    /// </summary>
    /// <response code="201">Obligación creada.</response>
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
    /// Actualiza una obligación. Requiere editor+.
    /// </summary>
    /// <response code="200">Obligación actualizada.</response>
    /// <response code="404">Obligación no encontrada.</response>
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
            return NotFound(new { message = "Obligation not found in this project." });

        obl.ApplyUpdate(request);
        await _obligationService.UpdateAsync(obl, ct);

        var paidAmount = await CalculatePaidAmountAsync(obligationId, ct);
        return Ok(obl.ToResponse(paidAmount));
    }

    // ── DELETE /api/projects/{projectId}/obligations/{obligationId}

    /// <summary>
    /// Soft-delete de una obligación. Requiere editor+.
    /// </summary>
    /// <response code="204">Obligación eliminada.</response>
    /// <response code="404">Obligación no encontrada.</response>
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
            return NotFound(new { message = "Obligation not found in this project." });

        await _obligationService.SoftDeleteAsync(obligationId, userId, ct);
        return NoContent();
    }

    // ── Private Helpers ─────────────────────────────────────

    /// <summary>
    /// Calcula el monto pagado de una obligación sumando los gastos vinculados.
    /// </summary>
    private async Task<decimal> CalculatePaidAmountAsync(Guid obligationId, CancellationToken ct)
    {
        var expenses = await _expenseRepo.GetByObligationIdAsync(obligationId, ct);
        return expenses.Sum(e => e.ExpConvertedAmount);
    }
}
