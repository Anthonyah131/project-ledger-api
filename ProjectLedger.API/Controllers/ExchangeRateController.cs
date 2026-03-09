using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Currency;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Consulta de tasas de cambio via ExchangeRate-API.
/// Requiere autenticación pero no pertenece a un proyecto específico.
/// </summary>
[ApiController]
[Route("api/exchange-rates")]
[Authorize]
[Tags("Exchange Rates")]
[Produces("application/json")]
public class ExchangeRateController : ControllerBase
{
    private readonly IExchangeRateService _exchangeRateService;

    public ExchangeRateController(IExchangeRateService exchangeRateService)
    {
        _exchangeRateService = exchangeRateService;
    }

    // ── GET /api/exchange-rates?from=CRC&to=USD&amount=100000 ──

    /// <summary>
    /// Obtiene el tipo de cambio entre dos monedas, con conversión opcional.
    /// </summary>
    /// <param name="from">Código ISO 4217 de la moneda origen (e.g. CRC).</param>
    /// <param name="to">Código ISO 4217 de la moneda destino (e.g. USD).</param>
    /// <param name="amount">Monto opcional a convertir.</param>
    /// <param name="ct">Token de cancelación.</param>
    [HttpGet]
    [ProducesResponseType(typeof(ExchangeRateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetRate(
        [FromQuery] string from,
        [FromQuery] string to,
        [FromQuery] decimal? amount,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
            return BadRequest(new { message = "Both 'from' and 'to' query parameters are required." });

        var result = await _exchangeRateService.GetExchangeRateAsync(from, to, amount, ct);
        return Ok(result);
    }

    // ── GET /api/exchange-rates/latest?base=CRC ──

    /// <summary>
    /// Obtiene las tasas de cambio más recientes para una moneda base.
    /// </summary>
    /// <param name="baseCurrency">Código ISO 4217 de la moneda base (default: USD).</param>
    /// <param name="ct">Token de cancelación.</param>
    [HttpGet("latest")]
    [ProducesResponseType(typeof(ExchangeRateLatestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetLatest(
        [FromQuery(Name = "base")] string baseCurrency = "USD",
        CancellationToken ct = default)
    {
        var result = await _exchangeRateService.GetLatestRatesAsync(baseCurrency, ct);
        return Ok(result);
    }
}
