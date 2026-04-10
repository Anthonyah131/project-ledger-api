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
/// Configuration for an individual AI provider.
/// </summary>
public class ChatbotProviderSettings
{
    /// <summary>Enables or disables the provider without removing its configuration.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Provider's API key (can be a placeholder like ${ENV_VAR} resolved at startup).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Base URL for the OpenAI-compatible API (without trailing slash).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Identifier of the model to use for this provider.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Timeout in seconds for HTTP calls to the provider.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Indicates whether this provider supports OpenAI-style tool calling (function calling).
    /// Enable only for providers/models that support it reliably.
    /// When false, the provider only uses context injection (Phase 2).
    /// </summary>
    public bool SupportsToolCalling { get; set; }
}
