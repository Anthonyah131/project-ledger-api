using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectLedger.API.Common.Settings;

namespace ProjectLedger.API.Services.Chatbot.Providers;

/// <summary>
/// Proveedor OpenRouter: agrega cientos de modelos bajo una sola API.
/// Capa gratuita disponible en modelos con sufijo :free.
/// Documentación: https://openrouter.ai/docs
/// </summary>
public class OpenRouterChatProvider : OpenAiCompatibleChatProvider
{
    // Modelo gratuito recomendado de OpenRouter (sin coste, sin necesidad de créditos)
    private const string DefaultModel = "openrouter/free";

    private readonly ChatbotProviderSettings _settings;

    public OpenRouterChatProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<ChatbotSettings> options,
        ILogger<OpenRouterChatProvider> logger)
        : base(httpClientFactory, options, logger)
    {
        _settings = options.Value.OpenRouter;
    }

    public override string ProviderName      => "OpenRouter";
    public override string Model             => string.IsNullOrWhiteSpace(_settings.Model) ? DefaultModel : _settings.Model;
    protected override string HttpClientName => "Chatbot.OpenRouter";

    public override bool IsEnabled           => _settings.Enabled && !string.IsNullOrWhiteSpace(_settings.ApiKey);
    public override bool SupportsToolCalling => _settings.SupportsToolCalling;
}
