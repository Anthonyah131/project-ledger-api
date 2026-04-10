using Microsoft.Extensions.Localization;
using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.ProjectBudget;
using ProjectLedger.API.Extensions.Mappings;
using ProjectLedger.API.Repositories;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Project budget controller.
/// 
/// Nested route: /api/projects/{projectId}/budget
/// - Only one active budget per project (singleton-like).
/// - Requires Plan:CanSetBudgets to create/update.
/// - Viewer+ can query. Editor+ can create/edit/delete.
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
    private readonly IPlanAuthorizationService _planAuth;
    private readonly IStringLocalizer<Messages> _localizer;

    public ProjectBudgetController(
        IProjectBudgetService budgetService,
        IExpenseRepository expenseRepo,
        IProjectAccessService accessService,
        IPlanAuthorizationService planAuth,
        IStringLocalizer<Messages> localizer)
    {
        _budgetService = budgetService;
        _expenseRepo = expenseRepo;
        _accessService = accessService;
        _planAuth = planAuth;
        _localizer = localizer;
    }

    // ── GET /api/projects/{projectId}/budget ────────────────

    /// <summary>
    /// Gets the active project budget with the calculated spent amount.
    /// </summary>
    /// <response code="200">Budget found.</response>
    /// <response code="404">No active budget for this project.</response>
    [HttpGet]
    [Authorize(Policy = "ProjectViewer")]
    [ProducesResponseType(typeof(ProjectBudgetResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetActive(Guid projectId, CancellationToken ct)
    {
        var budget = await _budgetService.GetActiveByProjectIdAsync(projectId, ct);
        if (budget is null)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["BudgetNotFound"]));

        var spent = await CalculateSpentAmountAsync(projectId, ct);
        return Ok(budget.ToResponse(spent));
    }

    // ── PUT /api/projects/{projectId}/budget ────────────────

    /// <summary>
    /// Creates or updates the project budget (upsert).
    /// Requires editor+ and Plan:CanSetBudgets.
    /// </summary>
    /// <response code="200">Budget updated.</response>
    /// <response code="201">Budget created.</response>
    /// <response code="403">Plan does not allow setting budgets.</response>
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

        var userId = User.GetRequiredUserId();
        await _planAuth.ValidateProjectWriteAccessAsync(projectId, userId, ct);

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
    /// Soft-deletes the active budget. Requires owner.
    /// </summary>
    /// <response code="204">Budget deleted.</response>
    /// <response code="404">No active budget found.</response>
    [HttpDelete]
    [Authorize(Policy = "ProjectOwner")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid projectId, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        await _planAuth.ValidateProjectWriteAccessAsync(projectId, userId, ct);

        var budget = await _budgetService.GetActiveByProjectIdAsync(projectId, ct);
        if (budget is null)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["BudgetNotFound"]));

        await _budgetService.SoftDeleteAsync(budget.PjbId, userId, ct);
        return NoContent();
    }

    // ── Private Helpers ─────────────────────────────────────

    private Task<decimal> CalculateSpentAmountAsync(Guid projectId, CancellationToken ct)
        => _expenseRepo.GetSpentAmountByProjectIdAsync(projectId, ct);
}
