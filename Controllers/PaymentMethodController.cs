using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.DTOs.Expense;
using ProjectLedger.API.DTOs.Income;
using ProjectLedger.API.DTOs.PaymentMethod;
using ProjectLedger.API.Extensions.Mappings;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Controlador de métodos de pago del usuario autenticado.
/// 
/// Ruta: /api/payment-methods
/// - OwnerUserId se obtiene SIEMPRE del JWT, nunca del body.
/// - Plan valida MaxPaymentMethods al crear.
/// - Solo el dueño puede ver/editar/eliminar sus payment methods.
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

    public PaymentMethodController(
        IPaymentMethodService paymentMethodService,
        IExpenseService expenseService,
        IIncomeService incomeService,
        IProjectPaymentMethodService projectPaymentMethodService)
    {
        _paymentMethodService = paymentMethodService;
        _expenseService = expenseService;
        _incomeService = incomeService;
        _projectPaymentMethodService = projectPaymentMethodService;
    }

    // ── GET /api/payment-methods ────────────────────────────

    /// <summary>
    /// Lista todos los métodos de pago del usuario autenticado.
    /// </summary>
    /// <response code="200">Lista de métodos de pago.</response>
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
    /// Obtiene un método de pago por ID. Solo el dueño puede verlo.
    /// </summary>
    /// <response code="200">Método de pago encontrado.</response>
    /// <response code="404">No encontrado o no pertenece al usuario.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PaymentMethodResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var pm = await _paymentMethodService.GetByIdAsync(id, ct);

        if (pm is null || pm.PmtOwnerUserId != userId)
            return NotFound(new { message = "Payment method not found." });

        return Ok(pm.ToResponse());
    }

    // ── POST /api/payment-methods ───────────────────────────

    /// <summary>
    /// Crea un método de pago para el usuario autenticado.
    /// Valida límite MaxPaymentMethods del plan.
    /// OwnerUserId viene del JWT — NUNCA del body.
    /// </summary>
    /// <response code="201">Método de pago creado.</response>
    /// <response code="403">Límite del plan excedido.</response>
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
    /// Actualiza un método de pago. Solo el dueño puede editarlo.
    /// </summary>
    /// <response code="200">Método de pago actualizado.</response>
    /// <response code="404">No encontrado o no pertenece al usuario.</response>
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
            return NotFound(new { message = "Payment method not found." });

        pm.ApplyUpdate(request);
        await _paymentMethodService.UpdateAsync(pm, ct);

        return Ok(pm.ToResponse());
    }

    // ── POST /api/payment-methods/{id}/partner ──────────────

    /// <summary>
    /// Enlaza un partner a un método de pago. Solo el dueño puede hacerlo.
    /// </summary>
    /// <response code="200">Partner enlazado.</response>
    /// <response code="404">Método de pago o partner no encontrado.</response>
    /// <response code="409">El método de pago ya tiene este partner enlazado.</response>
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
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // ── DELETE /api/payment-methods/{id}/partner ─────────────

    /// <summary>
    /// Desenlaza el partner de un método de pago. Solo el dueño puede hacerlo.
    /// </summary>
    /// <response code="200">Partner desenlazado.</response>
    /// <response code="404">Método de pago no encontrado.</response>
    /// <response code="409">El método de pago no tiene un partner enlazado.</response>
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
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // ── DELETE /api/payment-methods/{id} ────────────────────

    /// <summary>
    /// Soft-delete de un método de pago. Solo el dueño puede eliminarlo.
    /// </summary>
    /// <response code="400">El método de pago no se puede eliminar porque tiene movimientos activos relacionados.</response>
    /// <response code="204">Método de pago eliminado.</response>
    /// <response code="404">No encontrado o no pertenece al usuario.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var pm = await _paymentMethodService.GetByIdAsync(id, ct);

        if (pm is null || pm.PmtOwnerUserId != userId)
            return NotFound(new { message = "Payment method not found." });

        await _paymentMethodService.SoftDeleteAsync(id, userId, ct);
        return NoContent();
    }

    // ── GET /api/payment-methods/{id}/expenses ────────────

    /// <summary>
    /// Obtiene todos los movimientos (gastos) de un método de pago (paginado),
    /// cruzando todos los proyectos del usuario.
    /// </summary>
    /// <response code="200">Lista paginada de gastos asociados al método de pago.</response>
    /// <response code="404">Método de pago no encontrado.</response>
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
            return BadRequest(new { message = "Invalid date range: 'from' cannot be greater than 'to'." });

        var userId = User.GetRequiredUserId();
        var pm = await _paymentMethodService.GetByIdAsync(id, ct);

        if (pm is null || pm.PmtOwnerUserId != userId)
            return NotFound(new { message = "Payment method not found." });

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
    /// Obtiene todos los movimientos (ingresos) de un método de pago (paginado),
    /// cruzando todos los proyectos del usuario.
    /// </summary>
    /// <response code="200">Lista paginada de ingresos asociados al método de pago.</response>
    /// <response code="404">Método de pago no encontrado.</response>
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
            return BadRequest(new { message = "Invalid date range: 'from' cannot be greater than 'to'." });

        var userId = User.GetRequiredUserId();
        var pm = await _paymentMethodService.GetByIdAsync(id, ct);

        if (pm is null || pm.PmtOwnerUserId != userId)
            return NotFound(new { message = "Payment method not found." });

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
    /// Obtiene los proyectos vinculados al método de pago.
    /// </summary>
    /// <response code="200">Lista de proyectos relacionados al método de pago.</response>
    /// <response code="404">Método de pago no encontrado.</response>
    [HttpGet("{id:guid}/projects")]
    [ProducesResponseType(typeof(PaymentMethodProjectsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProjectsByPaymentMethod(Guid id, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var pm = await _paymentMethodService.GetByIdAsync(id, ct);

        if (pm is null || pm.PmtOwnerUserId != userId)
            return NotFound(new { message = "Payment method not found." });

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
    /// Retorna métricas agregadas de uso para el método de pago.
    /// </summary>
    /// <response code="200">Resumen de gastos, ingresos y proyectos relacionados.</response>
    /// <response code="404">Método de pago no encontrado.</response>
    [HttpGet("{id:guid}/summary")]
    [ProducesResponseType(typeof(PaymentMethodSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPaymentMethodSummary(Guid id, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var pm = await _paymentMethodService.GetByIdAsync(id, ct);

        if (pm is null || pm.PmtOwnerUserId != userId)
            return NotFound(new { message = "Payment method not found." });

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
    /// Devuelve el balance de la cuenta en un proyecto específico (en moneda de la cuenta).
    /// Solo gastos e ingresos activos (no recordatorios) cuentan en el balance.
    /// </summary>
    /// <response code="200">Balance de la cuenta en el proyecto.</response>
    /// <response code="400">project_id es requerido.</response>
    /// <response code="404">Cuenta no encontrada o no vinculada al proyecto.</response>
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
            return BadRequest(new { error = new { code = "PROJECT_ID_REQUIRED", message = "project_id query parameter is required." } });

        var userId = User.GetRequiredUserId();
        var pm = await _paymentMethodService.GetByIdAsync(id, ct);

        if (pm is null || pm.PmtOwnerUserId != userId)
            return NotFound(new { message = "Payment method not found." });

        var isLinked = await _projectPaymentMethodService.IsLinkedAsync(projectId.Value, id, ct);
        if (!isLinked)
            return NotFound(new { message = "Payment method is not linked to the specified project." });

        var balance = await _paymentMethodService.GetProjectBalanceAsync(id, projectId.Value, ct);
        return Ok(balance);
    }
}
