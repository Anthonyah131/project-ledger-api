using ProjectLedger.API.DTOs.Chatbot;

namespace ProjectLedger.API.Services.Chatbot.Interfaces;

/// <summary>
/// Servicio principal del chatbot: orquesta la rotación de proveedores
/// y devuelve la respuesta junto con metadata del proveedor utilizado.
/// </summary>
public interface IChatbotService
{
    /// <summary>
    /// Envía un mensaje usando el siguiente proveedor en la rotación.
    /// Si el proveedor asignado falla, prueba con el siguiente hasta agotar todos.
    /// </summary>
    Task<ChatbotMessageResponse> SendMessageAsync(string message, CancellationToken ct);
}
