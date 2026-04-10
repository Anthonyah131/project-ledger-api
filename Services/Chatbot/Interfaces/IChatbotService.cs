using ProjectLedger.API.DTOs.Chatbot;

namespace ProjectLedger.API.Services.Chatbot.Interfaces;

/// <summary>
/// Main chatbot service: orchestrates provider rotation,
/// injects real user financial context and includes conversation history.
/// </summary>
public interface IChatbotService
{
    /// <summary>
    /// Streams the LLM response token by token via SSE.
    /// Yields a "meta" event first, then "chunk" events, and finally a "done" event.
    /// Intent parsing and backend routing remain non-streaming (they are fast and structured).
    /// </summary>
    IAsyncEnumerable<ChatbotStreamEvent> StreamMessageAsync(
        Guid userId,
        string message,
        IReadOnlyList<ChatbotHistoryEntry>? history,
        CancellationToken ct);
}
