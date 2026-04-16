using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ProjectLedger.API.DTOs.Chatbot;
using ProjectLedger.API.DTOs.Mcp;
using ProjectLedger.API.Services.Chatbot.Interfaces;

namespace ProjectLedger.API.Services.Chatbot;

/// <summary>
/// Orchestrates the intent-based chatbot pipeline:
/// 1. LLM Call #1 (Intent Parser): classifies the user's query into domain/action/filters as structured JSON.
/// 2. IntentRouter: maps the parsed intent to an <see cref="IMcpService"/> method and fetches live financial data.
/// 3. LLM Call #2 (Response Formatter, streaming): generates a natural language response from the retrieved data.
///
/// Short-circuit path: greeting/context_only/off_topic domains skip the router (no live data fetch).
/// Uses round-robin provider rotation across up to 2 providers per request.
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

            _logger.LogDebug("Intent Parser raw response: {R}", parserResponse);
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
            yield return ChunkEvent("I'm sorry, I couldn't process your request at this time. Please try again.");
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
            formatterHistory         = history; // Pass history so the formatter can contextualize follow-up questions
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

        // Base rules for simple/conversational responses (greetings, context questions)
        var conversationalRules =
            "Be concise and direct. Answer in 1-3 sentences. " +
            "Do not add extra explanations or suggestions unless the user asked. " +
            "Do not use markdown formatting (no **, no *, no #, no ---). " +
            "Write plain conversational text.";

        // Rules for structured financial data responses
        var financialRules =
            "Do not use markdown formatting (no **, no *, no #, no ---). " +
            "Do not add unsolicited suggestions or advice. " +
            "Always use the EXACT currency code from the data (e.g. 10,000 CRC, 500 USD). Never assume a currency. " +
            "For a single value, answer in 1-2 sentences. " +
            "For multiple items (several projects, categories, periods), present each on its own line using plain text. " +
            "Lead with the direct answer, then list the breakdown. " +
            "If the data contains no records, say so clearly in one sentence. " +
            "If the previous conversation provides context (period, project, category), use it to frame your answer.";

        string systemPrompt;
        string formatterUserMessage;

        if (intent.Domain is "greeting" or "off_topic")
        {
            systemPrompt =
                $"You are a financial assistant. Today is {today:yyyy-MM-dd}. Answer in {lang}. {conversationalRules} " +
                "If the user greets you, greet them back briefly. " +
                "If the question is off-topic, say in one sentence that you only help with finances.";
            formatterUserMessage = userMessage;
        }
        else if (intent.Domain == "context_only")
        {
            systemPrompt =
                $"You are a financial assistant. Today is {today:yyyy-MM-dd}. Answer in {lang}. {conversationalRules} " +
                "Use ONLY the pre-loaded financial context below. Do not invent data. " +
                "If the information requested is not in the context, say so in one sentence.\n\n" +
                "Pre-loaded context:\n" + dataOrContext;
            formatterUserMessage = userMessage;
        }
        else
        {
            systemPrompt =
                $"You are a financial assistant. Today is {today:yyyy-MM-dd}. Answer in {lang}. {financialRules} " +
                "Use ONLY the financial data provided below. Do not invent numbers or dates.";
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
            yield return ChunkEvent("I'm sorry, I couldn't process your request at this time. Please try again.");
        }
    }

    // ── Stream event helpers ──────────────────────────────────────────────────

    /// <summary>Creates a stream chunk event containing response text.</summary>
    private static ChatbotStreamEvent ChunkEvent(string content) =>
        new() { Type = "chunk", Content = content };

    /// <summary>Creates the final 'done' event to signal stream completion.</summary>
    private static ChatbotStreamEvent DoneEvent() =>
        new() { Type = "done" };

    /// <summary>Creates a metadata event with pipeline execution stats.</summary>
    private static ChatbotStreamEvent MetaEvent(IChatProvider provider, bool usedFinancialContext, int toolCalls) =>
        new()
        {
            Type                 = "meta",
            UsedFinancialContext = usedFinancialContext,
            ToolCallsExecuted    = toolCalls
        };

    // ── Provider selector ────────────────────────────────────────────────────

    /// <summary>Selects a provider from the enabled list using the rotated start index and offset.</summary>
    private static IChatProvider GetProvider(List<IChatProvider> enabled, int startIndex, int offset) =>
        enabled[(startIndex + offset) % enabled.Count];

    // ── Intent Parser ────────────────────────────────────────────────────────

    /// <summary>
    /// Attempts to parse the LLM's raw response string into a structured ParsedIntent JSON object.
    /// Defaults to context_only/none upon parsing failure.
    /// </summary>
    private ParsedIntent ParseIntent(string response)
    {
        try
        {
            // Extract JSON from the response (may include extraneous text)
            var firstBrace = response.IndexOf('{');
            var lastBrace = response.LastIndexOf('}');

            if (firstBrace < 0 || lastBrace <= firstBrace)
            {
                _logger.LogWarning("Intent Parser did not return valid JSON: {R}", response);
                return new ParsedIntent { Domain = "context_only", Action = "none" };
            }

            var json = response[firstBrace..(lastBrace + 1)];
            var intent = JsonSerializer.Deserialize<ParsedIntent>(json, JsonOptions);

            if (intent is null || string.IsNullOrWhiteSpace(intent.Domain))
            {
                _logger.LogWarning("Intent Parser returned empty JSON or missing domain: {R}", response);
                return new ParsedIntent { Domain = "context_only", Action = "none" };
            }

            return intent;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not parse the LLM intent: {R}", response);
            return new ParsedIntent { Domain = "context_only", Action = "none" };
        }
    }

    // ── Message building ─────────────────────────────────────────────────────

    /// <summary>Constructs the list of conversation messages linking the system prompt, history, and the new user message.</summary>
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

    /// <summary>Builds the comprehensive system prompt for the intent parser LLM call.</summary>
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
        sb.AppendLine("- Only include filters the user explicitly mentions. Omit all others.");
        sb.AppendLine("- Detect the user's language: set \"lang\" to \"es\" for Spanish or \"en\" for English");
        sb.AppendLine("- PROJECT NAMES: Match project names from 'Visible Projects' in the context. Use the exact name listed.");
        sb.AppendLine("  If the user mentions a partial name (e.g. 'miravalles'), match it to the full project name ('Miravalles').");
        sb.AppendLine();
        sb.AppendLine("## Date conversion rules (today = " + today.ToString("yyyy-MM-dd") + ")");
        sb.AppendLine("Always convert relative expressions to absolute YYYY-MM-DD dates:");
        sb.AppendLine($"- 'this month'      → from: {new DateTime(today.Year, today.Month, 1):yyyy-MM-dd}, to: {today:yyyy-MM-dd}");
        sb.AppendLine($"- 'last month'      → from: {new DateTime(today.Year, today.Month, 1).AddMonths(-1):yyyy-MM-dd}, to: {new DateTime(today.Year, today.Month, 1).AddDays(-1):yyyy-MM-dd}");
        sb.AppendLine($"- 'this year' / 'YTD' → from: {new DateTime(today.Year, 1, 1):yyyy-MM-dd}, to: {today:yyyy-MM-dd}");
        sb.AppendLine($"- 'last year'       → from: {new DateTime(today.Year - 1, 1, 1):yyyy-MM-dd}, to: {new DateTime(today.Year - 1, 12, 31):yyyy-MM-dd}");
        sb.AppendLine($"- 'last 7 days' / 'last week' → from: {today.AddDays(-7):yyyy-MM-dd}, to: {today:yyyy-MM-dd}");
        sb.AppendLine($"- 'last 30 days'    → from: {today.AddDays(-30):yyyy-MM-dd}, to: {today:yyyy-MM-dd}");
        sb.AppendLine($"- 'last 3 months'   → from: {today.AddMonths(-3):yyyy-MM-dd}, to: {today:yyyy-MM-dd}");
        sb.AppendLine($"- 'last 6 months'   → from: {today.AddMonths(-6):yyyy-MM-dd}, to: {today:yyyy-MM-dd}");
        sb.AppendLine("- 'today'           → from: today, to: today");
        sb.AppendLine("- 'yesterday'       → from: yesterday, to: yesterday");
        sb.AppendLine("- 'YYYY-MM' month format → use the 'month' filter, not from/to");
        sb.AppendLine("- ONLY include 'from'/'to' when a time period is explicitly mentioned. For 'total', 'all time', or general questions, OMIT them.");
        sb.AppendLine();
        sb.AppendLine("## Intent disambiguation");
        sb.AppendLine("BALANCE / RESUMEN:");
        sb.AppendLine("  'balance general', 'resumen de proyecto X', 'cuánto he gastado en total en X', 'estado de mi proyecto X'");
        sb.AppendLine("  → domain: 'projects', action: 'portfolio', filters: { projectName: 'X' }   [ALL-TIME totals]");
        sb.AppendLine("  'balance de este mes', 'cómo voy en enero', 'resumen de marzo'");
        sb.AppendLine("  → domain: 'summary', action: 'monthly_overview', filters: { month: 'YYYY-MM' }   [month-scoped]");
        sb.AppendLine("TOTALS vs LISTING:");
        sb.AppendLine("  'cuánto gasté' / 'total de gastos' / 'cuánto fue'  → expenses/totals or summary");
        sb.AppendLine("  'ver mis gastos' / 'últimas transacciones' / 'listar movimientos'  → movements/recent");
        sb.AppendLine("COMPARISON:");
        sb.AppendLine("  'comparado con el mes anterior' / 'vs last month' → set comparePreviousPeriod: true");
        sb.AppendLine("FOLLOW-UP QUESTIONS:");
        sb.AppendLine("  If the previous assistant message answered a question about period/project/category,");
        sb.AppendLine("  and the user says 'y para X?' or 'what about X?' — keep all other filters from the previous intent");
        sb.AppendLine();

        sb.AppendLine("## Output format");
        sb.AppendLine("{\"domain\":\"...\",\"action\":\"...\",\"filters\":{...},\"lang\":\"es\"}");

        return sb.ToString().TrimEnd();
    }

    // ── Context pre-loading (unchanged) ──────────────────────────────────────

    /// <summary>
    /// Builds the financial context injected into the Intent Parser's system prompt.
    /// Loads: project list (names + currencies), current month overview, and overdue payments.
    /// Having project names in context allows the parser to detect project name filters
    /// and correctly route project-specific questions.
    /// </summary>
    private async Task<(string Summary, bool UsedFinancialContext)> BuildContextSummaryAsync(
        Guid userId,
        CancellationToken ct)
    {
        var sb = new StringBuilder();
        var usedFinancialContext = false;

        // ── Project list (names + currencies) ──────────────────────────────────
        // Gives the parser awareness of available project names and their currencies.
        try
        {
            var ctx = await _mcpService.GetContextAsync(userId, ct);

            if (ctx.VisibleProjects.Count > 0)
            {
                sb.AppendLine("## Visible Projects (use exact names for projectName filter)");
                foreach (var p in ctx.VisibleProjects)
                    sb.AppendLine($"- \"{p.ProjectName}\" (currency: {p.CurrencyCode}, role: {p.UserRole})");
                sb.AppendLine();
                usedFinancialContext = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load project list into context (userId={UserId})", userId);
        }

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
            _logger.LogWarning(ex, "Could not load the monthly overview into the system prompt (userId={UserId})", userId);
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
            _logger.LogWarning(ex, "Could not load overdue payments into the system prompt (userId={UserId})", userId);
        }

        return (sb.ToString().TrimEnd(), usedFinancialContext);
    }
}
