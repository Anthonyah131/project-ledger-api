namespace ProjectLedger.API.Common.Settings;

/// <summary>
/// MCP channel configuration for service-to-service authentication.
/// The token is resolved from the MCP_SERVICE_TOKEN environment variable.
/// </summary>
public class McpSettings
{
    public const string SectionName = "Mcp";

    /// <summary>Auth token required for services calling MCP endpoints.</summary>
    public string ServiceToken { get; set; } = string.Empty;
}