using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Currency;
using ProjectLedger.API.Extensions.Mappings;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Gestión de monedas alternativas por proyecto.
/// Permite agregar/quitar monedas para visualización multi-divisa.
/// 
/// Ruta anidada: /api/projects/{projectId}/alternative-currencies
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/alternative-currencies")]
[Authorize]
[Tags("Alternative Currencies")]
[Produces("application/json")]
public class ProjectAlternativeCurrencyController : ControllerBase
{
    private readonly IProjectAlternativeCurrencyService _altCurrencyService;
    private readonly IProjectAccessService _accessService;

    public ProjectAlternativeCurrencyController(
        IProjectAlternativeCurrencyService altCurrencyService,
        IProjectAccessService accessService)
    {
        _altCurrencyService = altCurrencyService;
        _accessService = accessService;
    }

    // ── GET /api/projects/{projectId}/alternative-currencies ──

    /// <summary>
    /// Lista las monedas alternativas configuradas para el proyecto.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "ProjectViewer")]
    [ProducesResponseType(typeof(IEnumerable<ProjectAlternativeCurrencyResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetByProject(Guid projectId, CancellationToken ct)
    {
        var items = await _altCurrencyService.GetByProjectIdAsync(projectId, ct);
        return Ok(items.Select(x => x.ToResponse()));
    }

    // ── POST /api/projects/{projectId}/alternative-currencies ──

    /// <summary>
    /// Agrega una moneda alternativa al proyecto.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "ProjectEditor")]
    [ProducesResponseType(typeof(ProjectAlternativeCurrencyResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Add(
        Guid projectId,
        [FromBody] AddAlternativeCurrencyRequest request,
        CancellationToken ct)
    {
        var entity = await _altCurrencyService.AddAsync(projectId, request.CurrencyCode, ct);
        var response = entity.ToResponse();
        return CreatedAtAction(nameof(GetByProject), new { projectId }, response);
    }

    // ── DELETE /api/projects/{projectId}/alternative-currencies/{code} ──

    /// <summary>
    /// Elimina una moneda alternativa del proyecto.
    /// </summary>
    /// <param name="projectId">ID del proyecto.</param>
    /// <param name="code">Código ISO 4217 de la moneda a eliminar (e.g. USD, EUR).</param>
    /// <param name="ct">Token de cancelación.</param>
    [HttpDelete("{code}")]
    [Authorize(Policy = "ProjectEditor")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(Guid projectId, string code, CancellationToken ct)
    {
        await _altCurrencyService.RemoveAsync(projectId, code, ct);
        return NoContent();
    }
}
