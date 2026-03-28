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
/// por lo que sólo difieren en BaseUrl, Model, ApiKey y SupportsToolCalling.
/// Implementa tanto IChatProvider (conversación simple) como IToolCallingChatProvider
/// (function calling con herramientas MCP).
/// </summary>
public abstract class OpenAiCompatibleChatProvider : IToolCallingChatProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;

    // ── Miembros que cada proveedor concreto debe implementar ────────────────

    public abstract string ProviderName          { get; }
    public abstract string Model                 { get; }
    public abstract bool   IsEnabled             { get; }
    public abstract bool   SupportsToolCalling   { get; }

    /// <summary>Nombre registrado en IHttpClientFactory para este proveedor.</summary>
    protected abstract string HttpClientName { get; }

    protected OpenAiCompatibleChatProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<ChatbotSettings> options,
        ILogger logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger            = logger;
    }

    // ── IChatProvider ────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<string> SendMessageAsync(IReadOnlyList<TcMessage> messages, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);

        var requestBody = new
        {
            model    = Model,
            messages = SerializeMessages(messages)
        };

        _logger.LogDebug("Enviando {Count} mensajes a {Provider} ({Model})",
            messages.Count, ProviderName, Model);

        var httpResponse = await client.PostAsJsonAsync("chat/completions", requestBody, ct);
        httpResponse.EnsureSuccessStatusCode();

        var completion = await httpResponse.Content.ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("ChatbotProviderEmptyBody");

        var content = completion.Choices?.FirstOrDefault()?.Message?.Content;

        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("ChatbotProviderEmptyMessage");

        return content;
    }

    // ── IToolCallingChatProvider ─────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<ToolCallingResponse> SendWithToolsAsync(
        IReadOnlyList<TcMessage> messages,
        IReadOnlyList<ToolDefinition> tools,
        CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);

        object requestBody;

        if (tools.Count > 0)
        {
            requestBody = new
            {
                model    = Model,
                messages = SerializeMessages(messages),
                tools    = tools.Select(t => new
                {
                    type     = "function",
                    function = new
                    {
                        name        = t.Name,
                        description = t.Description,
                        parameters  = t.ParametersSchema
                    }
                }).ToArray(),
                tool_choice = "auto"
            };
        }
        else
        {
            requestBody = new
            {
                model    = Model,
                messages = SerializeMessages(messages)
            };
        }

        _logger.LogDebug("Enviando {Count} mensajes + {ToolCount} tools a {Provider} ({Model})",
            messages.Count, tools.Count, ProviderName, Model);

        var httpResponse = await client.PostAsJsonAsync("chat/completions", requestBody, ct);
        httpResponse.EnsureSuccessStatusCode();

        var completion = await httpResponse.Content.ReadFromJsonAsync<ToolCallingCompletionResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("ChatbotProviderEmptyBody");

        var choice = completion.Choices?.FirstOrDefault()?.Message;

        if (choice?.ToolCalls is { Count: > 0 })
        {
            var calls = choice.ToolCalls
                .Select(tc => new ToolCallRef(
                    tc.Id           ?? string.Empty,
                    tc.Function?.Name      ?? string.Empty,
                    tc.Function?.Arguments ?? "{}"))
                .ToList();

            return new ToolCallingResponse(null, calls);
        }

        if (string.IsNullOrWhiteSpace(choice?.Content))
            throw new InvalidOperationException("ChatbotProviderEmptyMessage");

        return new ToolCallingResponse(choice.Content, null);
    }

    // ── Serialización de mensajes ────────────────────────────────────────────

    private static object[] SerializeMessages(IReadOnlyList<TcMessage> messages) =>
        messages.Select<TcMessage, object>(m => m switch
        {
            TcSystemMessage s => new
            {
                role    = "system",
                content = s.Content
            },
            TcUserMessage u => new
            {
                role    = "user",
                content = u.Content
            },
            TcAssistantMessage { ToolCalls: { Count: > 0 } } a => new
            {
                role       = "assistant",
                content    = (string?)null,
                tool_calls = a.ToolCalls!.Select(tc => new
                {
                    id       = tc.Id,
                    type     = "function",
                    function = new { name = tc.FunctionName, arguments = tc.ArgumentsJson }
                }).ToArray()
            },
            TcAssistantMessage a => new
            {
                role    = "assistant",
                content = a.Content
            },
            TcToolResultMessage t => new
            {
                role         = "tool",
                tool_call_id = t.ToolCallId,
                content      = t.Content
            },
            _ => throw new InvalidOperationException($"Unsupported message type: {m.GetType().Name}")
        }).ToArray();

    // ── DTOs internos para deserializar respuestas ───────────────────────────

    // Respuesta simple (sin tool calls)
    private sealed record ChatCompletionResponse(
        [property: JsonPropertyName("choices")] List<ChatChoice>? Choices);

    private sealed record ChatChoice(
        [property: JsonPropertyName("message")] ChatMessage? Message);

    private sealed record ChatMessage(
        [property: JsonPropertyName("content")] string? Content);

    // Respuesta con tool calling
    private sealed record ToolCallingCompletionResponse(
        [property: JsonPropertyName("choices")] List<ToolCallingChoice>? Choices);

    private sealed record ToolCallingChoice(
        [property: JsonPropertyName("message")] ToolCallingMessage? Message);

    private sealed record ToolCallingMessage(
        [property: JsonPropertyName("content")]    string?          Content,
        [property: JsonPropertyName("tool_calls")] List<ApiToolCall>? ToolCalls);

    private sealed record ApiToolCall(
        [property: JsonPropertyName("id")]       string?             Id,
        [property: JsonPropertyName("type")]     string?             Type,
        [property: JsonPropertyName("function")] ApiToolCallFunction? Function);

    private sealed record ApiToolCallFunction(
        [property: JsonPropertyName("name")]      string? Name,
        [property: JsonPropertyName("arguments")] string? Arguments);
}
