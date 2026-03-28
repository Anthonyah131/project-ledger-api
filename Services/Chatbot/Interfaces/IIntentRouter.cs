using ProjectLedger.API.DTOs.Chatbot;

namespace ProjectLedger.API.Services.Chatbot.Interfaces;

/// <summary>
/// Enruta un intent parseado por el LLM hacia los métodos correspondientes de IMcpService.
/// El router controla toda la lógica de mapeo domain+action → query — el LLM nunca toca la DB.
/// </summary>
public interface IIntentRouter
{
    /// <summary>
    /// Ejecuta el intent: construye el query DTO desde los filtros,
    /// llama al método de IMcpService correspondiente, y devuelve el resultado serializado como JSON.
    /// </summary>
    Task<string> ExecuteAsync(Guid userId, ParsedIntent intent, CancellationToken ct);
}
