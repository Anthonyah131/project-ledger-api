using ProjectLedger.API.DTOs.Chatbot;

namespace ProjectLedger.API.Services.Chatbot.Interfaces;

/// <summary>
/// Routes an intent parsed by the LLM to the corresponding IMcpService methods.
/// The router controls all mapping logic (domain + action → query) — the LLM never touches the DB.
/// </summary>
public interface IIntentRouter
{
    /// <summary>
    /// Executes the intent: builds the query DTO from the filters,
    /// calls the corresponding IMcpService method, and returns the result serialized as JSON.
    /// </summary>
    Task<string> ExecuteAsync(Guid userId, ParsedIntent intent, CancellationToken ct);
}
