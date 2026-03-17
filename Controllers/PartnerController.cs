using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Partner;
using ProjectLedger.API.Extensions.Mappings;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Controlador de partners (contactos financieros) del usuario autenticado.
///
/// Ruta: /api/partners
/// - OwnerUserId se obtiene SIEMPRE del JWT, nunca del body.
/// - Solo el dueño puede ver/editar/eliminar sus partners.
/// </summary>
[ApiController]
[Route("api/partners")]
[Authorize]
[Tags("Partners")]
[Produces("application/json")]
public class PartnerController : ControllerBase
{
    private readonly IPartnerService _partnerService;

    public PartnerController(IPartnerService partnerService)
    {
        _partnerService = partnerService;
    }

    // ── GET /api/partners ───────────────────────────────────

    /// <summary>
    /// Lista los partners del usuario autenticado. Soporta búsqueda y paginación.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyPartners(
        [FromQuery] string? search,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var userId = User.GetRequiredUserId();
        var (items, totalCount) = await _partnerService.SearchAsync(userId, search, skip, take, ct);

        return Ok(new
        {
            items = items.ToResponse(),
            totalCount
        });
    }

    // ── GET /api/partners/{id} ──────────────────────────────

    /// <summary>
    /// Obtiene un partner por ID con sus métodos de pago asociados.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PartnerDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var partner = await _partnerService.GetByIdWithPaymentMethodsAsync(id, ct);

        if (partner is null || partner.PtrOwnerUserId != userId)
            return NotFound(new { message = "Partner not found." });

        return Ok(partner.ToDetailResponse());
    }

    // ── POST /api/partners ──────────────────────────────────

    /// <summary>
    /// Crea un partner para el usuario autenticado.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PartnerResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePartnerRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        var partner = request.ToEntity(userId);
        await _partnerService.CreateAsync(partner, ct);

        return CreatedAtAction(
            nameof(GetById),
            new { id = partner.PtrId },
            partner.ToResponse());
    }

    // ── PATCH /api/partners/{id} ────────────────────────────

    /// <summary>
    /// Actualiza un partner. No permite cambiar owner_user_id.
    /// </summary>
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(PartnerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdatePartnerRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.GetRequiredUserId();
        var partner = await _partnerService.GetByIdAsync(id, ct);

        if (partner is null || partner.PtrOwnerUserId != userId)
            return NotFound(new { message = "Partner not found." });

        partner.ApplyUpdate(request);
        await _partnerService.UpdateAsync(partner, ct);

        return Ok(partner.ToResponse());
    }

    // ── DELETE /api/partners/{id} ───────────────────────────

    /// <summary>
    /// Soft-delete de un partner. No se puede eliminar si tiene payment methods
    /// vinculados a proyectos activos.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var partner = await _partnerService.GetByIdAsync(id, ct);

        if (partner is null || partner.PtrOwnerUserId != userId)
            return NotFound(new { message = "Partner not found." });

        try
        {
            await _partnerService.SoftDeleteAsync(id, userId, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // ── GET /api/partners/{id}/payment-methods ──────────────

    /// <summary>
    /// Lista los métodos de pago del partner.
    /// </summary>
    [HttpGet("{id:guid}/payment-methods")]
    [ProducesResponseType(typeof(IEnumerable<PartnerPaymentMethodResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPaymentMethods(Guid id, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        var partner = await _partnerService.GetByIdAsync(id, ct);

        if (partner is null || partner.PtrOwnerUserId != userId)
            return NotFound(new { message = "Partner not found." });

        var paymentMethods = await _partnerService.GetPaymentMethodsAsync(id, ct);

        var response = paymentMethods.Select(pm => new PartnerPaymentMethodResponse
        {
            Id = pm.PmtId,
            Name = pm.PmtName,
            Type = pm.PmtType,
            Currency = pm.PmtCurrency,
            BankName = pm.PmtBankName
        });

        return Ok(response);
    }
}
