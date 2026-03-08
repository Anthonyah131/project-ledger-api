using ProjectLedger.API.DTOs.Currency;

namespace ProjectLedger.API.Services;

public interface IExchangeRateService
{
    /// <summary>
    /// Gets the exchange rate between two currencies, with optional amount conversion.
    /// </summary>
    Task<ExchangeRateResponse> GetExchangeRateAsync(string from, string to, decimal? amount = null, CancellationToken ct = default);

    /// <summary>
    /// Gets latest exchange rates for a base currency against all available currencies.
    /// </summary>
    Task<ExchangeRateLatestResponse> GetLatestRatesAsync(string baseCurrency, CancellationToken ct = default);
}
