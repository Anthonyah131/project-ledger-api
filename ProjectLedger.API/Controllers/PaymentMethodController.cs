using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    public PaymentMethodController(IPaymentMethodService paymentMethodService)
    {
        _paymentMethodService = paymentMethodService;
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

    // ── DELETE /api/payment-methods/{id} ────────────────────

    /// <summary>
    /// Soft-delete de un método de pago. Solo el dueño puede eliminarlo.
    /// </summary>
    /// <response code="204">Método de pago eliminado.</response>
    /// <response code="404">No encontrado o no pertenece al usuario.</response>
    [HttpDelete("{id:guid}")]
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
}
