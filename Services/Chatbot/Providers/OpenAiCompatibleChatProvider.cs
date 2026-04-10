using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectLedger.API.Common.Settings;
using ProjectLedger.API.Services.Chatbot.Interfaces;

namespace ProjectLedger.API.Services.Chatbot.Providers;

/// <summary>
/// Base class for providers whose API is compatible with the standard
/// OpenAI chat completions (POST /chat/completions).
/// Groq, Cerebras, OpenRouter, and BytePlus Ark expose this same contract,
/// differing only in BaseUrl, Model, ApiKey, and SupportsToolCalling.
/// Implements both IChatProvider (plain conversation) and IToolCallingChatProvider
/// (function calling with MCP tools).
/// </summary>
public abstract class OpenAiCompatibleChatProvider : IToolCallingChatProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;

    // ── Members that each concrete provider must implement ───────────────────

    public abstract string ProviderName          { get; }
    public abstract string Model                 { get; }
    public abstract bool   IsEnabled             { get; }
    public abstract bool   SupportsToolCalling   { get; }

    /// <summary>Name registered in IHttpClientFactory for this provider.</summary>
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

        _logger.LogDebug("Sending {Count} messages to {Provider} ({Model})",
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

    // ── IChatProvider: streaming ─────────────────────────────────────────────

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> StreamMessageAsync(
        IReadOnlyList<TcMessage> messages,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);

        var requestBody = new
        {
            model    = Model,
            messages = SerializeMessages(messages),
            stream   = true
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(requestBody)
        };

        // ResponseHeadersRead avoids buffering the entire response body before we start reading.
        using var httpResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        httpResponse.EnsureSuccessStatusCode();

        using var stream = await httpResponse.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        // ReadLineAsync returns null at end-of-stream; avoids CA2024 (EndOfStream in async context).
        while (true)
        {
            var line = await reader.ReadLineAsync(ct);

            if (line is null || ct.IsCancellationRequested) break;
            if (!line.StartsWith("data: ", StringComparison.Ordinal)) continue;

            var payload = line["data: ".Length..];
            if (payload == "[DONE]") break;

            string? chunk;
            try
            {
                var parsed = JsonSerializer.Deserialize<StreamChunk>(payload, _streamJsonOptions);
                chunk = parsed?.Choices?.FirstOrDefault()?.Delta?.Content;
            }
            catch
            {
                // Malformed SSE line — skip silently
                continue;
            }

            if (!string.IsNullOrEmpty(chunk))
                yield return chunk;
        }
    }

    private static readonly JsonSerializerOptions _streamJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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

        _logger.LogDebug("Sending {Count} messages + {ToolCount} tools to {Provider} ({Model})",
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

    // ── Message serialization ────────────────────────────────────────────────

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

    // ── Internal DTOs for deserializing responses ────────────────────────────

    // SSE streaming chunks (stream=true)
    private sealed record StreamChunk(
        [property: JsonPropertyName("choices")] List<StreamChoice>? Choices);

    private sealed record StreamChoice(
        [property: JsonPropertyName("delta")] StreamDelta? Delta);

    private sealed record StreamDelta(
        [property: JsonPropertyName("content")] string? Content);

    // Simple response (without tool calls)
    private sealed record ChatCompletionResponse(
        [property: JsonPropertyName("choices")] List<ChatChoice>? Choices);

    private sealed record ChatChoice(
        [property: JsonPropertyName("message")] ChatMessage? Message);

    private sealed record ChatMessage(
        [property: JsonPropertyName("content")] string? Content);

    // Response with tool calling
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
