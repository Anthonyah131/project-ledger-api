using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectLedger.API.Common.Settings;

namespace ProjectLedger.API.Services.Chatbot.Providers;

/// <summary>
/// Cerebras Provider: High-performance AI chips (WSE).
/// Free tier available with minute/day limits.
/// Documentation: https://inference-docs.cerebras.ai/introduction
/// </summary>
public class CerebrasChatProvider : OpenAiCompatibleChatProvider
{
    // Cerebras free high-capacity model with broad context
    private const string DefaultModel = "llama3.1-8b";

    private readonly ChatbotProviderSettings _settings;

    public CerebrasChatProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<ChatbotSettings> options,
        ILogger<CerebrasChatProvider> logger)
        : base(httpClientFactory, options, logger)
    {
        _settings = options.Value.Cerebras;
    }

    public override string ProviderName      => "Cerebras";
    public override string Model             => string.IsNullOrWhiteSpace(_settings.Model) ? DefaultModel : _settings.Model;
    protected override string HttpClientName => "Chatbot.Cerebras";

    public override bool IsEnabled           => _settings.Enabled && !string.IsNullOrWhiteSpace(_settings.ApiKey);
    public override bool SupportsToolCalling => _settings.SupportsToolCalling;
}
