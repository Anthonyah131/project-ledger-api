namespace ProjectLedger.API.Common.Settings;

public class ExchangeRateSettings
{
    public const string SectionName = "ExchangeRateApi";

    public bool Enabled { get; set; } = true;
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://v6.exchangerate-api.com/";
    public int TimeoutSeconds { get; set; } = 10;
}
