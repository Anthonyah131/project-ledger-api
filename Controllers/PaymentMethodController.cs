using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.DTOs.Expense;
using ProjectLedger.API.DTOs.Income;
using ProjectLedger.API.DTOs.PaymentMethod;
using ProjectLedger.API.Extensions.Mappings;
using ProjectLedger.API.Resources;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Authenticated user's payment methods controller.
/// 
/// Route: /api/payment-methods
/// - OwnerUserId is ALWAYS obtained from the JWT, never from the body.
/// - Plan validates MaxPaymentMethods upon creation.
/// - Only the owner can view/edit/delete their payment methods.
/// </summary>
[ApiController]
[Route("api/payment-methods")]
[Authorize]
[Tags("Payment Methods")]
[Produces("application/json")]
public class PaymentMethodController : ControllerBase
{
    private readonly IPaymentMethodService _paymentMethodService;
    private readonly IExpenseService _expenseService;
    private readonly IIncomeService _incomeService;
    private readonly IProjectPaymentMethodService _projectPaymentMethodService;
    private readonly IStringLocalizer<Messages> _localizer;

    public PaymentMethodController(
        IPaymentMethodService paymentMethodService,
        IExpenseService expenseService,
        IIncomeService incomeService,
        IProjectPaymentMethodService projectPaymentMethodService,
        IStringLocalizer<Messages> localizer)
    {
        _paymentMethodService = paymentMethodService;
        _expenseService = expenseService;
        _incomeService = incomeService;
        _projectPaymentMethodService = projectPaymentMethodService;
        _localizer = localizer;
    }

    // ── GET /api/payment-methods/lookup ────────────────────────

    /// <summary>
    /// Lightweight paginated list of the authenticated user's payment methods.
    /// Designed for command palette, expense/income forms, and selectors.
    /// </summary>
    /// <response code="200">Paginated payment method lookup.</response>
    [HttpGet("lookup")]
    [ProducesResponseType(typeof(PaymentMethodLookupResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLookup(
        [FromQuery] LookupRequest request,
        CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();

        var (items, totalCount) = await _paymentMethodService.GetLookupAsync(
            userId, request.Search, request.Skip, request.PageSize, ct);

        var itemList = items.Select(pm => new PaymentMethodLookupItem
        {
            Id = pm.PmtId,
            Name = pm.PmtName,
            Type = pm.PmtType,
            Currency = pm.PmtCurrency
        }).ToList();

        return Ok(new PaymentMethodLookupResponse
        {
            Items = itemList,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount
        });
    }

    // ── GET /api/payment-methods ────────────────────────────

    /// <summary>
    /// Lists all of the authenticated user's payment methods.
    /// </summary>
    /// <response code="200">List of payment methods.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PaymentMethodResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyPaymentMethods(CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var methods = await _paymentMethodService.GetByOwnerUserIdAsync(userId, ct);
        return Ok(methods.ToResponse());
    }

    // ── GET /api/payment-methods/{id} ───────────────────────

    /// <summary>
    /// Gets a payment method by ID. Only the owner can view it.
    /// </summary>
    /// <response code="200">Payment method found.</response>
    /// <response code="404">Not found or does not belong to the user.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PaymentMethodResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var pm = await _paymentMethodService.GetByIdAsync(id, ct);

        if (pm is null || pm.PmtOwnerUserId != userId)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["PaymentMethodNotFound"]));

        return Ok(pm.ToResponse());
    }

    // ── POST /api/payment-methods ───────────────────────────

    /// <summary>
    /// Creates a payment method for the authenticated user.
    /// Validates the plan's MaxPaymentMethods limit.
    /// OwnerUserId comes from the JWT — NEVER from the body.
    /// </summary>
    /// <response code="201">Payment method created.</response>
    /// <response code="403">Plan limit exceeded.</response>
    [HttpPost]
    [ProducesResponseType(typeof(PaymentMethodResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePaymentMethodRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();

        var pm = request.ToEntity(userId);
        await _paymentMethodService.CreateAsync(pm, ct);

        return CreatedAtAction(
            nameof(GetById),
            new { id = pm.PmtId },
            pm.ToResponse());
    }

    // ── PUT /api/payment-methods/{id} ───────────────────────

    /// <summary>
    /// Updates a payment method. Only the owner can edit it.
    /// </summary>
    /// <response code="200">Payment method updated.</response>
    /// <response code="404">Not found or does not belong to the user.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PaymentMethodResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdatePaymentMethodRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        var pm = await _paymentMethodService.GetByIdAsync(id, ct);

        if (pm is null || pm.PmtOwnerUserId != userId)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["PaymentMethodNotFound"]));

        pm.ApplyUpdate(request);
        await _paymentMethodService.UpdateAsync(pm, ct);

        return Ok(pm.ToResponse());
    }

    // ── POST /api/payment-methods/{id}/partner ──────────────

    /// <summary>
    /// Links a partner to a payment method. Only the owner can do this.
    /// </summary>
    /// <response code="200">Partner linked.</response>
    /// <response code="404">Payment method or partner not found.</response>
    /// <response code="409">The payment method already has this partner linked.</response>
    [HttpPost("{id:guid}/partner")]
    [ProducesResponseType(typeof(PaymentMethodResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> LinkPartner(
        Guid id,
        [FromBody] LinkPartnerToPaymentMethodRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();

        try
        {
            var pm = await _paymentMethodService.LinkPartnerAsync(id, request.PartnerId, userId, ct);
            return Ok(pm.ToResponse());
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer[ex.Message]));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(LocalizedResponse.Create("CONFLICT", _localizer[ex.Message]));
        }
    }

    // ── DELETE /api/payment-methods/{id}/partner ─────────────

    /// <summary>
    /// Unlinks the partner from a payment method. Only the owner can do this.
    /// </summary>
    /// <response code="200">Partner unlinked.</response>
    /// <response code="404">Payment method not found.</response>
    /// <response code="409">The payment method does not have a linked partner.</response>
    [HttpDelete("{id:guid}/partner")]
    [ProducesResponseType(typeof(PaymentMethodResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UnlinkPartner(Guid id, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();

        try
        {
            var pm = await _paymentMethodService.UnlinkPartnerAsync(id, userId, ct);
            return Ok(pm.ToResponse());
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer[ex.Message]));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(LocalizedResponse.Create("CONFLICT", _localizer[ex.Message]));
        }
    }

    // ── DELETE /api/payment-methods/{id} ────────────────────

    /// <summary>
    /// Soft-deletes a payment method. Only the owner can delete it.
    /// </summary>
    /// <response code="400">The payment method cannot be deleted because it has active related movements.</response>
    /// <response code="204">Payment method deleted.</response>
    /// <response code="404">Not found or does not belong to the user.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var pm = await _paymentMethodService.GetByIdAsync(id, ct);

        if (pm is null || pm.PmtOwnerUserId != userId)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["PaymentMethodNotFound"]));

        await _paymentMethodService.SoftDeleteAsync(id, userId, ct);
        return NoContent();
    }

    // ── GET /api/payment-methods/{id}/expenses ────────────

    /// <summary>
    /// Gets all the movements (expenses) of a payment method (paginated),
    /// crossing all the user's projects.
    /// </summary>
    /// <response code="200">Paginated list of expenses associated with the payment method.</response>
    /// <response code="404">Payment method not found.</response>
    [HttpGet("{id:guid}/expenses")]
    [ProducesResponseType(typeof(PagedWithTotalsResponse<ExpenseResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetExpensesByPaymentMethod(
        Guid id,
        [FromQuery] PagedRequest pagination,
        [FromQuery] bool? isActive,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] Guid? projectId,
        CancellationToken ct)
    {
        if (from.HasValue && to.HasValue && from > to)
            return BadRequest(LocalizedResponse.Create("VALIDATION_ERROR", _localizer["InvalidDateRange"]));

        var userId = User.GetRequiredUserId();
        var pm = await _paymentMethodService.GetByIdAsync(id, ct);

        if (pm is null || pm.PmtOwnerUserId != userId)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["PaymentMethodNotFound"]));

        var (items, totalCount, totalActiveAmount) = await _expenseService.GetByPaymentMethodIdPagedAsync(
            id, isActive, pagination.Skip, pagination.PageSize,
            pagination.SortBy, pagination.IsDescending,
            from, to, projectId, ct);

        var response = PagedWithTotalsResponse<ExpenseResponse>.Create(
            items.ToResponse().ToList(), totalCount, pagination, totalActiveAmount);

        return Ok(response);
    }

    // ── GET /api/payment-methods/{id}/incomes ─────────────

    /// <summary>
    /// Gets all the movements (incomes) of a payment method (paginated),
    /// crossing all the user's projects.
    /// </summary>
    /// <response code="200">Paginated list of incomes associated with the payment method.</response>
    /// <response code="404">Payment method not found.</response>
    [HttpGet("{id:guid}/incomes")]
    [ProducesResponseType(typeof(PagedWithTotalsResponse<IncomeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetIncomesByPaymentMethod(
        Guid id,
        [FromQuery] PagedRequest pagination,
        [FromQuery] bool? isActive,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] Guid? projectId,
        CancellationToken ct)
    {
        if (from.HasValue && to.HasValue && from > to)
            return BadRequest(LocalizedResponse.Create("VALIDATION_ERROR", _localizer["InvalidDateRange"]));

        var userId = User.GetRequiredUserId();
        var pm = await _paymentMethodService.GetByIdAsync(id, ct);

        if (pm is null || pm.PmtOwnerUserId != userId)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["PaymentMethodNotFound"]));

        var (items, totalCount, totalActiveAmount) = await _incomeService.GetByPaymentMethodIdPagedAsync(
            id, isActive, pagination.Skip, pagination.PageSize,
            pagination.SortBy, pagination.IsDescending,
            from, to, projectId, ct);

        var response = PagedWithTotalsResponse<IncomeResponse>.Create(
            items.ToResponse().ToList(), totalCount, pagination, totalActiveAmount);

        return Ok(response);
    }

    // ── GET /api/payment-methods/{id}/projects ────────────

    /// <summary>
    /// Gets the projects linked to the payment method.
    /// </summary>
    /// <response code="200">List of projects related to the payment method.</response>
    /// <response code="404">Payment method not found.</response>
    [HttpGet("{id:guid}/projects")]
    [ProducesResponseType(typeof(PaymentMethodProjectsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProjectsByPaymentMethod(Guid id, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var pm = await _paymentMethodService.GetByIdAsync(id, ct);

        if (pm is null || pm.PmtOwnerUserId != userId)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["PaymentMethodNotFound"]));

        var links = await _projectPaymentMethodService.GetByPaymentMethodIdAsync(id, ct);
        var items = links
            .Select(link => link.Project)
            .Where(project => project is not null)
            .Select(project => new PaymentMethodProjectResponse
            {
                Id = project.PrjId,
                Name = project.PrjName,
                CurrencyCode = project.PrjCurrencyCode,
                Description = project.PrjDescription,
                OwnerUserId = project.PrjOwnerUserId,
                CreatedAt = project.PrjCreatedAt,
                UpdatedAt = project.PrjUpdatedAt
            })
            .ToList();

        var response = new PaymentMethodProjectsResponse
        {
            Items = items,
            TotalCount = items.Count
        };

        return Ok(response);
    }

    // ── GET /api/payment-methods/{id}/summary ─────────────

    /// <summary>
    /// Returns aggregated usage metrics for the payment method.
    /// </summary>
    /// <response code="200">Summary of expenses, incomes, and related projects.</response>
    /// <response code="404">Payment method not found.</response>
    [HttpGet("{id:guid}/summary")]
    [ProducesResponseType(typeof(PaymentMethodSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPaymentMethodSummary(Guid id, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var pm = await _paymentMethodService.GetByIdAsync(id, ct);

        if (pm is null || pm.PmtOwnerUserId != userId)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["PaymentMethodNotFound"]));

        var expenses = await _expenseService.GetByPaymentMethodIdAsync(id, ct);
        var incomes = await _incomeService.GetByPaymentMethodIdAsync(id, ct);
        var links = await _projectPaymentMethodService.GetByPaymentMethodIdAsync(id, ct);

        var pmCurrency = pm.PmtCurrency;

        var summary = new PaymentMethodSummaryResponse
        {
            RelatedExpensesCount = expenses.Count(),
            RelatedIncomesCount = incomes.Count(),
            RelatedProjectsCount = links.Count(),
            TotalExpenseAmount = expenses.Sum(e =>
                e.ExpAccountAmount ??
                (e.ExpOriginalCurrency == pmCurrency ? e.ExpOriginalAmount : e.ExpConvertedAmount)),
            TotalIncomeAmount = incomes.Sum(i =>
                i.IncAccountAmount ??
                (i.IncOriginalCurrency == pmCurrency ? i.IncOriginalAmount : i.IncConvertedAmount)),
            Currency = pmCurrency
        };

        return Ok(summary);
    }

    // ── GET /api/payment-methods/{id}/balance ──────────────

    /// <summary>
    /// Returns the account balance in a specific project (in the account's currency).
    /// Only active expenses and incomes (not drafts) count in the balance.
    /// </summary>
    /// <response code="200">Account balance in the project.</response>
    /// <response code="400">project_id is required.</response>
    /// <response code="404">Account not found or not linked to the project.</response>
    [HttpGet("{id:guid}/balance")]
    [ProducesResponseType(typeof(PaymentMethodBalanceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProjectBalance(
        Guid id,
        [FromQuery] Guid? projectId,
        CancellationToken ct)
    {
        if (!projectId.HasValue)
            return BadRequest(LocalizedResponse.Create("VALIDATION_ERROR", _localizer["ProjectIdRequired"]));

        var userId = User.GetRequiredUserId();
        var pm = await _paymentMethodService.GetByIdAsync(id, ct);

        if (pm is null || pm.PmtOwnerUserId != userId)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["PaymentMethodNotFound"]));

        var isLinked = await _projectPaymentMethodService.IsLinkedAsync(projectId.Value, id, ct);
        if (!isLinked)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["PaymentMethodNotLinkedToProject"]));

        var balance = await _paymentMethodService.GetProjectBalanceAsync(id, projectId.Value, ct);
        return Ok(balance);
    }
}
