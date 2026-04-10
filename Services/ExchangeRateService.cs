using System.Net.Http.Json;
using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ProjectLedger.API.DTOs.Currency;

namespace ProjectLedger.API.Services;

/// <summary>
/// Exchange rate service using ExchangeRate-API v6 (https://www.exchangerate-api.com/).
/// Caches results in memory for 1 hour to reduce external API calls.
/// </summary>
public class ExchangeRateService : IExchangeRateService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ExchangeRateService> _logger;
    private readonly ExchangeRateSettings _settings;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    public ExchangeRateService(
        HttpClient httpClient,
        IMemoryCache cache,
        IOptions<ExchangeRateSettings> settings,
        ILogger<ExchangeRateService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ExchangeRateResponse> GetExchangeRateAsync(
        string from, string to, decimal? amount = null, CancellationToken ct = default)
    {
        from = from.ToUpperInvariant();
        to = to.ToUpperInvariant();

        if (from == to)
        {
            return new ExchangeRateResponse
            {
                BaseCurrency = from,
                TargetCurrency = to,
                Rate = 1m,
                Amount = amount,
                ConvertedAmount = amount,
                Date = DateOnly.FromDateTime(DateTime.UtcNow)
            };
        }

        var latestRates = await GetLatestRatesAsync(from, ct);

        if (!latestRates.Rates.TryGetValue(to, out var rate))
            throw new InvalidOperationException("ExchangeRateNotAvailable");

        // Applies amount conversion when requested.
        if (amount.HasValue)
        {
            return new ExchangeRateResponse
            {
                BaseCurrency = from,
                TargetCurrency = to,
                Rate = rate,
                Amount = amount,
                ConvertedAmount = Math.Round(amount.Value * rate, 2),
                Date = latestRates.Date
            };
        }

        return new ExchangeRateResponse
        {
            BaseCurrency = from,
            TargetCurrency = to,
            Rate = rate,
            Amount = null,
            ConvertedAmount = null,
            Date = latestRates.Date
        };
    }

    /// <inheritdoc />
    public async Task<ExchangeRateLatestResponse> GetLatestRatesAsync(
        string baseCurrency, CancellationToken ct = default)
    {
        EnsureExchangeRateApiConfigured();

        baseCurrency = baseCurrency.ToUpperInvariant();

        var cacheKey = $"exchange_rates_latest_{baseCurrency}";

        if (_cache.TryGetValue(cacheKey, out ExchangeRateLatestResponse? cached))
            return cached!;

        var url = $"v6/{Uri.EscapeDataString(_settings.ApiKey)}/latest/{Uri.EscapeDataString(baseCurrency)}";
        var providerResponse = await FetchFromApiAsync<ExchangeRateApiLatestResponse>(url, ct);

        if (providerResponse is null)
            throw new InvalidOperationException("ExchangeRateNotAvailable");

        if (!string.Equals(providerResponse.Result, "success", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("ExchangeRateNotAvailable");

        if (providerResponse.ConversionRates is null || providerResponse.ConversionRates.Count == 0)
            throw new InvalidOperationException("ExchangeRateNotAvailable");

        var result = new ExchangeRateLatestResponse
        {
            BaseCurrency = baseCurrency,
            Date = ParseProviderDate(providerResponse.TimeLastUpdateUtc),
            Rates = providerResponse.ConversionRates
        };

        _cache.Set(cacheKey, result, CacheDuration);
        return result;
    }

    /// <summary>
    /// Executes an HTTP GET request to the ExchangeRate-API and deserializes the response.
    /// Handles HTTP request errors gracefully by logging them and throwing a generic service exception.
    /// </summary>
    /// <typeparam name="T">The expected response type to deserialize into.</typeparam>
    /// <param name="relativeUrl">The relative URL for the API endpoint.</param>
    /// <param name="ct">Cancellation token.</param>
    private async Task<T?> FetchFromApiAsync<T>(string relativeUrl, CancellationToken ct)
    {
        try
        {
            var response = await _httpClient.GetAsync(relativeUrl, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch exchange rate from ExchangeRate-API: {Url}", relativeUrl);
            throw new InvalidOperationException("ExchangeRateServiceUnavailable");
        }
    }

    /// <summary>Validates that the service is enabled and credentials are present.</summary>
    private void EnsureExchangeRateApiConfigured()
    {
        if (!_settings.Enabled)
            throw new InvalidOperationException("ExchangeRateServiceDisabled");

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            throw new InvalidOperationException("ExchangeRateServiceMissingApiKey");
    }

    /// <summary>Parses the date string from the provider into a system DateOnly.</summary>
    private static DateOnly ParseProviderDate(string? utcDate)
    {
        if (!string.IsNullOrWhiteSpace(utcDate)
            && DateTime.TryParse(
                utcDate,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return DateOnly.FromDateTime(parsed);
        }

        return DateOnly.FromDateTime(DateTime.UtcNow);
    }

    // ── ExchangeRate-API v6 response model (internal) ───────

    private sealed class ExchangeRateApiLatestResponse
    {
        [JsonPropertyName("result")]
        public string? Result { get; set; }

        [JsonPropertyName("base_code")]
        public string? BaseCode { get; set; }

        [JsonPropertyName("time_last_update_utc")]
        public string? TimeLastUpdateUtc { get; set; }

        [JsonPropertyName("error-type")]
        public string? ErrorType { get; set; }

        [JsonPropertyName("conversion_rates")]
        public Dictionary<string, decimal> ConversionRates { get; set; } = new();
    }

}
