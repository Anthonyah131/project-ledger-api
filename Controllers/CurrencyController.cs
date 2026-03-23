using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ProjectLedger.API.DTOs.Currency;
using ProjectLedger.API.Extensions.Mappings;
using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.Resources;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Catálogo de monedas ISO 4217. Solo lectura, público (no requiere auth).
/// </summary>
[ApiController]
[Route("api/currencies")]
[Tags("Currencies")]
[Produces("application/json")]
[AllowAnonymous]
public class CurrencyController : ControllerBase
{
    private readonly ICurrencyService _currencyService;
    private readonly IStringLocalizer<Messages> _localizer;

    public CurrencyController(ICurrencyService currencyService,
    IStringLocalizer<Messages> localizer)
    {
        _currencyService = currencyService;
        _localizer = localizer;
    }

    // ── GET /api/currencies ─────────────────────────────────

    /// <summary>
    /// Lista todas las monedas activas del catálogo.
    /// </summary>
    /// <response code="200">Lista de monedas activas.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CurrencyResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var currencies = await _currencyService.GetAllActiveAsync(ct);
        return Ok(currencies.ToResponse());
    }

    // ── GET /api/currencies/{code} ──────────────────────────

    /// <summary>
    /// Obtiene una moneda por su código ISO 4217.
    /// </summary>
    /// <param name="code">Código ISO 4217 (e.g. USD, EUR, DOP).</param>
    /// <response code="200">Moneda encontrada.</response>
    /// <response code="404">Moneda no encontrada.</response>
    [HttpGet("{code}")]
    [ProducesResponseType(typeof(CurrencyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByCode(string code, CancellationToken ct)
    {
        var currency = await _currencyService.GetByCodeAsync(code, ct);
        if (currency is null)
            return NotFound(LocalizedResponse.Create("NOT_FOUND", _localizer["CurrencyNotFound"]));

        return Ok(currency.ToResponse());
    }
}
