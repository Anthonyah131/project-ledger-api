using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.ProjectBudget;
using ProjectLedger.API.Extensions.Mappings;
using ProjectLedger.API.Repositories;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Controlador de presupuesto de proyecto.
/// 
/// Ruta anidada: /api/projects/{projectId}/budget
/// - Solo un presupuesto activo por proyecto (singleton-like).
/// - Requiere Plan:CanSetBudgets para crear/actualizar.
/// - Viewer+ puede consultar. Editor+ puede crear/editar/eliminar.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/budget")]
[Authorize]
[Tags("Project Budget")]
[Produces("application/json")]
public class ProjectBudgetController : ControllerBase
{
    private readonly IProjectBudgetService _budgetService;
    private readonly IExpenseRepository _expenseRepo;
    private readonly IProjectAccessService _accessService;

    public ProjectBudgetController(
        IProjectBudgetService budgetService,
        IExpenseRepository expenseRepo,
        IProjectAccessService accessService)
    {
        _budgetService = budgetService;
        _expenseRepo = expenseRepo;
        _accessService = accessService;
    }

    // ── GET /api/projects/{projectId}/budget ────────────────

    /// <summary>
    /// Obtiene el presupuesto activo del proyecto con el monto gastado calculado.
    /// </summary>
    /// <response code="200">Presupuesto encontrado.</response>
    /// <response code="404">No hay presupuesto activo para este proyecto.</response>
    [HttpGet]
    [Authorize(Policy = "ProjectViewer")]
    [ProducesResponseType(typeof(ProjectBudgetResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetActive(Guid projectId, CancellationToken ct)
    {
        var budget = await _budgetService.GetActiveByProjectIdAsync(projectId, ct);
        if (budget is null)
            return NotFound(new { message = "No active budget found for this project." });

        var spent = await CalculateSpentAmountAsync(projectId, ct);
        return Ok(budget.ToResponse(spent));
    }

    // ── PUT /api/projects/{projectId}/budget ────────────────

    /// <summary>
    /// Crea o actualiza el presupuesto del proyecto (upsert).
    /// Requiere editor+ y Plan:CanSetBudgets.
    /// </summary>
    /// <response code="200">Presupuesto actualizado.</response>
    /// <response code="201">Presupuesto creado.</response>
    /// <response code="403">Plan no permite setear presupuestos.</response>
    [HttpPut]
    [Authorize(Policy = "ProjectEditor")]
    [ProducesResponseType(typeof(ProjectBudgetResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProjectBudgetResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SetBudget(
        Guid projectId,
        [FromBody] SetProjectBudgetRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var existing = await _budgetService.GetActiveByProjectIdAsync(projectId, ct);
        var spent = await CalculateSpentAmountAsync(projectId, ct);

        if (existing is not null)
        {
            // Update
            existing.ApplyUpdate(request);
            await _budgetService.UpdateAsync(existing, ct);
            return Ok(existing.ToResponse(spent));
        }

        // Create
        var budget = request.ToEntity(projectId);
        await _budgetService.CreateAsync(budget, ct);

        return CreatedAtAction(
            nameof(GetActive),
            new { projectId },
            budget.ToResponse(spent));
    }

    // ── DELETE /api/projects/{projectId}/budget ─────────────

    /// <summary>
    /// Soft-delete del presupuesto activo. Requiere owner.
    /// </summary>
    /// <response code="204">Presupuesto eliminado.</response>
    /// <response code="404">No hay presupuesto activo.</response>
    [HttpDelete]
    [Authorize(Policy = "ProjectOwner")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid projectId, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();

        var budget = await _budgetService.GetActiveByProjectIdAsync(projectId, ct);
        if (budget is null)
            return NotFound(new { message = "No active budget found for this project." });

        await _budgetService.SoftDeleteAsync(budget.PjbId, userId, ct);
        return NoContent();
    }

    // ── Private Helpers ─────────────────────────────────────

    private Task<decimal> CalculateSpentAmountAsync(Guid projectId, CancellationToken ct)
        => _expenseRepo.GetSpentAmountByProjectIdAsync(projectId, ct);
}
