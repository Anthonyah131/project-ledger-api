using System.ComponentModel.DataAnnotations;

namespace ProjectLedger.API.DTOs.Chatbot;

/// <summary>Request para enviar un mensaje al chatbot.</summary>
public class ChatbotMessageRequest
{
    /// <summary>Texto del mensaje del usuario.</summary>
    [Required]
    [MinLength(1)]
    [MaxLength(4000)]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Historial de mensajes anteriores de la sesión (role: "user" | "assistant").
    /// El cliente es responsable de mantener y enviar el historial en cada request.
    /// Se trunca automáticamente a los últimos 20 mensajes antes de enviarlo al LLM.
    /// </summary>
    public List<ChatbotHistoryEntry>? History { get; set; }
}

/// <summary>Entrada del historial de conversación.</summary>
public class ChatbotHistoryEntry
{
    /// <summary>Rol del mensaje: "user" o "assistant".</summary>
    [Required]
    public string Role { get; set; } = string.Empty;

    /// <summary>Contenido del mensaje.</summary>
    [Required]
    [MaxLength(8000)]
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// SSE event emitted by the streaming endpoint (POST /message/stream).
/// The <see cref="Type"/> field acts as a discriminator:
/// - "meta"  → sent once before the first chunk; contains provider, model, and pipeline metadata.
/// - "chunk" → one partial text token per event; read <see cref="Content"/>.
/// - "done"  → sent once after the last chunk to signal stream completion.
/// - "error" → sent if the pipeline fails; read <see cref="Content"/> for the message.
/// </summary>
public class ChatbotStreamEvent
{
    public string  Type                  { get; set; } = string.Empty;
    public string? Content               { get; set; }
    public string? Provider              { get; set; }
    public string? Model                 { get; set; }
    public bool?   UsedFinancialContext  { get; set; }
    public int?    ToolCallsExecuted     { get; set; }
}
