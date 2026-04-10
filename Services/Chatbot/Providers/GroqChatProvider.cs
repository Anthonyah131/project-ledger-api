using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectLedger.API.Common.Settings;

namespace ProjectLedger.API.Services.Chatbot.Providers;

/// <summary>
/// Groq Provider: Ultra-fast inference on proprietary LPU hardware.
/// Free tier with token/minute limits per model.
/// Documentation: https://console.groq.com/docs/overview
/// </summary>
public class GroqChatProvider : OpenAiCompatibleChatProvider
{
    // High-capacity free model on Groq
    private const string DefaultModel = "meta-llama/llama-4-scout-17b-16e-instruct";

    private readonly ChatbotProviderSettings _settings;

    public GroqChatProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<ChatbotSettings> options,
        ILogger<GroqChatProvider> logger)
        : base(httpClientFactory, options, logger)
    {
        _settings = options.Value.Groq;
    }

    public override string ProviderName      => "Groq";
    public override string Model             => string.IsNullOrWhiteSpace(_settings.Model) ? DefaultModel : _settings.Model;
    protected override string HttpClientName => "Chatbot.Groq";

    public override bool IsEnabled           => _settings.Enabled && !string.IsNullOrWhiteSpace(_settings.ApiKey);
    public override bool SupportsToolCalling => _settings.SupportsToolCalling;
}
