namespace ProjectLedger.API.Common.Settings;

/// <summary>
/// CORS configuration read from appsettings.json → "Cors" section.
/// </summary>
public class CorsSettings
{
    public const string SectionName = "Cors";
    public const string PolicyName = "ProjectLedgerCorsPolicy";

    public string[] AllowedOrigins { get; set; } = [];
}
