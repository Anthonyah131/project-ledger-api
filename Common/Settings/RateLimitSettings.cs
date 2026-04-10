namespace ProjectLedger.API.Common.Settings;

/// <summary>
/// Rate Limiting configuration read from appsettings.json → "RateLimit" section.
/// </summary>
public class RateLimitSettings
{
    public const string SectionName = "RateLimit";
    public const string PolicyName = "GlobalRateLimit";

    /// <summary>Maximum number of requests permitted within the window.</summary>
    public int PermitLimit { get; set; } = 100;

    /// <summary>The time window in seconds for the rate limit.</summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>Maximum number of requests that can be queued once the permit limit is reached.</summary>
    public int QueueLimit { get; set; }
}
