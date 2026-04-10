namespace ProjectLedger.API.Services.Chatbot.Interfaces;

/// <summary>
/// Contract that each chatbot AI provider must fulfill.
/// All implementations expose an API compatible with OpenAI chat completions.
/// </summary>
public interface IChatProvider
{
    /// <summary>Readable name of the provider (OpenRouter, Groq, Cerebras, BytePlus).</summary>
    string ProviderName { get; }

    /// <summary>Model used by this provider.</summary>
    string Model { get; }

    /// <summary>
    /// Indicates if the provider is enabled (has a configured key and Enabled=true).
    /// A disabled provider is omitted from rotation.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Indicates if the provider/model supports tool calling (function calling).
    /// When false, the provider can only receive plain text conversations.
    /// </summary>
    bool SupportsToolCalling { get; }

    /// <summary>
    /// Sends the conversation to the provider and returns the response in plain text.
    /// The array must include the system message, history, and current message.
    /// Throws an exception if the provider is unavailable or there is a network/API error.
    /// </summary>
    Task<string> SendMessageAsync(IReadOnlyList<TcMessage> messages, CancellationToken ct);

    /// <summary>
    /// Streams the final response token by token using the provider's SSE endpoint (stream=true).
    /// Each yielded string is a partial text chunk (delta content).
    /// Throws on network or API errors.
    /// </summary>
    IAsyncEnumerable<string> StreamMessageAsync(IReadOnlyList<TcMessage> messages, CancellationToken ct);
}
