namespace ProjectLedger.API.Common.Settings;

/// <summary>
/// CORS configuration read from appsettings.json → "Cors" section.
/// </summary>
public class CorsSettings
{
    /// <summary>Name of the configuration section in appsettings.json.</summary>
    public const string SectionName = "Cors";

    /// <summary>Standard policy name used throughout the application.</summary>
    public const string PolicyName = "ProjectLedgerCorsPolicy";

    /// <summary>List of origins allowed to make cross-site requests.</summary>
    public string[] AllowedOrigins { get; set; } = [];
}
