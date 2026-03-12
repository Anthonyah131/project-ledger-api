namespace ProjectLedger.API.Common.Settings;

/// <summary>
/// Configuración del canal MCP para autenticación service-to-service.
/// El token se resuelve desde la variable de entorno MCP_SERVICE_TOKEN.
/// </summary>
public class McpSettings
{
    public const string SectionName = "Mcp";

    public string ServiceToken { get; set; } = string.Empty;
}