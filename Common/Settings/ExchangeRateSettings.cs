namespace ProjectLedger.API.Common.Settings;

/// <summary>
/// Configuration for the external exchange rate provider (ExchangeRate-API).
/// </summary>
public class ExchangeRateSettings
{
    public const string SectionName = "ExchangeRateApi";

    /// <summary>Whether the external exchange rate integration is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>API key provided by exchangerate-api.com.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Base URL for the API endpoints.</summary>
    public string BaseUrl { get; set; } = "https://v6.exchangerate-api.com/";

    /// <summary>Timeout in seconds for API calls.</summary>
    public int TimeoutSeconds { get; set; } = 10;
}
