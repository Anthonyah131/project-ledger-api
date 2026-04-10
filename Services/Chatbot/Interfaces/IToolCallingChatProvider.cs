namespace ProjectLedger.API.Services.Chatbot.Interfaces;

// ── Message types for conversations with tool calling ─────────────────────────

/// <summary>Base message for the conversation with the LLM.</summary>
public abstract record TcMessage;

/// <summary>System message (assistant instructions).</summary>
public sealed record TcSystemMessage(string Content) : TcMessage;

/// <summary>User message.</summary>
public sealed record TcUserMessage(string Content) : TcMessage;

/// <summary>
/// Assistant message. If the LLM called tools, Content is null and ToolCalls
/// contains the calls to be executed. If it responded with text, Content has the response.
/// </summary>
public sealed record TcAssistantMessage(
    string? Content,
    IReadOnlyList<ToolCallRef>? ToolCalls = null) : TcMessage;

/// <summary>Result of executing a tool, sent back to the LLM.</summary>
public sealed record TcToolResultMessage(
    string ToolCallId,
    string FunctionName,
    string Content) : TcMessage;

// ── Tools ────────────────────────────────────────────────────────────────────

/// <summary>Definition of a tool (function) exposed to the LLM.</summary>
public sealed record ToolDefinition(
    string Name,
    string Description,
    object ParametersSchema);  // Anonymous object that serializes to JSON Schema

/// <summary>Reference to a tool call that the LLM wants to execute.</summary>
public sealed record ToolCallRef(
    string Id,
    string FunctionName,
    string ArgumentsJson);

// ── Response of the request with tools ───────────────────────────────────────

/// <summary>
/// Result of a tool calling round-trip.
/// If the LLM called tools: Content is null, ToolCalls has the calls.
/// If the LLM responded with text: Content has the response, ToolCalls is null.
/// </summary>
public sealed record ToolCallingResponse(
    string? Content,
    IReadOnlyList<ToolCallRef>? ToolCalls);

// ── Interface ────────────────────────────────────────────────────────────────

/// <summary>
/// IChatProvider extension for providers that support tool calling (function calling).
/// Allows the LLM to decide which tools to call and receive their results.
/// </summary>
public interface IToolCallingChatProvider : IChatProvider
{
    /// <summary>
    /// Sends messages to the LLM along with the available tools.
    /// Returns text if the LLM responded directly, or the list of tool calls to execute.
    /// </summary>
    Task<ToolCallingResponse> SendWithToolsAsync(
        IReadOnlyList<TcMessage> messages,
        IReadOnlyList<ToolDefinition> tools,
        CancellationToken ct);
}
