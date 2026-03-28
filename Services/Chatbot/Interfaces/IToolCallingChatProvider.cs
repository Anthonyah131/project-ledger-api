namespace ProjectLedger.API.Services.Chatbot.Interfaces;

// ── Tipos de mensajes para conversaciones con tool calling ───────────────────

/// <summary>Mensaje base de la conversación con el LLM.</summary>
public abstract record TcMessage;

/// <summary>Mensaje de sistema (instrucciones del asistente).</summary>
public sealed record TcSystemMessage(string Content) : TcMessage;

/// <summary>Mensaje del usuario.</summary>
public sealed record TcUserMessage(string Content) : TcMessage;

/// <summary>
/// Mensaje del asistente. Si el LLM llamó herramientas, Content es null y ToolCalls
/// contiene las llamadas a ejecutar. Si respondió con texto, Content tiene la respuesta.
/// </summary>
public sealed record TcAssistantMessage(
    string? Content,
    IReadOnlyList<ToolCallRef>? ToolCalls = null) : TcMessage;

/// <summary>Resultado de ejecutar una herramienta, enviado de vuelta al LLM.</summary>
public sealed record TcToolResultMessage(
    string ToolCallId,
    string FunctionName,
    string Content) : TcMessage;

// ── Herramientas ─────────────────────────────────────────────────────────────

/// <summary>Definición de una herramienta (function) expuesta al LLM.</summary>
public sealed record ToolDefinition(
    string Name,
    string Description,
    object ParametersSchema);  // Objeto anonimo que serializa a JSON Schema

/// <summary>Referencia a una llamada de herramienta que el LLM quiere ejecutar.</summary>
public sealed record ToolCallRef(
    string Id,
    string FunctionName,
    string ArgumentsJson);

// ── Respuesta de la petición con herramientas ────────────────────────────────

/// <summary>
/// Resultado de un round-trip con tool calling.
/// Si el LLM llamó herramientas: Content es null, ToolCalls tiene las llamadas.
/// Si el LLM respondió con texto: Content tiene la respuesta, ToolCalls es null.
/// </summary>
public sealed record ToolCallingResponse(
    string? Content,
    IReadOnlyList<ToolCallRef>? ToolCalls);

// ── Interfaz ─────────────────────────────────────────────────────────────────

/// <summary>
/// Extensión de IChatProvider para proveedores que soportan tool calling (function calling).
/// Permite al LLM decidir qué herramientas llamar y recibir sus resultados.
/// </summary>
public interface IToolCallingChatProvider : IChatProvider
{
    /// <summary>
    /// Envía mensajes al LLM junto con las herramientas disponibles.
    /// Devuelve texto si el LLM respondió directamente, o la lista de tool calls a ejecutar.
    /// </summary>
    Task<ToolCallingResponse> SendWithToolsAsync(
        IReadOnlyList<TcMessage> messages,
        IReadOnlyList<ToolDefinition> tools,
        CancellationToken ct);
}
