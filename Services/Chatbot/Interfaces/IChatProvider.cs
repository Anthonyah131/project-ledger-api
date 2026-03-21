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
    /// Envía un mensaje al proveedor y devuelve la respuesta en texto plano.
    /// Lanza excepción si el proveedor no está disponible o hay un error de red/API.
    /// </summary>
    Task<string> SendMessageAsync(string message, CancellationToken ct);
}
