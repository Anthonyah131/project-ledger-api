using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectLedger.API.Common.Settings;

namespace ProjectLedger.API.Services.Chatbot.Providers;

/// <summary>
/// BytePlus Provider (Doubao / Ark): ByteDance's LLM platform available outside of China.
/// The API is OpenAI-compatible. The model is an endpoint ID with the format ep-XXXX assigned
/// when creating the deployment in the BytePlus Ark console.
/// Documentation: https://docs.byteplus.com/en/docs/ModelArk
/// </summary>
public class BytePlusChatProvider : OpenAiCompatibleChatProvider
{
    // The default model is the endpoint ID assigned in BytePlus Ark.
    // Must be configured in appsettings or the CHATBOT_BYTEPLUS_MODEL environment variable.
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
