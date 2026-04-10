namespace ProjectLedger.API.Common.Settings;

/// <summary>
/// Global configuration for the AI chatbot with provider rotation.
/// API keys are resolved from environment variables.
/// </summary>
public class ChatbotSettings
{
    public const string SectionName = "Chatbot";

    /// <summary>
    /// System message sent as the first message in each request.
    /// Defines the assistant's personality, language, and context.
    /// If empty, no system prompt is included.
    /// </summary>
    public string SystemPrompt { get; set; } = string.Empty;

    public ChatbotProviderSettings OpenRouter { get; set; } = new();
    public ChatbotProviderSettings Groq       { get; set; } = new();
    public ChatbotProviderSettings Cerebras   { get; set; } = new();
    public ChatbotProviderSettings BytePlus   { get; set; } = new();
}

/// <summary>
/// Configuración de un proveedor individual de IA.
/// </summary>
public class ChatbotProviderSettings
{
    /// <summary>Habilita o deshabilita el proveedor sin eliminar su configuración.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>API key del proveedor (placeholder ${ENV_VAR} resuelto en startup).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>URL base de la API compatible con OpenAI (sin trailing slash).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Identificador del modelo a usar en este proveedor.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Timeout en segundos para las llamadas HTTP.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Indica si este proveedor soporta tool calling (function calling) de OpenAI.
    /// Habilitar solo en proveedores/modelos que lo soporten de forma confiable.
    /// Cuando es false, el proveedor solo usa inyección de contexto (Fase 2).
    /// </summary>
    public bool SupportsToolCalling { get; set; }
}
