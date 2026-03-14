using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Common;
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
    private const string OcrUsageEntityName = "expense_document_read";
    // Must match allowed values in audit_logs_action_type_check.
    private const string OcrUsageActionType = "create";

    private readonly IExpenseService _expenseService;
    private readonly IProjectAccessService _accessService;
    private readonly IProjectService _projectService;
    private readonly IPlanAuthorizationService _planAuth;
    private readonly IAuditLogService _auditLogService;
    private readonly ITransactionCurrencyExchangeService _exchangeService;
    private readonly IExpenseDocumentIntelligenceService _expenseDocumentAiService;

    public ExpenseController(
        IExpenseService expenseService,
        IProjectAccessService accessService,
        IProjectService projectService,
        IPlanAuthorizationService planAuth,
        IAuditLogService auditLogService,
        ITransactionCurrencyExchangeService exchangeService,
        IExpenseDocumentIntelligenceService expenseDocumentAiService)
    {
        _expenseService = expenseService;
        _accessService = accessService;
        _projectService = projectService;
        _planAuth = planAuth;
        _auditLogService = auditLogService;
        _exchangeService = exchangeService;
        _expenseDocumentAiService = expenseDocumentAiService;
    }

    // ── GET /api/projects/{projectId}/expenses ──────────────

    /// <summary>
    /// Lista todos los gastos del proyecto con paginación.
    /// </summary>
    /// <response code="200">Lista paginada de gastos del proyecto.</response>
    /// <response code="403">Sin acceso al proyecto.</response>
    [HttpGet]
    [Authorize(Policy = "ProjectViewer")]
    [ProducesResponseType(typeof(PagedResponse<ExpenseResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetByProject(
        Guid projectId,
        [FromQuery] PagedRequest pagination,
        [FromQuery] bool includeDeleted = false,
        [FromQuery] bool? isActive = null,
        CancellationToken ct = default)
    {
        // Solo Editor+ puede ver gastos eliminados
        if (includeDeleted)
        {
            var userId = User.GetRequiredUserId();
            await _accessService.ValidateAccessAsync(userId, projectId, ProjectRoles.Editor, ct);
        }

        var (items, totalCount) = await _expenseService.GetByProjectIdPagedAsync(
            projectId, includeDeleted, isActive, pagination.Skip, pagination.PageSize,
            pagination.SortBy, pagination.IsDescending, ct);

        var response = PagedResponse<ExpenseResponse>.Create(
            items.ToResponse().ToList(), totalCount, pagination);

        return Ok(response);
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

        await _planAuth.ValidateProjectWriteAccessAsync(projectId, userId, ct);

        await _expenseService.CreateAsync(expense, ct);

        // Copiar los exchanges de la plantilla al nuevo gasto
        if (template.CurrencyExchanges.Count > 0)
        {
            var templateExchanges = template.CurrencyExchanges
                .Select(x => new CurrencyExchangeRequest
                {
                    CurrencyCode = x.TceCurrencyCode,
                    ExchangeRate = x.TceExchangeRate,
                    ConvertedAmount = x.TceConvertedAmount
                }).ToList();
            await _exchangeService.SaveExchangesAsync("expense", expense.ExpId, templateExchanges, ct);
            expense = (await _expenseService.GetByIdAsync(expense.ExpId, ct))!;
        }

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

    // ── GET /api/projects/{projectId}/expenses/extract-from-image/quota ──

    /// <summary>
    /// Retorna el cupo mensual de lecturas de documentos (OCR) para este proyecto,
    /// gobernado por el plan del owner del proyecto.
    /// </summary>
    /// <response code="200">Cupo mensual y lecturas restantes.</response>
    /// <response code="403">Sin acceso de edición al proyecto.</response>
    [HttpGet("extract-from-image/quota")]
    [Authorize(Policy = "ProjectEditor")]
    [ProducesResponseType(typeof(DocumentReadQuotaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDocumentReadQuota(Guid projectId, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        await _planAuth.ValidateProjectWriteAccessAsync(projectId, userId, ct);
        await _accessService.ValidateAccessAsync(userId, projectId, ProjectRoles.Editor, ct);

        var quota = await BuildDocumentReadQuotaAsync(projectId, ct);
        return Ok(quota);
    }

    // ── POST /api/projects/{projectId}/expenses/extract-from-image ──

    /// <summary>
    /// Analiza una imagen/PDF de recibo o factura y retorna un borrador de gasto
    /// para que el usuario lo revise y lo ajuste manualmente antes de guardar.
    /// </summary>
    /// <response code="200">Borrador de gasto extraido por IA.</response>
    /// <response code="400">Archivo invalido o error de extraccion.</response>
    /// <response code="403">Sin acceso al proyecto.</response>
    [HttpPost("extract-from-image")]
    [Authorize(Policy = "ProjectEditor")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ExtractExpenseFromDocumentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExtractFromImage(
        Guid projectId,
        [FromForm] ExtractExpenseFromDocumentRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        await _planAuth.ValidateProjectWriteAccessAsync(projectId, userId, ct);
        await _accessService.ValidateAccessAsync(userId, projectId, ProjectRoles.Editor, ct);

        var quota = await BuildDocumentReadQuotaAsync(projectId, ct);

        if (!quota.CanUseOcr)
            throw new PlanDeniedException(PlanPermission.CanUseOcr, quota.PlanName);

        if (quota.MonthlyLimit is not null && quota.UsedThisMonth >= quota.MonthlyLimit.Value)
            throw new PlanLimitExceededException(
                PlanLimits.MaxDocumentReadsPerMonth,
                quota.MonthlyLimit.Value,
                quota.PlanName);

        var response = await _expenseDocumentAiService.ExtractDraftAsync(
            projectId,
            request.File,
            request.DocumentKind,
            ct);

        await _auditLogService.LogAsync(
            OcrUsageEntityName,
            projectId,
            OcrUsageActionType,
            quota.ProjectOwnerUserId,
            newValues: new
            {
                ProjectId = projectId,
                RequestedByUserId = userId,
                request.DocumentKind,
                FileName = request.File.FileName,
                Source = "azure-document-intelligence"
            },
            ct: ct);

        return Ok(response);
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

        // Validar que el plan del owner permite escritura (y sharing si es miembro)
        await _planAuth.ValidateProjectWriteAccessAsync(projectId, userId, ct);

        // Validación imperativa extra (defensa en profundidad)
        await _accessService.ValidateAccessAsync(userId, projectId, ProjectRoles.Editor, ct);

        var expense = request.ToEntity(projectId, userId);
        await _expenseService.CreateAsync(expense, ct);

        // Guardar conversiones a monedas alternativas
        if (request.CurrencyExchanges?.Count > 0)
        {
            await _exchangeService.SaveExchangesAsync("expense", expense.ExpId, request.CurrencyExchanges, ct);
            // Re-fetch para incluir exchanges en la respuesta
            expense = (await _expenseService.GetByIdAsync(expense.ExpId, ct))!;
        }

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
        await _planAuth.ValidateProjectWriteAccessAsync(projectId, userId, ct);
        await _accessService.ValidateAccessAsync(userId, projectId, ProjectRoles.Editor, ct);

        var expense = await _expenseService.GetByIdAsync(expenseId, ct);
        if (expense == null || expense.ExpProjectId != projectId)
            return NotFound(new { message = "Expense not found in this project." });

        expense.ApplyUpdate(request);
        await _expenseService.UpdateAsync(expense, ct);

        // Actualizar conversiones a monedas alternativas
        if (request.CurrencyExchanges is not null)
        {
            await _exchangeService.ReplaceExchangesAsync("expense", expense.ExpId, request.CurrencyExchanges, ct);
        }

        expense = (await _expenseService.GetByIdAsync(expense.ExpId, ct))!;
        return Ok(expense.ToResponse());
    }

    // ── PATCH /api/projects/{projectId}/expenses/{expenseId}/active-state ──

    /// <summary>
    /// Activa o desactiva un gasto sin requerir el payload completo de actualización.
    /// </summary>
    /// <response code="200">Estado del gasto actualizado.</response>
    /// <response code="404">Gasto no encontrado o no pertenece al proyecto.</response>
    [HttpPatch("{expenseId:guid}/active-state")]
    [Authorize(Policy = "ProjectEditor")]
    [ProducesResponseType(typeof(ExpenseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateActiveState(
        Guid projectId,
        Guid expenseId,
        [FromBody] UpdateExpenseActiveStateRequest request,
        CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        await _planAuth.ValidateProjectWriteAccessAsync(projectId, userId, ct);
        await _accessService.ValidateAccessAsync(userId, projectId, ProjectRoles.Editor, ct);

        var expense = await _expenseService.GetByIdAsync(expenseId, ct);
        if (expense == null || expense.ExpProjectId != projectId)
            return NotFound(new { message = "Expense not found in this project." });

        expense.ExpIsActive = request.IsActive;
        await _expenseService.UpdateAsync(expense, ct);

        expense = (await _expenseService.GetByIdAsync(expense.ExpId, ct))!;
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
        await _planAuth.ValidateProjectWriteAccessAsync(projectId, userId, ct);
        await _accessService.ValidateAccessAsync(userId, projectId, ProjectRoles.Editor, ct);

        var expense = await _expenseService.GetByIdAsync(expenseId, ct);
        if (expense == null || expense.ExpProjectId != projectId)
            return NotFound(new { message = "Expense not found in this project." });

        await _expenseService.SoftDeleteAsync(expenseId, userId, ct);

        return NoContent();
    }

    private async Task<DocumentReadQuotaResponse> BuildDocumentReadQuotaAsync(Guid projectId, CancellationToken ct)
    {
        var project = await _projectService.GetByIdAsync(projectId, ct)
            ?? throw new KeyNotFoundException($"Project '{projectId}' not found.");

        if (project.PrjIsDeleted)
            throw new KeyNotFoundException($"Project '{projectId}' not found.");

        var ownerUserId = project.PrjOwnerUserId;
        var capabilities = await _planAuth.GetCapabilitiesAsync(ownerUserId, ct);

        var canUseOcr = capabilities.Permissions.TryGetValue(nameof(PlanPermission.CanUseOcr), out var hasOcrPermission)
            && hasOcrPermission;

        var monthlyLimit = capabilities.Limits.TryGetValue(PlanLimits.MaxDocumentReadsPerMonth, out var configuredLimit)
            ? configuredLimit
            : null;

        var periodStartUtc = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEndUtc = periodStartUtc.AddMonths(1);

        var usedThisMonth = await _auditLogService.CountByUserAndActionInRangeAsync(
            ownerUserId,
            OcrUsageEntityName,
            OcrUsageActionType,
            periodStartUtc,
            periodEndUtc,
            ct);

        var isUnlimited = monthlyLimit is null;
        int? remainingThisMonth = isUnlimited
            ? null
            : Math.Max(monthlyLimit.GetValueOrDefault() - usedThisMonth, 0);

        return new DocumentReadQuotaResponse
        {
            ProjectOwnerUserId = ownerUserId,
            PlanName = capabilities.PlanName,
            PlanSlug = capabilities.PlanSlug,
            CanUseOcr = canUseOcr,
            UsedThisMonth = usedThisMonth,
            MonthlyLimit = monthlyLimit,
            RemainingThisMonth = remainingThisMonth,
            IsUnlimited = isUnlimited,
            IsAvailable = canUseOcr && (isUnlimited || usedThisMonth < monthlyLimit.GetValueOrDefault()),
            PeriodStartUtc = periodStartUtc,
            PeriodEndUtc = periodEndUtc
        };
    }
}
