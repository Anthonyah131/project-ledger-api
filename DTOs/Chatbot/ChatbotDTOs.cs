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

/// <summary>Respuesta del chatbot con metadata del proveedor utilizado.</summary>
public class ChatbotMessageResponse
{
    /// <summary>Texto de respuesta generado por el modelo de IA.</summary>
    public string Response { get; set; } = string.Empty;

    /// <summary>Nombre del proveedor que procesó la petición (OpenRouter, Groq, Cerebras, BytePlus).</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Identificador del modelo utilizado dentro del proveedor.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Indica si se inyectó contexto financiero real (resumen mensual + pagos vencidos)
    /// en el system prompt de esta petición.
    /// </summary>
    public bool UsedFinancialContext { get; set; }

    /// <summary>
    /// Número de llamadas a herramientas MCP ejecutadas por el LLM en esta petición.
    /// 0 si el proveedor no soporta tool calling o el LLM respondió sin consultar datos.
    /// </summary>
    public int ToolCallsExecuted { get; set; }
}
