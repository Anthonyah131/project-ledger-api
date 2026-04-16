using System.ComponentModel.DataAnnotations;

namespace ProjectLedger.API.DTOs.Chatbot;

/// <summary>Request to send a message to the chatbot.</summary>
public class ChatbotMessageRequest
{
    /// <summary>User message text.</summary>
    [Required]
    [MinLength(1)]
    [MaxLength(4000)]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Previous session message history (role: "user" | "assistant").
    /// The client is responsible for maintaining and sending the history in each request.
    /// It is automatically truncated to the last 20 messages before sending to the LLM.
    /// </summary>
    public List<ChatbotHistoryEntry>? History { get; set; }
}

/// <summary>Conversation history entry.</summary>
public class ChatbotHistoryEntry
{
    /// <summary>Message role: "user" or "assistant".</summary>
    [Required]
    [AllowedValues("user", "assistant")]
    public string Role { get; set; } = string.Empty;

    /// <summary>Message content.</summary>
    [Required]
    [MaxLength(8000)]
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// SSE event emitted by the streaming endpoint (POST /message/stream).
/// The <see cref="Type"/> field acts as a discriminator:
/// - "meta"  → sent once before the first chunk; contains pipeline metadata.
/// - "chunk" → one partial text token per event; read <see cref="Content"/>.
/// - "done"  → sent once after the last chunk to signal stream completion.
/// - "error" → sent if the pipeline fails; read <see cref="Content"/> for the message.
/// </summary>
public class ChatbotStreamEvent
{
    public string  Type                  { get; set; } = string.Empty;
    public string? Content               { get; set; }
    public bool?   UsedFinancialContext  { get; set; }
    public int?    ToolCallsExecuted     { get; set; }
}
