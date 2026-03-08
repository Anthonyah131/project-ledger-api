using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.DTOs.Income;
using ProjectLedger.API.Extensions.Mappings;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Controlador de ingresos con autorización multi-tenant.
/// 
/// Ruta anidada: /api/projects/{projectId}/incomes
/// El projectId viene SIEMPRE de la ruta, nunca del body.
/// El userId viene SIEMPRE del JWT, nunca del body.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/incomes")]
[Authorize]
[Tags("Incomes")]
[Produces("application/json")]
public class IncomeController : ControllerBase
{
    private readonly IIncomeService _incomeService;
    private readonly IProjectAccessService _accessService;
    private readonly IPlanAuthorizationService _planAuth;
    private readonly ITransactionCurrencyExchangeService _exchangeService;

    public IncomeController(
        IIncomeService incomeService,
        IProjectAccessService accessService,
        IPlanAuthorizationService planAuth,
        ITransactionCurrencyExchangeService exchangeService)
    {
        _incomeService = incomeService;
        _accessService = accessService;
        _planAuth = planAuth;
        _exchangeService = exchangeService;
    }

    // ── GET /api/projects/{projectId}/incomes ───────────────

    /// <summary>
    /// Lista todos los ingresos del proyecto con paginación.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "ProjectViewer")]
    [ProducesResponseType(typeof(PagedResponse<IncomeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetByProject(
        Guid projectId,
        [FromQuery] PagedRequest pagination,
        [FromQuery] bool includeDeleted = false,
        CancellationToken ct = default)
    {
        if (includeDeleted)
        {
            var userId = User.GetRequiredUserId();
            await _accessService.ValidateAccessAsync(userId, projectId, ProjectRoles.Editor, ct);
        }

        var (items, totalCount) = await _incomeService.GetByProjectIdPagedAsync(
            projectId, includeDeleted, pagination.Skip, pagination.PageSize,
            pagination.SortBy, pagination.IsDescending, ct);

        var response = PagedResponse<IncomeResponse>.Create(
            items.ToResponse().ToList(), totalCount, pagination);

        return Ok(response);
    }

    // ── GET /api/projects/{projectId}/incomes/{incomeId} ────

    /// <summary>
    /// Obtiene un ingreso por ID.
    /// </summary>
    [HttpGet("{incomeId:guid}")]
    [Authorize(Policy = "ProjectViewer")]
    [ProducesResponseType(typeof(IncomeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid projectId, Guid incomeId, CancellationToken ct)
    {
        var income = await _incomeService.GetByIdAsync(incomeId, ct);
        if (income == null || income.IncProjectId != projectId)
            return NotFound(new { message = "Income not found in this project." });

        return Ok(income.ToResponse());
    }

    // ── POST /api/projects/{projectId}/incomes ──────────────

    /// <summary>
    /// Crea un ingreso en el proyecto. Requiere rol editor+.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "ProjectEditor")]
    [ProducesResponseType(typeof(IncomeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        Guid projectId,
        [FromBody] CreateIncomeRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();

        await _planAuth.ValidateProjectWriteAccessAsync(projectId, userId, ct);
        await _accessService.ValidateAccessAsync(userId, projectId, ProjectRoles.Editor, ct);

        var income = request.ToEntity(projectId, userId);
        await _incomeService.CreateAsync(income, ct);

        // Guardar exchange values para monedas alternativas
        if (request.CurrencyExchanges?.Count > 0)
        {
            await _exchangeService.SaveExchangesAsync("income", income.IncId, request.CurrencyExchanges, ct);
            // Re-fetch para incluir exchanges en la respuesta
            income = (await _incomeService.GetByIdAsync(income.IncId, ct))!;
        }

        return CreatedAtAction(
            nameof(GetById),
            new { projectId, incomeId = income.IncId },
            income.ToResponse());
    }

    // ── PUT /api/projects/{projectId}/incomes/{incomeId} ────

    /// <summary>
    /// Actualiza un ingreso existente.
    /// </summary>
    [HttpPut("{incomeId:guid}")]
    [Authorize(Policy = "ProjectEditor")]
    [ProducesResponseType(typeof(IncomeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid projectId,
        Guid incomeId,
        [FromBody] UpdateIncomeRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        await _planAuth.ValidateProjectWriteAccessAsync(projectId, userId, ct);

        var income = await _incomeService.GetByIdAsync(incomeId, ct);
        if (income == null || income.IncProjectId != projectId)
            return NotFound(new { message = "Income not found in this project." });

        income.ApplyUpdate(request);
        await _incomeService.UpdateAsync(income, ct);

        // Actualizar exchange values
        if (request.CurrencyExchanges is not null)
        {
            await _exchangeService.ReplaceExchangesAsync("income", income.IncId, request.CurrencyExchanges, ct);
        }

        income = (await _incomeService.GetByIdAsync(incomeId, ct))!;
        return Ok(income.ToResponse());
    }

    // ── DELETE /api/projects/{projectId}/incomes/{incomeId} ─

    /// <summary>
    /// Elimina (soft delete) un ingreso.
    /// </summary>
    [HttpDelete("{incomeId:guid}")]
    [Authorize(Policy = "ProjectEditor")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid projectId,
        Guid incomeId,
        CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        await _planAuth.ValidateProjectWriteAccessAsync(projectId, userId, ct);

        var income = await _incomeService.GetByIdAsync(incomeId, ct);
        if (income == null || income.IncProjectId != projectId)
            return NotFound(new { message = "Income not found in this project." });

        await _incomeService.SoftDeleteAsync(incomeId, userId, ct);
        return NoContent();
    }
}
