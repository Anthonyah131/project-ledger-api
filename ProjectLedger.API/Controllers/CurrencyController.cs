using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Currency;
using ProjectLedger.API.Extensions.Mappings;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Catálogo de monedas ISO 4217. Solo lectura, público (no requiere auth).
/// </summary>
[ApiController]
[Route("api/currencies")]
[Tags("Currencies")]
[Produces("application/json")]
public class CurrencyController : ControllerBase
{
    private readonly ICurrencyService _currencyService;

    public CurrencyController(ICurrencyService currencyService)
    {
        _currencyService = currencyService;
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
            return NotFound(new { message = $"Currency '{code.ToUpperInvariant()}' not found." });

        return Ok(currency.ToResponse());
    }
}
