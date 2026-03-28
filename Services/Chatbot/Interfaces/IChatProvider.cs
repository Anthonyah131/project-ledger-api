namespace ProjectLedger.API.Services.Chatbot.Interfaces;

/// <summary>
/// Contrato que debe cumplir cada proveedor de IA del chatbot.
/// Todas las implementaciones exponen una API compatible con OpenAI chat completions.
/// </summary>
public interface IChatProvider
{
    /// <summary>Nombre legible del proveedor (OpenRouter, Groq, Cerebras, BytePlus).</summary>
    string ProviderName { get; }

    /// <summary>Modelo utilizado por este proveedor.</summary>
    string Model { get; }

    /// <summary>
    /// Indica si el proveedor está habilitado (tiene key configurada y Enabled=true).
    /// Un proveedor deshabilitado se omite en la rotación.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Indica si el proveedor/modelo soporta tool calling (function calling).
    /// Cuando es false, el proveedor solo puede recibir conversaciones planas.
    /// </summary>
    bool SupportsToolCalling { get; }

    /// <summary>
    /// Envía la conversación al proveedor y devuelve la respuesta en texto plano.
    /// El array debe incluir el system message, el historial y el mensaje actual.
    /// Lanza excepción si el proveedor no está disponible o hay un error de red/API.
    /// </summary>
    Task<string> SendMessageAsync(IReadOnlyList<TcMessage> messages, CancellationToken ct);
}
