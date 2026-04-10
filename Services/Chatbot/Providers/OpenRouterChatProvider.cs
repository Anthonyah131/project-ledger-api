using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectLedger.API.Common.Settings;

namespace ProjectLedger.API.Services.Chatbot.Providers;

/// <summary>
/// OpenRouter Provider: Aggregates hundreds of models under a single API.
/// Free tier available for models with the :free suffix.
/// Documentation: https://openrouter.ai/docs
/// </summary>
public class OpenRouterChatProvider : OpenAiCompatibleChatProvider
{
    // Recommended free model from OpenRouter (no cost, no credits needed)
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
