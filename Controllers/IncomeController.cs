using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.DTOs.Expense;
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
    private const string OcrUsageEntityName = "expense_document_read";
    // Must match allowed values in audit_logs_action_type_check.
    private const string OcrUsageActionType = "create";

    private readonly IIncomeService _incomeService;
    private readonly IProjectAccessService _accessService;
    private readonly IProjectService _projectService;
    private readonly IPlanAuthorizationService _planAuth;
    private readonly IAuditLogService _auditLogService;
    private readonly ITransactionCurrencyExchangeService _exchangeService;
    private readonly IExpenseDocumentIntelligenceService _expenseDocumentAiService;

    public IncomeController(
        IIncomeService incomeService,
        IProjectAccessService accessService,
        IProjectService projectService,
        IPlanAuthorizationService planAuth,
        IAuditLogService auditLogService,
        ITransactionCurrencyExchangeService exchangeService,
        IExpenseDocumentIntelligenceService expenseDocumentAiService)
    {
        _incomeService = incomeService;
        _accessService = accessService;
        _projectService = projectService;
        _planAuth = planAuth;
        _auditLogService = auditLogService;
        _exchangeService = exchangeService;
        _expenseDocumentAiService = expenseDocumentAiService;
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

    // ── POST /api/projects/{projectId}/incomes/extract-from-image ──────────

    // ── GET /api/projects/{projectId}/incomes/extract-from-image/quota ─────

    /// <summary>
    /// Retorna el cupo mensual de lecturas de documentos (OCR) para este proyecto,
    /// gobernado por el plan del owner del proyecto.
    /// </summary>
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

    // ── POST /api/projects/{projectId}/incomes/extract-from-image ──────────

    /// <summary>
    /// Analiza una imagen/PDF y retorna un borrador de ingreso para pre-llenar
    /// el formulario de creación.
    /// </summary>
    [HttpPost("extract-from-image")]
    [Authorize(Policy = "ProjectEditor")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ExtractIncomeFromDocumentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExtractFromImage(
        Guid projectId,
        [FromForm] ExtractIncomeFromDocumentRequest request,
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

        var expenseExtraction = await _expenseDocumentAiService.ExtractDraftAsync(
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
                Source = "azure-document-intelligence",
                Target = "income"
            },
            ct: ct);

        var response = new ExtractIncomeFromDocumentResponse
        {
            Provider = expenseExtraction.Provider,
            DocumentKind = expenseExtraction.DocumentKind,
            ModelId = expenseExtraction.ModelId,
            Draft = new IncomeDocumentDraftResponse
            {
                CategoryId = expenseExtraction.SuggestedCategory?.CategoryId,
                PaymentMethodId = expenseExtraction.SuggestedPaymentMethod?.PaymentMethodId,
                OriginalAmount = expenseExtraction.Draft.OriginalAmount,
                OriginalCurrency = expenseExtraction.Draft.OriginalCurrency,
                ExchangeRate = expenseExtraction.Draft.ExchangeRate,
                ConvertedAmount = expenseExtraction.Draft.ConvertedAmount,
                AccountAmount = null,
                AccountCurrency = expenseExtraction.AvailablePaymentMethods
                    .FirstOrDefault(pm => pm.PaymentMethodId == expenseExtraction.SuggestedPaymentMethod?.PaymentMethodId)
                    ?.Currency,
                Title = expenseExtraction.Draft.Title,
                Description = expenseExtraction.Draft.Description,
                IncomeDate = expenseExtraction.Draft.ExpenseDate,
                ReceiptNumber = expenseExtraction.Draft.ReceiptNumber,
                Notes = expenseExtraction.Draft.Notes,
                CurrencyExchanges = expenseExtraction.Draft.CurrencyExchanges,
                DetectedMerchantName = expenseExtraction.Draft.DetectedMerchantName,
                DetectedPaymentMethodText = expenseExtraction.Draft.DetectedPaymentMethodText
            },
            SuggestedCategory = expenseExtraction.SuggestedCategory is null
                ? null
                : new SuggestedIncomeCategoryResponse
                {
                    CategoryId = expenseExtraction.SuggestedCategory.CategoryId,
                    Name = expenseExtraction.SuggestedCategory.Name,
                    Confidence = expenseExtraction.SuggestedCategory.Confidence,
                    Reason = expenseExtraction.SuggestedCategory.Reason
                },
            SuggestedPaymentMethod = expenseExtraction.SuggestedPaymentMethod is null
                ? null
                : new SuggestedIncomePaymentMethodResponse
                {
                    PaymentMethodId = expenseExtraction.SuggestedPaymentMethod.PaymentMethodId,
                    Name = expenseExtraction.SuggestedPaymentMethod.Name,
                    Type = expenseExtraction.SuggestedPaymentMethod.Type,
                    Confidence = expenseExtraction.SuggestedPaymentMethod.Confidence,
                    Reason = expenseExtraction.SuggestedPaymentMethod.Reason
                },
            AvailableCategories = expenseExtraction.AvailableCategories
                .Select(c => new IncomeCategoryOptionResponse
                {
                    CategoryId = c.CategoryId,
                    Name = c.Name,
                    IsDefault = c.IsDefault
                })
                .ToList(),
            AvailablePaymentMethods = expenseExtraction.AvailablePaymentMethods
                .Select(pm => new IncomePaymentMethodOptionResponse
                {
                    PaymentMethodId = pm.PaymentMethodId,
                    Name = pm.Name,
                    Type = pm.Type,
                    Currency = pm.Currency,
                    BankName = pm.BankName,
                    AccountNumber = pm.AccountNumber
                })
                .ToList(),
            Warnings = expenseExtraction.Warnings
                .Select(w => w.Replace("expense", "income", StringComparison.OrdinalIgnoreCase))
                .ToList()
        };

        return Ok(response);
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
        await _accessService.ValidateAccessAsync(userId, projectId, ProjectRoles.Editor, ct);

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
        await _accessService.ValidateAccessAsync(userId, projectId, ProjectRoles.Editor, ct);

        var income = await _incomeService.GetByIdAsync(incomeId, ct);
        if (income == null || income.IncProjectId != projectId)
            return NotFound(new { message = "Income not found in this project." });

        await _incomeService.SoftDeleteAsync(incomeId, userId, ct);
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
