using ProjectLedger.API.DTOs.Chatbot;

namespace ProjectLedger.API.Services.Chatbot.Interfaces;

/// <summary>
/// Servicio principal del chatbot: orquesta la rotación de proveedores,
/// inyecta contexto financiero real del usuario e incluye el historial de conversación.
/// </summary>
public interface IChatbotService
{
    /// <summary>
    /// Envía un mensaje con contexto financiero inyectado y el historial de la sesión.
    /// Si el proveedor asignado falla, prueba con el siguiente hasta agotar todos.
    /// </summary>
    Task<ChatbotMessageResponse> SendMessageAsync(
        Guid userId,
        string message,
        IReadOnlyList<ChatbotHistoryEntry>? history,
        CancellationToken ct);
}
