namespace ProjectLedger.API.Common.Settings;

/// <summary>
/// Rate Limiting configuration read from appsettings.json → "RateLimit" section.
/// </summary>
public class RateLimitSettings
{
    public const string SectionName = "RateLimit";
    public const string PolicyName = "GlobalRateLimit";

    public int PermitLimit { get; set; } = 100;
    public int WindowSeconds { get; set; } = 60;
    public int QueueLimit { get; set; }
}
