using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectLedger.API.Common.Settings;
using ProjectLedger.API.Services.Chatbot.Interfaces;

namespace ProjectLedger.API.Services.Chatbot.Providers;

/// <summary>
/// Clase base para proveedores cuya API es compatible con el estándar
/// OpenAI chat completions (POST /chat/completions).
/// Groq, Cerebras, OpenRouter y BytePlus Ark exponen este mismo contrato,
/// por lo que sólo difieren en BaseUrl, Model y ApiKey.
/// </summary>
public abstract class OpenAiCompatibleChatProvider : IChatProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;
    private readonly ChatbotSettings _settings;

    // ── Miembros que cada proveedor concreto debe implementar ────────────────

    public abstract string ProviderName { get; }
    public abstract string Model        { get; }
    public abstract bool   IsEnabled    { get; }

    /// <summary>Nombre registrado en IHttpClientFactory para este proveedor.</summary>
    protected abstract string HttpClientName { get; }

    protected OpenAiCompatibleChatProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<ChatbotSettings> options,
        ILogger logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings          = options.Value;
        _logger            = logger;
    }

    /// <inheritdoc/>
    public async Task<string> SendMessageAsync(string message, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);

        // Construir la lista de mensajes: system prompt (si existe) + mensaje del usuario.
        // El system prompt instruye al modelo sobre su identidad, idioma y comportamiento.
        var messages = BuildMessages(message);

        var requestBody = new { model = Model, messages };

        _logger.LogDebug("Enviando mensaje a {Provider} con modelo {Model}", ProviderName, Model);

        // Todas las APIs devuelven 4xx/5xx en errores — EnsureSuccessStatusCode lanzará
        // HttpRequestException que el ChatbotService captura para pasar al siguiente proveedor.
        var httpResponse = await client.PostAsJsonAsync("chat/completions", requestBody, ct);
        httpResponse.EnsureSuccessStatusCode();

        var completion = await httpResponse.Content.ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException($"{ProviderName} devolvió un body vacío.");

        var content = completion.Choices?.FirstOrDefault()?.Message?.Content;

        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException($"{ProviderName} devolvió un mensaje vacío.");

        return content;
    }

    // ── Construcción del array de mensajes ───────────────────────────────────

    /// <summary>
    /// Devuelve el array de mensajes a enviar al modelo.
    /// Si hay system prompt configurado, va primero con role "system".
    /// </summary>
    private object[] BuildMessages(string userMessage)
    {
        var systemPrompt = _settings.SystemPrompt;

        if (string.IsNullOrWhiteSpace(systemPrompt))
            return [new { role = "user", content = userMessage }];

        return
        [
            new { role = "system", content = systemPrompt },
            new { role = "user",   content = userMessage  }
        ];
    }

    // ── DTOs internos para deserializar la respuesta de chat completions ─────

    private sealed record ChatCompletionResponse(
        [property: JsonPropertyName("choices")] List<ChatChoice>? Choices);

    private sealed record ChatChoice(
        [property: JsonPropertyName("message")] ChatMessage? Message);

    private sealed record ChatMessage(
        [property: JsonPropertyName("content")] string? Content);
}
