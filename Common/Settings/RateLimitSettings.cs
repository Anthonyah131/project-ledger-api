namespace ProjectLedger.API.Common.Settings;

/// <summary>
/// Configuración de Rate Limiting leída desde appsettings.json → sección "RateLimit".
/// </summary>
public class RateLimitSettings
{
    public const string SectionName = "RateLimit";
    public const string PolicyName = "GlobalRateLimit";

    public int PermitLimit { get; set; } = 100;
    public int WindowSeconds { get; set; } = 60;
    public int QueueLimit { get; set; }
}
