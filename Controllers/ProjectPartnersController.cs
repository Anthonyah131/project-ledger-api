using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.DTOs.Partner;
using ProjectLedger.API.Extensions;
using ProjectLedger.API.Extensions.Mappings;
using ProjectLedger.API.Models;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Balances, historial y liquidaciones de socios por proyecto.
/// Solo accesible cuando <c>prj_partners_enabled = true</c>.
/// Requiere rol <c>owner</c> o <c>editor</c>.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}")]
[Authorize]
[Tags("Project Partners — Balances & Settlements")]
[Produces("application/json")]
public class ProjectPartnersController : ControllerBase
{
    private readonly IProjectService _projectService;
    private readonly IProjectAccessService _accessService;
    private readonly IPartnerBalanceService _balanceService;
    private readonly IPartnerSettlementService _settlementService;

    public ProjectPartnersController(
        IProjectService projectService,
        IProjectAccessService accessService,
        IPartnerBalanceService balanceService,
        IPartnerSettlementService settlementService)
    {
        _projectService = projectService;
        _accessService = accessService;
        _balanceService = balanceService;
        _settlementService = settlementService;
    }

    // ── Helper ───────────────────────────────────────────────

    private async Task<Project> GetProjectWithPartnersEnabledAsync(Guid projectId, string role, CancellationToken ct)
    {
        var userId = User.GetRequiredUserId();
        await _accessService.ValidateAccessAsync(userId, projectId, role, ct);

        var project = await _projectService.GetByIdAsync(projectId, ct)
            ?? throw new KeyNotFoundException($"Project {projectId} not found.");

        if (!project.PrjPartnersEnabled)
            throw new InvalidOperationException("Partners module is not enabled for this project.");

        return project;
    }

    // ── GET /projects/:id/partners/balance ───────────────────

    /// <summary>
    /// Retorna los balances de todos los socios del proyecto en la moneda base del proyecto.
    ///
    /// Lógica del balance:
    ///   netBalance = (othersOweHim - heOwesOthers) + (settlementsPaid - settlementsReceived)
    ///
    /// Positivo = otros le deben. Negativo = él le debe a otros.
    ///
    /// Las liquidaciones registradas (partner_settlements) saldan deuda entre socios sin pasar por
    /// métodos de pago del proyecto. Al pagar una liquidación, la deuda se reduce en el balance neto.
    /// Solo disponible cuando <c>partners_enabled = true</c>.
    /// </summary>
    [HttpGet("partners/balance")]
    [ProducesResponseType(typeof(PartnerBalanceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetPartnersBalance(Guid projectId, CancellationToken ct)
    {
        var project = await GetProjectWithPartnersEnabledAsync(projectId, "viewer", ct);
        var balance = await _balanceService.GetBalancesAsync(projectId, project.PrjCurrencyCode, ct);
        return Ok(balance);
    }

    // ── GET /projects/:id/partners/settlement-suggestions ────

    /// <summary>
    /// Devuelve las transferencias mínimas necesarias para que todos los balances queden en cero.
    ///
    /// Algoritmo greedy: empareja al mayor acreedor con el mayor deudor y repite.
    /// Minimiza el número de transferencias.
    /// Solo disponible cuando <c>partners_enabled = true</c>.
    /// </summary>
    [HttpGet("partners/settlement-suggestions")]
    [ProducesResponseType(typeof(SettlementSuggestionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetSettlementSuggestions(Guid projectId, CancellationToken ct)
    {
        var project = await GetProjectWithPartnersEnabledAsync(projectId, "viewer", ct);
        var suggestions = await _balanceService.GetSettlementSuggestionsAsync(projectId, project.PrjCurrencyCode, ct);
        return Ok(suggestions);
    }

    // ── GET /projects/:id/partners/:partnerId/history ────────

    /// <summary>
    /// Lista todas las transacciones con splits de un partner y sus liquidaciones en el proyecto.
    /// Las transacciones están paginadas. Las liquidaciones se devuelven completas.
    /// </summary>
    [HttpGet("partners/{partnerId:guid}/history")]
    [ProducesResponseType(typeof(PartnerHistoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetPartnerHistory(
        Guid projectId,
        Guid partnerId,
        [FromQuery] PagedRequest pagination,
        CancellationToken ct)
    {
        await GetProjectWithPartnersEnabledAsync(projectId, "viewer", ct);
        var history = await _balanceService.GetPartnerHistoryAsync(projectId, partnerId, pagination, ct);
        return Ok(history);
    }

    // ── POST /projects/:id/partner-settlements ───────────────

    /// <summary>
    /// Registra una liquidación directa entre dos partners del proyecto.
    ///
    /// Cuando el partner A paga al partner B:
    ///   - Se crea un registro con from_partner_id=A, to_partner_id=B, amount=X
    ///   - El balance de A mejora: su settlementsPaid sube, netBalance se acerca a 0
    ///   - El balance de B se reduce: su settlementsReceived sube, netBalance se acerca a 0
    ///
    /// Las liquidaciones no afectan los métodos de pago del proyecto.
    /// </summary>
    [HttpPost("partner-settlements")]
    [ProducesResponseType(typeof(SettlementResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateSettlement(
        Guid projectId,
        [FromBody] CreateSettlementRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (request.Amount <= 0)
            return BadRequest(new { message = "Amount must be greater than zero." });

        if (request.ExchangeRate <= 0)
            return BadRequest(new { message = "ExchangeRate must be greater than zero." });

        if (request.FromPartnerId == request.ToPartnerId)
            return BadRequest(new { message = "From and To partners must be different." });

        await GetProjectWithPartnersEnabledAsync(projectId, "editor", ct);

        var userId = User.GetRequiredUserId();
        var entity = request.ToEntity(projectId, userId);
        var created = await _settlementService.CreateAsync(entity, request.CurrencyExchanges, ct);

        return CreatedAtAction(
            nameof(GetSettlements),
            new { projectId },
            created.ToResponse());
    }

    // ── PATCH /projects/:id/partner-settlements/:id ──────────

    /// <summary>
    /// Actualiza los campos editables de una liquidación existente.
    /// Solo se aplican los campos que vienen en el body (PATCH semántico).
    /// Al cambiar amount o exchangeRate, convertedAmount se recalcula automáticamente.
    /// </summary>
    [HttpPatch("partner-settlements/{settlementId:guid}")]
    [ProducesResponseType(typeof(SettlementResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateSettlement(
        Guid projectId,
        Guid settlementId,
        [FromBody] UpdateSettlementRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (request.Amount.HasValue && request.Amount.Value <= 0)
            return BadRequest(new { message = "Amount must be greater than zero." });

        if (request.ExchangeRate.HasValue && request.ExchangeRate.Value <= 0)
            return BadRequest(new { message = "ExchangeRate must be greater than zero." });

        await GetProjectWithPartnersEnabledAsync(projectId, "editor", ct);

        var updated = await _settlementService.UpdateAsync(settlementId, projectId, request, ct);
        return Ok(updated.ToResponse());
    }

    // ── GET /projects/:id/partner-settlements ────────────────

    /// <summary>
    /// Lista las liquidaciones activas del proyecto (paginado).
    /// </summary>
    [HttpGet("partner-settlements")]
    [ProducesResponseType(typeof(PagedResponse<SettlementResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetSettlements(
        Guid projectId,
        [FromQuery] PagedRequest pagination,
        CancellationToken ct)
    {
        await GetProjectWithPartnersEnabledAsync(projectId, "viewer", ct);

        var (items, total) = await _settlementService.GetPagedByProjectIdAsync(
            projectId, pagination.Skip, pagination.PageSize, ct);

        var response = PagedResponse<SettlementResponse>.Create(
            items.Select(s => s.ToResponse()).ToList(),
            total,
            pagination);

        return Ok(response);
    }

    // ── DELETE /projects/:id/partner-settlements/:id ─────────

    /// <summary>
    /// Soft-delete de una liquidación. Revierte su efecto en el balance.
    /// </summary>
    [HttpDelete("partner-settlements/{settlementId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DeleteSettlement(Guid projectId, Guid settlementId, CancellationToken ct)
    {
        await GetProjectWithPartnersEnabledAsync(projectId, "editor", ct);
        var userId = User.GetRequiredUserId();
        await _settlementService.SoftDeleteAsync(settlementId, projectId, userId, ct);
        return NoContent();
    }
}
