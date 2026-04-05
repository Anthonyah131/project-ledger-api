using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ProjectLedger.API.DTOs.Chatbot;
using ProjectLedger.API.DTOs.Mcp;
using ProjectLedger.API.Services.Chatbot.Interfaces;

namespace ProjectLedger.API.Services.Chatbot;

/// <summary>
/// Orquesta el pipeline de 2 pasos basado en intents:
///
/// LLM Call #1 (Intent Parser): clasifica domain + action + filters como JSON estructurado.
/// Backend (IntentRouter): mapea el intent a IMcpService y ejecuta la query.
/// LLM Call #2 (Response Formatter): genera la respuesta final en lenguaje natural.
///
/// Para greeting/context_only/off_topic se salta el paso de tools (0 tool executions).
/// Usa rotación round-robin de proveedores (máximo 2 providers por request).
/// </summary>
public class ChatbotService : IChatbotService
{
    private const int MaxHistoryEntries = 20;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IEnumerable<IChatProvider> _providers;
    private readonly ChatbotProviderRotator _rotator;
    private readonly IIntentRouter _intentRouter;
    private readonly IMcpService _mcpService;
    private readonly ILogger<ChatbotService> _logger;

    public ChatbotService(
        IEnumerable<IChatProvider> providers,
        ChatbotProviderRotator rotator,
        IIntentRouter intentRouter,
        IMcpService mcpService,
        ILogger<ChatbotService> logger)
    {
        _providers = providers;
        _rotator = rotator;
        _intentRouter = intentRouter;
        _mcpService = mcpService;
        _logger = logger;
    }

    // ── Streaming pipeline ────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async IAsyncEnumerable<ChatbotStreamEvent> StreamMessageAsync(
        Guid userId,
        string message,
        IReadOnlyList<ChatbotHistoryEntry>? history,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var enabled = _providers.Where(p => p.IsEnabled).ToList();

        if (enabled.Count == 0)
            throw new InvalidOperationException("ChatbotNoProvidersEnabled");

        var today      = DateTime.UtcNow;
        var startIndex = _rotator.GetNext(enabled.Count);

        var (contextSummary, usedFinancialContext) = await BuildContextSummaryAsync(userId, ct);

        // ── LLM Call #1: Intent Parser (non-streaming — structured JSON output) ──
        var parserProvider  = GetProvider(enabled, startIndex, 0);
        ParsedIntent? intent = null;

        try
        {
            var parserSystemPrompt = BuildParserSystemPrompt(today, contextSummary);
            var parserMessages     = BuildMessagesList(parserSystemPrompt, history, message);

            _logger.LogDebug("Intent Parser — provider: {Provider}", parserProvider.ProviderName);
            var parserResponse = await parserProvider.SendMessageAsync(parserMessages, ct);

            _logger.LogInformation("Intent Parser raw response: {R}", parserResponse);
            intent = ParseIntent(parserResponse);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Intent Parser ({Provider}) failed — streaming fallback", parserProvider.ProviderName);
        }

        // Yield is not allowed inside catch blocks — handle fallback here.
        if (intent is null)
        {
            yield return MetaEvent(parserProvider, usedFinancialContext, 0);
            yield return ChunkEvent("Lo siento, no pude procesar tu consulta en este momento. Intenta de nuevo.");
            yield return DoneEvent();
            yield break;
        }

        // ── Determine data source and tool count ─────────────────────────────
        string dataOrContext;
        int    toolCallsExecuted;
        IReadOnlyList<ChatbotHistoryEntry>? formatterHistory;
        bool   finalUsedFinancialContext;

        if (intent.Domain is "context_only" or "greeting" or "off_topic")
        {
            _logger.LogDebug("Intent domain={Domain} — short-circuit, no tools", intent.Domain);
            dataOrContext            = contextSummary;
            toolCallsExecuted        = 0;
            formatterHistory         = history;
            finalUsedFinancialContext = usedFinancialContext;
        }
        else
        {
            _logger.LogDebug("IntentRouter: {Domain}/{Action}", intent.Domain, intent.Action);
            dataOrContext            = await _intentRouter.ExecuteAsync(userId, intent, ct);
            toolCallsExecuted        = 1;
            formatterHistory         = null;
            finalUsedFinancialContext = true;
        }

        var responseProvider = GetProvider(enabled, startIndex, 1);

        _logger.LogInformation(
            "Stream pipeline: Parser({P1})→Router→Formatter({P2}), intent={Domain}/{Action}",
            parserProvider.ProviderName, responseProvider.ProviderName, intent.Domain, intent.Action);

        // Send metadata before the first text chunk so the client knows the provider/model immediately.
        yield return MetaEvent(responseProvider, finalUsedFinancialContext, toolCallsExecuted);

        // ── LLM Call #2: Response Formatter (streaming) ──────────────────────
        await foreach (var chunk in StreamFormatResponseAsync(
            responseProvider, message, dataOrContext, intent, formatterHistory, ct))
        {
            yield return chunk;
        }

        yield return DoneEvent();
    }

    /// <summary>
    /// Builds the formatter prompt (same logic as <see cref="FormatResponseAsync"/>)
    /// and streams the provider response chunk by chunk.
    /// Falls back to a single error chunk if the provider throws.
    /// </summary>
    private async IAsyncEnumerable<ChatbotStreamEvent> StreamFormatResponseAsync(
        IChatProvider provider,
        string userMessage,
        string dataOrContext,
        ParsedIntent intent,
        IReadOnlyList<ChatbotHistoryEntry>? history,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var today = DateTime.UtcNow;
        var lang  = intent.Lang == "en" ? "English" : "Spanish";

        var baseRules =
            "Be concise and direct — answer the question in 1-3 sentences when possible. " +
            "Do not add extra explanations or suggestions unless the user asked. " +
            "Do not use markdown formatting (no **, no *, no #, no ---). " +
            "Write plain conversational text. For lists use simple line breaks, not bullet points or tables.";

        string systemPrompt;
        string formatterUserMessage;

        if (intent.Domain is "greeting" or "off_topic")
        {
            systemPrompt =
                $"You are a financial assistant. Today is {today:yyyy-MM-dd}. Answer in {lang}. {baseRules} " +
                "If the user greets you, greet them back briefly. " +
                "If the question is off-topic, say in one sentence that you only help with finances.";
            formatterUserMessage = userMessage;
        }
        else if (intent.Domain == "context_only")
        {
            systemPrompt =
                $"You are a financial assistant. Today is {today:yyyy-MM-dd}. Answer in {lang}. {baseRules} " +
                "Use ONLY the pre-loaded financial context below. Do not invent data. " +
                "If the information requested is not in the context, say so in one sentence.\n\n" +
                "Pre-loaded context:\n" + dataOrContext;
            formatterUserMessage = userMessage;
        }
        else
        {
            systemPrompt =
                $"You are a financial assistant. Today is {today:yyyy-MM-dd}. Answer in {lang}. {baseRules} " +
                "Use ONLY the data provided below. Do not invent numbers. " +
                "For amounts, include the currency code next to the number (e.g. 10,000 CRC).";
            formatterUserMessage = $"User question: {userMessage}\n\nFinancial data:\n{dataOrContext}";
        }

        var messages = BuildMessagesList(systemPrompt, history, formatterUserMessage);

        _logger.LogDebug("Response Formatter (stream) — provider: {Provider}", provider.ProviderName);

        // Enumerate the provider stream. Errors during iteration are surfaced as exceptions
        // on MoveNextAsync, so we track whether we emitted anything and fall back if not.
        bool anyChunk = false;

        await foreach (var chunk in provider.StreamMessageAsync(messages, ct).WithCancellation(ct))
        {
            anyChunk = true;
            yield return ChunkEvent(chunk);
        }

        if (!anyChunk)
        {
            _logger.LogWarning("Response Formatter ({Provider}) returned no chunks", provider.ProviderName);
            yield return ChunkEvent("Lo siento, no pude procesar tu consulta en este momento. Intenta de nuevo.");
        }
    }

    // ── Stream event helpers ──────────────────────────────────────────────────

    private static ChatbotStreamEvent ChunkEvent(string content) =>
        new() { Type = "chunk", Content = content };

    private static ChatbotStreamEvent DoneEvent() =>
        new() { Type = "done" };

    private static ChatbotStreamEvent MetaEvent(IChatProvider provider, bool usedFinancialContext, int toolCalls) =>
        new()
        {
            Type                 = "meta",
            Provider             = provider.ProviderName,
            Model                = provider.Model,
            UsedFinancialContext = usedFinancialContext,
            ToolCallsExecuted    = toolCalls
        };

    // ── Provider selector ────────────────────────────────────────────────────

    private static IChatProvider GetProvider(List<IChatProvider> enabled, int startIndex, int offset) =>
        enabled[(startIndex + offset) % enabled.Count];

    // ── Intent Parser ────────────────────────────────────────────────────────

    private ParsedIntent ParseIntent(string response)
    {
        try
        {
            // Extraer el JSON del response (puede venir con texto extra)
            var firstBrace = response.IndexOf('{');
            var lastBrace = response.LastIndexOf('}');

            if (firstBrace < 0 || lastBrace <= firstBrace)
            {
                _logger.LogWarning("Intent Parser no devolvió JSON válido: {R}", response);
                return new ParsedIntent { Domain = "context_only", Action = "none" };
            }

            var json = response[firstBrace..(lastBrace + 1)];
            var intent = JsonSerializer.Deserialize<ParsedIntent>(json, JsonOptions);

            if (intent is null || string.IsNullOrWhiteSpace(intent.Domain))
            {
                _logger.LogWarning("Intent Parser devolvió JSON vacío o sin domain: {R}", response);
                return new ParsedIntent { Domain = "context_only", Action = "none" };
            }

            return intent;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo parsear el intent del LLM: {R}", response);
            return new ParsedIntent { Domain = "context_only", Action = "none" };
        }
    }

    // ── Message building ─────────────────────────────────────────────────────

    private static List<TcMessage> BuildMessagesList(
        string systemPrompt,
        IReadOnlyList<ChatbotHistoryEntry>? history,
        string userMessage)
    {
        var messages = new List<TcMessage> { new TcSystemMessage(systemPrompt) };

        if (history is { Count: > 0 })
        {
            var trimmed = history.Count > MaxHistoryEntries
                ? history.Skip(history.Count - MaxHistoryEntries).ToList()
                : history;

            foreach (var entry in trimmed)
            {
                TcMessage msg = entry.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase)
                    ? new TcAssistantMessage(entry.Content)
                    : new TcUserMessage(entry.Content);

                messages.Add(msg);
            }
        }

        messages.Add(new TcUserMessage(userMessage));
        return messages;
    }

    // ── Parser system prompt ─────────────────────────────────────────────────

    private static string BuildParserSystemPrompt(DateTime today, string contextSummary)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"You are a financial query classifier. Today is {today:yyyy-MM-dd}.");
        sb.AppendLine("Analyze the user's question and output ONLY a valid JSON object. No explanation, no markdown, just JSON.");
        sb.AppendLine();

        sb.AppendLine("## Pre-loaded Financial Context");
        sb.AppendLine(contextSummary);
        sb.AppendLine();

        sb.AppendLine("## Available Domains and Actions");
        sb.AppendLine(IntentSchema.GetAsText());
        sb.AppendLine();

        sb.AppendLine("## Rules");
        sb.AppendLine("- Use domain 'context_only' ONLY when the question asks for the EXACT numbers already visible in the pre-loaded context");
        sb.AppendLine("  (e.g. 'what did I spend this month?' when the overview shows this month's total).");
        sb.AppendLine("- NEVER use 'context_only' for: listing transactions, asking about a specific project by name,");
        sb.AppendLine("  asking for recent/last/latest items, or any question needing data not explicitly in the context above.");
        sb.AppendLine("- NEVER use 'context_only' when the user asks to SEE, LIST or GET individual transactions.");
        sb.AppendLine("  Those always require domain 'movements' with action 'recent'.");
        sb.AppendLine("- If the user is greeting or making small talk, use domain: \"greeting\"");
        sb.AppendLine("- If the question is unrelated to finances, use domain: \"off_topic\"");
        sb.AppendLine("- Extract filters from the user's natural language (project names, dates, categories, etc.)");
        sb.AppendLine("- Convert relative dates to YYYY-MM-DD (e.g. 'this month' -> first/last day of current month)");
        sb.AppendLine("- Convert relative months to YYYY-MM format for the 'month' filter");
        sb.AppendLine("- Only include filters the user explicitly mentions. Omit all others.");
        sb.AppendLine("- Detect the user's language: set \"lang\" to \"es\" for Spanish or \"en\" for English");
        sb.AppendLine("- IMPORTANT: Only include 'from'/'to' when the user mentions a specific time period.");
        sb.AppendLine("  For 'total', 'all time', or general questions without dates, OMIT from/to entirely.");
        sb.AppendLine();

        sb.AppendLine("## Output format");
        sb.AppendLine("{\"domain\":\"...\",\"action\":\"...\",\"filters\":{...},\"lang\":\"es\"}");

        return sb.ToString().TrimEnd();
    }

    // ── Context pre-loading (unchanged) ──────────────────────────────────────

    private async Task<(string Summary, bool UsedFinancialContext)> BuildContextSummaryAsync(
        Guid userId,
        CancellationToken ct)
    {
        var sb = new StringBuilder();
        var usedFinancialContext = false;

        try
        {
            var overview = await _mcpService.GetMonthlyOverviewAsync(
                userId, new McpMonthlyOverviewQuery(), ct);

            sb.AppendLine($"## Current Month Overview ({overview.Month}) — ALL projects combined, no individual transactions");
            sb.AppendLine($"- Currency: {overview.CurrencyCode}");
            sb.AppendLine($"- Total spent: {overview.TotalSpent:F2}");
            sb.AppendLine($"- Total income: {overview.TotalIncome:F2}");
            sb.AppendLine($"- Net balance: {overview.NetBalance:F2}");
            sb.AppendLine($"- Transactions: {overview.ExpenseCount} expenses, {overview.IncomeCount} incomes");

            if (overview.TopCategories.Count > 0)
            {
                sb.AppendLine("- Top expense categories:");
                foreach (var cat in overview.TopCategories)
                    sb.AppendLine($"  * {cat.CategoryName}: {cat.TotalAmount:F2} ({cat.Percentage}%)");
            }

            if (overview.Alerts.Count > 0)
            {
                sb.AppendLine("- Alerts:");
                foreach (var alert in overview.Alerts)
                    sb.AppendLine($"  * [{alert.Type.ToUpper()}] {alert.Message}");
            }

            usedFinancialContext = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo cargar el resumen mensual en el system prompt (userId={UserId})", userId);
        }

        try
        {
            var overdue = await _mcpService.GetOverduePaymentsAsync(
                userId, new McpOverduePaymentsQuery { PageSize = 5 }, ct);

            if (overdue.TotalCount > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"## Overdue Payments ({overdue.TotalCount} total)");
                foreach (var item in overdue.Items)
                    sb.AppendLine($"- {item.Title} | Project: {item.ProjectName} | Due: {item.DueDate} | " +
                                  $"Remaining: {item.RemainingAmount:F2} {item.Currency} | {item.DaysOverdue} days overdue");

                usedFinancialContext = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudieron cargar los pagos vencidos en el system prompt (userId={UserId})", userId);
        }

        return (sb.ToString().TrimEnd(), usedFinancialContext);
    }
}
