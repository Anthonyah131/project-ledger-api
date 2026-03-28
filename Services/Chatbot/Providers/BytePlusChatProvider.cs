using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectLedger.API.Common.Settings;

namespace ProjectLedger.API.Services.Chatbot.Providers;

/// <summary>
/// Proveedor BytePlus (Doubao / Ark): plataforma de LLM de ByteDance disponible fuera de China.
/// La API es compatible con OpenAI. El modelo es un endpoint ID con formato ep-XXXX asignado
/// al crear el deployment en la consola de BytePlus Ark.
/// Documentación: https://docs.byteplus.com/en/docs/ModelArk
/// </summary>
public class BytePlusChatProvider : OpenAiCompatibleChatProvider
{
    // El modelo por defecto es el endpoint ID asignado en BytePlus Ark.
    // Debe configurarse en appsettings o en la variable de entorno CHATBOT_BYTEPLUS_MODEL.
    private const string DefaultModel = "doubao-lite-4k";

    private readonly ChatbotProviderSettings _settings;

    public BytePlusChatProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<ChatbotSettings> options,
        ILogger<BytePlusChatProvider> logger)
        : base(httpClientFactory, options, logger)
    {
        _settings = options.Value.BytePlus;
    }

    public override string ProviderName      => "BytePlus";
    public override string Model             => string.IsNullOrWhiteSpace(_settings.Model) ? DefaultModel : _settings.Model;
    protected override string HttpClientName => "Chatbot.BytePlus";

    public override bool IsEnabled           => _settings.Enabled && !string.IsNullOrWhiteSpace(_settings.ApiKey);
    public override bool SupportsToolCalling => _settings.SupportsToolCalling;
}
