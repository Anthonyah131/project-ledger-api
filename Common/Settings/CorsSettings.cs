namespace ProjectLedger.API.Common.Settings;

/// <summary>
/// Configuración CORS leída desde appsettings.json → sección "Cors".
/// </summary>
public class CorsSettings
{
    public const string SectionName = "Cors";
    public const string PolicyName = "ProjectLedgerCorsPolicy";

    public string[] AllowedOrigins { get; set; } = [];
}
