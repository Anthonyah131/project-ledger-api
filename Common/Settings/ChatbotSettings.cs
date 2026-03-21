namespace ProjectLedger.API.Common.Settings;

/// <summary>
/// Configuración global del chatbot de IA con rotación de proveedores.
/// Los API keys se resuelven desde variables de entorno.
/// </summary>
public class ChatbotSettings
{
    public const string SectionName = "Chatbot";

    /// <summary>
    /// Mensaje de sistema enviado como primer mensaje en cada petición.
    /// Define la personalidad, el idioma y el contexto del asistente.
    /// Si está vacío no se incluye ningún system prompt.
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
}
