using Microsoft.Extensions.Localization;
using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Currency;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Exchange rate queries via ExchangeRate-API.
/// Requires authentication but does not belong to a specific project.
/// </summary>
[ApiController]
[Route("api/exchange-rates")]
[Authorize]
[Tags("Exchange Rates")]
[Produces("application/json")]
public class ExchangeRateController : ControllerBase
{
    private readonly IExchangeRateService _exchangeRateService;
    private readonly IStringLocalizer<Messages> _localizer;

    public ExchangeRateController(IExchangeRateService exchangeRateService,
    IStringLocalizer<Messages> localizer)
    {
        _exchangeRateService = exchangeRateService;
        _localizer = localizer;
    }

    // ── GET /api/exchange-rates?from=CRC&to=USD&amount=100000 ──

    /// <summary>
    /// Gets the exchange rate between two currencies, with optional conversion.
    /// </summary>
    /// <param name="from">ISO 4217 code of the source currency (e.g., CRC).</param>
    /// <param name="to">ISO 4217 code of the target currency (e.g., USD).</param>
    /// <param name="amount">Optional amount to convert.</param>
    /// <param name="ct">Cancellation token.</param>
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
            return BadRequest(LocalizedResponse.Create("VALIDATION_ERROR", _localizer["ExchangeRateParamsRequired"]));

        var result = await _exchangeRateService.GetExchangeRateAsync(from, to, amount, ct);
        return Ok(result);
    }

    // ── GET /api/exchange-rates/latest?base=CRC ──

    /// <summary>
    /// Gets the latest exchange rates for a base currency.
    /// </summary>
    /// <param name="baseCurrency">ISO 4217 code of the base currency (default: USD).</param>
    /// <param name="ct">Cancellation token.</param>
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
