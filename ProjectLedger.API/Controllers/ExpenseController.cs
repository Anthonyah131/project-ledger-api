using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Expense;
using ProjectLedger.API.Extensions.Mappings;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Controlador de gastos con autorización multi-tenant.
/// 
/// Ruta anidada: /api/projects/{projectId}/expenses
/// El projectId viene SIEMPRE de la ruta, nunca del body.
/// El userId viene SIEMPRE del JWT, nunca del body.
/// 
/// Usa [Authorize(Policy = "...")] para validación declarativa
/// + IProjectAccessService para validación imperativa extra.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/expenses")]
[Authorize]
[Tags("Expenses")]
[Produces("application/json")]
public class ExpenseController : ControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly IProjectAccessService _accessService;

    public ExpenseController(
        IExpenseService expenseService,
        IProjectAccessService accessService)
    {
        _expenseService = expenseService;
        _accessService = accessService;
    }

    // ── GET /api/projects/{projectId}/expenses ──────────────

    /// <summary>
    /// Lista todos los gastos del proyecto. Requiere al menos ser viewer.
    /// </summary>
    /// <response code="200">Lista de gastos del proyecto.</response>
    /// <response code="403">Sin acceso al proyecto.</response>
    [HttpGet]
    [Authorize(Policy = "ProjectViewer")]
    [ProducesResponseType(typeof(IEnumerable<ExpenseResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetByProject(
        Guid projectId,
        [FromQuery] bool includeDeleted = false,
        CancellationToken ct = default)
    {
        // Solo Editor+ puede ver gastos eliminados
        if (includeDeleted)
        {
            var userId = User.GetRequiredUserId();
            await _accessService.ValidateAccessAsync(userId, projectId, ProjectRoles.Editor, ct);
        }

        var expenses = await _expenseService.GetByProjectIdAsync(projectId, includeDeleted, ct);
        return Ok(expenses.ToResponse());
    }

    // ── GET /api/projects/{projectId}/expenses/templates ───

    /// <summary>
    /// Lista todas las plantillas de gasto del proyecto.
    /// Las plantillas no son movimientos financieros reales.
    /// </summary>
    /// <response code="200">Lista de plantillas.</response>
    [HttpGet("templates")]
    [Authorize(Policy = "ProjectViewer")]
    [ProducesResponseType(typeof(IEnumerable<ExpenseResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTemplates(Guid projectId, CancellationToken ct)
    {
        var templates = await _expenseService.GetTemplatesByProjectIdAsync(projectId, ct);
        return Ok(templates.ToResponse());
    }

    // ── POST /api/projects/{projectId}/expenses/from-template/{templateId}

    /// <summary>
    /// Crea un gasto real a partir de una plantilla existente.
    /// Reutiliza: categoría, método de pago, moneda, descripción, exchange rate, alt currency.
    /// El usuario puede sobreescribir monto y fecha.
    /// </summary>
    /// <response code="201">Gasto creado desde la plantilla.</response>
    /// <response code="404">Plantilla no encontrada o no pertenece al proyecto.</response>
    /// <response code="400">El gasto origen no es una plantilla.</response>
    [HttpPost("from-template/{templateId:guid}")]
    [Authorize(Policy = "ProjectEditor")]
    [ProducesResponseType(typeof(ExpenseResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateFromTemplate(
        Guid projectId,
        Guid templateId,
        [FromBody] CreateFromTemplateRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var template = await _expenseService.GetByIdAsync(templateId, ct);
        if (template is null || template.ExpProjectId != projectId)
            return NotFound(new { message = "Template not found in this project." });

        if (!template.ExpIsTemplate)
            return BadRequest(new { message = "The specified expense is not a template." });

        var userId = User.GetRequiredUserId();
        var expense = template.ToEntityFromTemplate(projectId, userId, request);
        await _expenseService.CreateAsync(expense, ct);

        return CreatedAtAction(
            nameof(GetById),
            new { projectId, expenseId = expense.ExpId },
            expense.ToResponse());
    }

    // ── GET /api/projects/{projectId}/expenses/{expenseId} ──

    /// <summary>
    /// Obtiene un gasto por ID. Requiere al menos ser viewer del proyecto.
    /// </summary>
    /// <response code="200">Gasto encontrado.</response>
    /// <response code="404">Gasto no encontrado o no pertenece al proyecto.</response>
    [HttpGet("{expenseId:guid}")]
    [Authorize(Policy = "ProjectViewer")]
    [ProducesResponseType(typeof(ExpenseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid projectId, Guid expenseId, CancellationToken ct)
    {
        var expense = await _expenseService.GetByIdAsync(expenseId, ct);
        if (expense == null || expense.ExpProjectId != projectId)
            return NotFound(new { message = "Expense not found in this project." });

        return Ok(expense.ToResponse());
    }

    // ── POST /api/projects/{projectId}/expenses ─────────────

    /// <summary>
    /// Crea un gasto en el proyecto. Requiere rol editor+.
    /// Valida límite de gastos por mes según el plan del owner.
    ///
    /// PROTECCIÓN CONTRA ESCALAMIENTO DE PRIVILEGIOS:
    /// - ProjectId viene de la RUTA, no del body
    /// - CreatedByUserId viene del JWT, no del body
    /// </summary>
    /// <response code="201">Gasto creado.</response>
    /// <response code="400">Datos inválidos.</response>
    /// <response code="403">Sin acceso o plan no permite más gastos.</response>
    [HttpPost]
    [Authorize(Policy = "ProjectEditor")]
    [ProducesResponseType(typeof(ExpenseResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        Guid projectId,
        [FromBody] CreateExpenseRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // ⚠️ userId del JWT — NUNCA del request body
        var userId = User.GetRequiredUserId();

        // Validación imperativa extra (defensa en profundidad)
        await _accessService.ValidateAccessAsync(userId, projectId, ProjectRoles.Editor, ct);

        var expense = request.ToEntity(projectId, userId);
        await _expenseService.CreateAsync(expense, ct);

        return CreatedAtAction(
            nameof(GetById),
            new { projectId, expenseId = expense.ExpId },
            expense.ToResponse());
    }

    // ── PUT /api/projects/{projectId}/expenses/{expenseId} ──

    /// <summary>
    /// Actualiza un gasto. Requiere rol editor+.
    /// Valida que el gasto pertenezca al proyecto de la ruta.
    /// </summary>
    /// <response code="200">Gasto actualizado.</response>
    /// <response code="400">Datos inválidos.</response>
    /// <response code="404">Gasto no encontrado o no pertenece al proyecto.</response>
    /// <response code="403">Sin acceso al proyecto.</response>
    [HttpPut("{expenseId:guid}")]
    [Authorize(Policy = "ProjectEditor")]
    [ProducesResponseType(typeof(ExpenseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(
        Guid projectId,
        Guid expenseId,
        [FromBody] UpdateExpenseRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        await _accessService.ValidateAccessAsync(userId, projectId, ProjectRoles.Editor, ct);

        var expense = await _expenseService.GetByIdAsync(expenseId, ct);
        if (expense == null || expense.ExpProjectId != projectId)
            return NotFound(new { message = "Expense not found in this project." });

        expense.ApplyUpdate(request);
        await _expenseService.UpdateAsync(expense, ct);

        return Ok(expense.ToResponse());
    }

    // ── DELETE /api/projects/{projectId}/expenses/{expenseId}

    /// <summary>
    /// Soft-delete de un gasto. Requiere rol editor+.
    /// </summary>
    /// <response code="204">Gasto eliminado.</response>
    /// <response code="404">Gasto no encontrado o no pertenece al proyecto.</response>
    /// <response code="403">Sin acceso al proyecto.</response>
    [HttpDelete("{expenseId:guid}")]
    [Authorize(Policy = "ProjectEditor")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(
        Guid projectId, Guid expenseId,
        CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        await _accessService.ValidateAccessAsync(userId, projectId, ProjectRoles.Editor, ct);

        var expense = await _expenseService.GetByIdAsync(expenseId, ct);
        if (expense == null || expense.ExpProjectId != projectId)
            return NotFound(new { message = "Expense not found in this project." });

        await _expenseService.SoftDeleteAsync(expenseId, userId, ct);

        return NoContent();
    }
}
