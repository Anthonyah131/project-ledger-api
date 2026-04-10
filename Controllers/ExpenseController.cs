using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.Resources;
using ProjectLedger.API.DTOs.Expense;
using ProjectLedger.API.Extensions.Mappings;
using ProjectLedger.API.Models;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Expense controller with multi-tenant authorization.
/// 
/// Nested route: /api/projects/{projectId}/expenses
/// projectId ALWAYS comes from the route, never from the body.
/// userId ALWAYS comes from the JWT, never from the body.
/// 
/// Uses [Authorize(Policy = "...")] for declarative validation
/// + IProjectAccessService for extra imperative validation.
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
    private readonly IStringLocalizer<Messages> _localizer;

    public ExpenseController(
        IExpenseService expenseService,
        IProjectAccessService accessService,
        IProjectService projectService,
        IPlanAuthorizationService planAuth,
        IAuditLogService auditLogService,
        ITransactionCurrencyExchangeService exchangeService,
        IExpenseDocumentIntelligenceService expenseDocumentAiService,
        IStringLocalizer<Messages> localizer)
    {
        _expenseService = expenseService;
        _accessService = accessService;
        _projectService = projectService;
        _planAuth = planAuth;
        _auditLogService = auditLogService;
        _exchangeService = exchangeService;
        _expenseDocumentAiService = expenseDocumentAiService;
        _localizer = localizer;
    }

    // ── GET /api/projects/{projectId}/expenses ──────────────

    /// <summary>
    /// Lists all project expenses with pagination.
    /// </summary>
    /// <response code="200">Paged list of project expenses.</response>
    /// <response code="403">No access to the project.</response>
    [HttpGet]
    [Authorize(Policy = "ProjectViewer")]
    [ProducesResponseType(typeof(PagedResponse<ExpenseResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetByProject(
        Guid projectId,
        [FromQuery] PagedRequest pagination,
        [FromQuery] bool includeDeleted = false,
        [FromQuery] bool? isActive = null,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        CancellationToken ct = default)
    {
        if (from.HasValue && to.HasValue && from > to)
            return BadRequest(LocalizedResponse.Create("VALIDATION_ERROR", _localizer["InvalidDateRange"]));

        // Only Editor+ can view deleted expenses
        if (includeDeleted)
        {
            var userId = User.GetRequiredUserId();
            await _accessService.ValidateAccessAsync(userId, projectId, ProjectRoles.Editor, ct);
        }

        var (items, totalCount) = await _expenseService.GetByProjectIdPagedAsync(
            projectId, includeDeleted, isActive, pagination.Skip, pagination.PageSize,
            pagination.SortBy, pagination.IsDescending, from, to, ct);

        var response = PagedResponse<ExpenseResponse>.Create(
            items.ToResponse().ToList(), totalCount, pagination);

        return Ok(response);
    }

    // ── GET /api/projects/{projectId}/expenses/templates ───

    /// <summary>
    /// Lists all project expense templates.
    /// Templates are not actual financial movements.
    /// </summary>
    /// <response code="200">List of templates.</response>
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
    /// Creates an actual expense from an existing template.
    /// Reuses: category, payment method, currency, description, exchange rate, alt currency.
    /// The user can override amount and date.
    /// </summary>
    /// <response code="201">Expense created from the template.</response>
    /// <response code="404">Template not found or does not belong to the project.</response>
    /// <response code="400">Source expense is not a template.</response>
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
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["TemplateNotFound"]));

        if (!template.ExpIsTemplate)
            return BadRequest(LocalizedResponse.Create("VALIDATION_ERROR", _localizer["ExpenseNotTemplate"]));

        var userId = User.GetRequiredUserId();
        var expense = template.ToEntityFromTemplate(projectId, userId, request);

        await _planAuth.ValidateProjectWriteAccessAsync(projectId, userId, ct);

        await _expenseService.CreateAsync(expense, ct: ct);

        // Copy exchanges from template to new expense
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
    /// Gets an expense by ID. Requires at least viewer role on the project.
    /// </summary>
    /// <response code="200">Expense found.</response>
    /// <response code="404">Expense not found or does not belong to the project.</response>
    [HttpGet("{expenseId:guid}")]
    [Authorize(Policy = "ProjectViewer")]
    [ProducesResponseType(typeof(ExpenseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid projectId, Guid expenseId, CancellationToken ct)
    {
        var expense = await _expenseService.GetByIdAsync(expenseId, ct);
        if (expense == null || expense.ExpProjectId != projectId)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["ExpenseNotFound"]));

        return Ok(expense.ToResponse());
    }

    // ── GET /api/projects/{projectId}/expenses/extract-from-image/quota ──

    /// <summary>
    /// Returns the monthly document reading (OCR) quota for this project,
    /// governed by the project owner's plan.
    /// </summary>
    /// <response code="200">Monthly quota and remaining reads.</response>
    /// <response code="403">No edit access to the project.</response>
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
    /// Analyzes an image/PDF of a receipt or invoice and returns an expense draft
    /// for the user to review and adjust manually before saving.
    /// </summary>
    /// <response code="200">Expense draft extracted by AI.</response>
    /// <response code="400">Invalid file or extraction error.</response>
    /// <response code="403">No access to the project.</response>
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
            transactionKind: "expense",
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
    /// Creates an expense in the project. Requires editor+ role.
    /// Validates the monthly expenses limit according to the owner's plan.
    ///
    /// PRIVILEGE ESCALATION PROTECTION:
    /// - ProjectId comes from the ROUTE, not the body
    /// - CreatedByUserId comes from the JWT, not the body
    /// </summary>
    /// <response code="201">Expense created.</response>
    /// <response code="400">Invalid data.</response>
    /// <response code="403">No access or plan limits exceeded.</response>
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

        // ⚠️ userId from JWT — NEVER from request body
        var userId = User.GetRequiredUserId();

        // Validate owner's plan allows write access (and sharing if member)
        await _planAuth.ValidateProjectWriteAccessAsync(projectId, userId, ct);

        // Extra imperative validation (defense in depth)
        await _accessService.ValidateAccessAsync(userId, projectId, ProjectRoles.Editor, ct);

        var expense = request.ToEntity(projectId, userId);
        var splits = request.Splits?.Select(s => new SplitInput(
            s.PartnerId, s.SplitType, s.SplitValue, s.ResolvedAmount,
            s.CurrencyExchanges?.Select(ce => new SplitCurrencyExchangeInput(ce.CurrencyCode, ce.ExchangeRate, ce.ConvertedAmount)).ToList()
        )).ToList();
        await _expenseService.CreateAsync(expense, splits, ct);

        // Save alternative currency conversions
        if (request.CurrencyExchanges?.Count > 0)
        {
            await _exchangeService.SaveExchangesAsync("expense", expense.ExpId, request.CurrencyExchanges, ct);
            // Re-fetch to include exchanges in response
            expense = (await _expenseService.GetByIdAsync(expense.ExpId, ct))!;
        }

        return CreatedAtAction(
            nameof(GetById),
            new { projectId, expenseId = expense.ExpId },
            expense.ToResponse());
    }

    // ── POST /api/projects/{projectId}/expenses/bulk ────────

    /// <summary>
    /// Fast import: creates up to 100 expenses in a single request.
    /// Common fields (category, payment method, currency, exchange rate) are sent
    /// once at the batch level. The converted_amount is calculated as amount × exchangeRate.
    /// All-or-nothing operation: if any item validation fails, none are created.
    /// </summary>
    /// <response code="201">Expenses created.</response>
    /// <response code="400">Invalid data.</response>
    /// <response code="403">No access or plan limits exceeded.</response>
    [HttpPost("bulk")]
    [Authorize(Policy = "ProjectEditor")]
    [ProducesResponseType(typeof(BulkCreateExpenseResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> BulkCreate(
        Guid projectId,
        [FromBody] BulkCreateExpenseRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        await _planAuth.ValidateProjectWriteAccessAsync(projectId, userId, ct);
        await _accessService.ValidateAccessAsync(userId, projectId, ProjectRoles.Editor, ct);

        var items = request.Items.Select(item =>
        {
            var expense = new Expense
            {
                ExpId = Guid.NewGuid(),
                ExpProjectId = projectId,
                ExpCreatedByUserId = userId,
                ExpCategoryId = item.CategoryId,
                ExpPaymentMethodId = item.PaymentMethodId,
                ExpObligationId = item.ObligationId,
                ExpObligationEquivalentAmount = item.ObligationEquivalentAmount,
                ExpOriginalAmount = item.OriginalAmount,
                ExpOriginalCurrency = item.OriginalCurrency,
                ExpExchangeRate = item.ExchangeRate,
                ExpConvertedAmount = item.ConvertedAmount,
                ExpAccountAmount = item.AccountAmount,
                ExpTitle = item.Title,
                ExpDescription = item.Description,
                ExpExpenseDate = item.Date,
                ExpNotes = item.Notes,
                ExpIsTemplate = false,
                ExpIsActive = true
            };
            var splits = item.Splits?.Select(s => new SplitInput(
                s.PartnerId, s.SplitType, s.SplitValue, s.ResolvedAmount,
                s.CurrencyExchanges?.Select(ce => new SplitCurrencyExchangeInput(
                    ce.CurrencyCode, ce.ExchangeRate, ce.ConvertedAmount)).ToList()
            )).ToList();
            var exchanges = item.CurrencyExchanges?.Select(ce =>
                new TransactionExchangeInput(ce.CurrencyCode, ce.ExchangeRate, ce.ConvertedAmount)).ToList();
            return (expense, (IReadOnlyList<SplitInput>?)splits, (IReadOnlyList<TransactionExchangeInput>?)exchanges);
        }).ToList();

        var created = await _expenseService.BulkCreateAsync(items, ct);

        var response = new BulkCreateExpenseResponse
        {
            Created = created.Count,
            Items = created.Select(e => new BulkCreatedItemResponse
            {
                Id = e.ExpId,
                Title = e.ExpTitle,
                OriginalAmount = e.ExpOriginalAmount,
                ConvertedAmount = e.ExpConvertedAmount,
                Date = e.ExpExpenseDate
            }).ToList()
        };

        return StatusCode(StatusCodes.Status201Created, response);
    }

    // ── PUT /api/projects/{projectId}/expenses/{expenseId} ──

    /// <summary>
    /// Updates an expense. Requires editor+ role.
    /// Validates that the expense belongs to the route's project.
    /// </summary>
    /// <response code="200">Expense updated.</response>
    /// <response code="400">Invalid data.</response>
    /// <response code="404">Expense not found or does not belong to the project.</response>
    /// <response code="403">No access to the project.</response>
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
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["ExpenseNotFound"]));

        expense.ApplyUpdate(request);
        var updateSplits = request.Splits?.Select(s => new SplitInput(
            s.PartnerId, s.SplitType, s.SplitValue, s.ResolvedAmount,
            s.CurrencyExchanges?.Select(ce => new SplitCurrencyExchangeInput(ce.CurrencyCode, ce.ExchangeRate, ce.ConvertedAmount)).ToList()
        )).ToList();
        await _expenseService.UpdateAsync(expense, updateSplits, ct);

        // Update alternative currency conversions
        if (request.CurrencyExchanges is not null)
            await _exchangeService.ReplaceExchangesAsync("expense", expense.ExpId, request.CurrencyExchanges, ct);

        expense = (await _expenseService.GetByIdAsync(expense.ExpId, ct))!;
        return Ok(expense.ToResponse());
    }

    // ── PATCH /api/projects/{projectId}/expenses/{expenseId}/active-state ──

    /// <summary>
    /// Activates or deactivates an expense without requiring the full update payload.
    /// </summary>
    /// <response code="200">Expense active state updated.</response>
    /// <response code="404">Expense not found or does not belong to the project.</response>
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
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["ExpenseNotFound"]));

        expense.ExpIsActive = request.IsActive;
        await _expenseService.UpdateAsync(expense, ct: ct);

        expense = (await _expenseService.GetByIdAsync(expense.ExpId, ct))!;
        return Ok(expense.ToResponse());
    }

    // ── DELETE /api/projects/{projectId}/expenses/{expenseId}

    /// <summary>
    /// Soft-deletes an expense. Requires editor+ role.
    /// </summary>
    /// <response code="204">Expense deleted.</response>
    /// <response code="404">Expense not found or does not belong to the project.</response>
    /// <response code="403">No access to the project.</response>
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
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["ExpenseNotFound"]));

        await _expenseService.SoftDeleteAsync(expenseId, userId, ct);

        return NoContent();
    }

    private async Task<DocumentReadQuotaResponse> BuildDocumentReadQuotaAsync(Guid projectId, CancellationToken ct)
    {
        var project = await _projectService.GetByIdAsync(projectId, ct)
            ?? throw new KeyNotFoundException("ProjectNotFound");

        if (project.PrjIsDeleted)
            throw new KeyNotFoundException("ProjectNotFound");

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
