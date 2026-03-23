using Microsoft.Extensions.Logging;
using ProjectLedger.API.DTOs.Chatbot;
using ProjectLedger.API.Services.Chatbot.Interfaces;

namespace ProjectLedger.API.Services.Chatbot;

/// <summary>
/// Orquesta la rotación de proveedores de IA.
/// Cada mensaje comienza por el siguiente proveedor en la lista (round-robin).
/// Si ese proveedor falla (límite alcanzado, error de red, key inválida),
/// se prueba el siguiente hasta agotar todos los habilitados.
/// </summary>
public class ChatbotService : IChatbotService
{
    private readonly IEnumerable<IChatProvider> _providers;
    private readonly ChatbotProviderRotator     _rotator;
    private readonly ILogger<ChatbotService>    _logger;

    public ChatbotService(
        IEnumerable<IChatProvider> providers,
        ChatbotProviderRotator rotator,
        ILogger<ChatbotService> logger)
    {
        _providers = providers;
        _rotator   = rotator;
        _logger    = logger;
    }

    /// <inheritdoc/>
    public async Task<ChatbotMessageResponse> SendMessageAsync(string message, CancellationToken ct)
    {
        // Sólo considerar proveedores activos (key configurada + Enabled=true)
        var enabled = _providers.Where(p => p.IsEnabled).ToList();

        if (enabled.Count == 0)
            throw new InvalidOperationException("ChatbotNoProvidersEnabled");

        // Índice de inicio de esta petición (avanza con cada llamada)
        var startIndex = _rotator.GetNext(enabled.Count);

        for (int attempt = 0; attempt < enabled.Count; attempt++)
        {
            var provider = enabled[(startIndex + attempt) % enabled.Count];

            try
            {
                _logger.LogDebug(
                    "Chatbot intento {Attempt}/{Total} — proveedor: {Provider}",
                    attempt + 1, enabled.Count, provider.ProviderName);

                var response = await provider.SendMessageAsync(message, ct);

                _logger.LogInformation(
                    "Chatbot respondió con {Provider} ({Model})", provider.ProviderName, provider.Model);

                return new ChatbotMessageResponse
                {
                    Response = response,
                    Provider = provider.ProviderName,
                    Model    = provider.Model
                };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Loguear y probar el siguiente proveedor
                _logger.LogWarning(
                    ex,
                    "Proveedor {Provider} no disponible (intento {Attempt}/{Total}), pasando al siguiente.",
                    provider.ProviderName, attempt + 1, enabled.Count);
            }
        }

        throw new InvalidOperationException("ChatbotAllProvidersUnavailable");
    }
}
