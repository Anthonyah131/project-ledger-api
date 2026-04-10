using System.Text.Json;
using Microsoft.Extensions.Logging;
using ProjectLedger.API.DTOs.Chatbot;
using ProjectLedger.API.DTOs.Mcp;
using ProjectLedger.API.Services.Chatbot.Interfaces;

namespace ProjectLedger.API.Services.Chatbot;

/// <summary>
/// Maps a <see cref="ParsedIntent"/> (domain + action + filters) to <see cref="IMcpService"/> calls.
/// Replaces the ExecuteToolAsync switch and the tool selection parsing from the previous pipeline.
/// </summary>
public class IntentRouter : IIntentRouter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IMcpService            _mcpService;
    private readonly ILogger<IntentRouter>  _logger;

    public IntentRouter(IMcpService mcpService, ILogger<IntentRouter> logger)
    {
        _mcpService = mcpService;
        _logger     = logger;
    }

    public async Task<string> ExecuteAsync(Guid userId, ParsedIntent intent, CancellationToken ct)
    {
        var f = intent.Filters;
        var key = $"{intent.Domain}/{intent.Action}";

        _logger.LogDebug("IntentRouter executing: {Key}", key);

        try
        {
            object? result = key switch
            {
                // ── Expenses ──────────────────────────────────────────────
                "expenses/totals" => await _mcpService.GetExpenseTotalsAsync(userId, new McpExpenseTotalsQuery
                {
                    ProjectName           = f.ProjectName,
                    From                  = ParseDate(f.From),
                    To                    = ParseDate(f.To),
                    CategoryName          = f.CategoryName,
                    ComparePreviousPeriod = f.ComparePreviousPeriod,
                    IncludeTopCategories  = true
                }, ct),

                "expenses/by_category" => await _mcpService.GetExpenseByCategoryAsync(userId, new McpExpenseByCategoryQuery
                {
                    ProjectName   = f.ProjectName,
                    From          = ParseDate(f.From),
                    To            = ParseDate(f.To),
                    Top           = f.Top,
                    IncludeOthers = f.IncludeOthers,
                    IncludeTrend  = f.IncludeTrend
                }, ct),

                "expenses/by_project" => await _mcpService.GetExpenseByProjectAsync(userId, new McpExpenseByProjectQuery
                {
                    From                 = ParseDate(f.From),
                    To                   = ParseDate(f.To),
                    Top                  = f.Top,
                    IncludeBudgetContext = f.IncludeBudgetContext
                }, ct),

                "expenses/trends" => await _mcpService.GetExpenseTrendsAsync(userId, new McpExpenseTrendsQuery
                {
                    ProjectName  = f.ProjectName,
                    From         = ParseDate(f.From),
                    To           = ParseDate(f.To),
                    Granularity  = f.Granularity,
                    CategoryName = f.CategoryName
                }, ct),

                // ── Income ────────────────────────────────────────────────
                "income/by_period" => await _mcpService.GetIncomeByPeriodAsync(userId, new McpIncomeByPeriodQuery
                {
                    ProjectName           = f.ProjectName,
                    From                  = ParseDate(f.From),
                    To                    = ParseDate(f.To),
                    Granularity           = f.Granularity,
                    ComparePreviousPeriod = f.ComparePreviousPeriod
                }, ct),

                "income/by_project" => await _mcpService.GetIncomeByProjectAsync(userId, new McpIncomeByProjectQuery
                {
                    From = ParseDate(f.From),
                    To   = ParseDate(f.To),
                    Top  = f.Top
                }, ct),

                // ── Projects ──────────────────────────────────────────────
                "projects/portfolio" => await _mcpService.GetProjectPortfolioAsync(userId, new McpProjectPortfolioQuery
                {
                    Status       = f.Status,
                    ActivityDays = f.ActivityDays
                }, ct),

                "projects/deadlines" => await _mcpService.GetProjectDeadlinesAsync(userId, new McpProjectDeadlinesQuery
                {
                    ProjectName  = f.ProjectName,
                    DueFrom      = ParseDate(f.From),
                    DueTo        = ParseDate(f.To),
                    Search       = f.Search
                }, ct),

                "projects/activity_split" => await _mcpService.GetProjectActivitySplitAsync(userId, new McpProjectActivitySplitQuery
                {
                    ProjectName  = f.ProjectName,
                    ActivityDays = f.ActivityDays
                }, ct),

                // ── Payments ──────────────────────────────────────────────
                "payments/pending" => await _mcpService.GetPendingPaymentsAsync(userId, new McpPendingPaymentsQuery
                {
                    ProjectName        = f.ProjectName,
                    DueBefore          = ParseDate(f.To),
                    DueAfter           = ParseDate(f.From),
                    MinRemainingAmount = f.MinRemainingAmount,
                    Search             = f.Search
                }, ct),

                "payments/received" => await _mcpService.GetReceivedPaymentsAsync(userId, new McpReceivedPaymentsQuery
                {
                    ProjectName       = f.ProjectName,
                    From              = ParseDate(f.From),
                    To                = ParseDate(f.To),
                    PaymentMethodName = f.PaymentMethodName,
                    CategoryName      = f.CategoryName,
                    Search            = f.Search
                }, ct),

                "payments/overdue" => await _mcpService.GetOverduePaymentsAsync(userId, new McpOverduePaymentsQuery
                {
                    ProjectName        = f.ProjectName,
                    OverdueDaysMin     = f.OverdueDaysMin,
                    MinRemainingAmount = f.MinRemainingAmount
                }, ct),

                "payments/by_method" => await _mcpService.GetPaymentMethodUsageAsync(userId, new McpPaymentMethodUsageQuery
                {
                    ProjectName = f.ProjectName,
                    From        = ParseDate(f.From),
                    To          = ParseDate(f.To),
                    Top         = f.Top
                }, ct),

                // ── Obligations ───────────────────────────────────────────
                "obligations/upcoming" => await _mcpService.GetUpcomingObligationsAsync(userId, new McpUpcomingObligationsQuery
                {
                    ProjectName        = f.ProjectName,
                    DueWithinDays      = f.DueWithinDays,
                    MinRemainingAmount = f.MinRemainingAmount
                }, ct),

                "obligations/unpaid" => await _mcpService.GetUnpaidObligationsAsync(userId, new McpUnpaidObligationsQuery
                {
                    ProjectName = f.ProjectName,
                    Status      = f.Status,
                    Search      = f.Search
                }, ct),

                // ── Partners ──────────────────────────────────────────────
                "partners/balances" => await _mcpService.GetPartnerBalancesAsync(userId, new McpPartnerBalancesQuery
                {
                    ProjectName = f.ProjectName
                }, ct),

                "partners/settlements" => await _mcpService.GetPartnerSettlementsAsync(userId, new McpPartnerSettlementsQuery
                {
                    ProjectName = f.ProjectName,
                    From        = ParseDate(f.From),
                    To          = ParseDate(f.To),
                    PartnerName = f.PartnerName
                }, ct),

                // ── Movements ─────────────────────────────────────────────
                "movements/recent" => await _mcpService.GetRecentMovementsAsync(userId, new McpRecentMovementsQuery
                {
                    ProjectName       = f.ProjectName,
                    Type              = f.Type,
                    From              = ParseDate(f.From),
                    To                = ParseDate(f.To),
                    CategoryName      = f.CategoryName,
                    PaymentMethodName = f.PaymentMethodName,
                    PartnerName       = f.PartnerName,
                    Search            = f.Search,
                    Top               = f.Top
                }, ct),

                // ── Summary ───────────────────────────────────────────────
                "summary/financial_health" => await _mcpService.GetFinancialHealthAsync(userId, new McpFinancialHealthQuery
                {
                    ProjectName = f.ProjectName,
                    From        = ParseDate(f.From),
                    To          = ParseDate(f.To)
                }, ct),

                "summary/monthly_overview" => await _mcpService.GetMonthlyOverviewAsync(userId, new McpMonthlyOverviewQuery
                {
                    Month       = f.Month,
                    ProjectName = f.ProjectName
                }, ct),

                "summary/alerts" => await _mcpService.GetAlertsAsync(userId, new McpAlertsQuery
                {
                    Month       = f.Month,
                    ProjectName = f.ProjectName,
                    MinPriority = f.MinPriority
                }, ct),

                _ => new { error = $"Unknown intent: {key}" }
            };

            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Error executing intent {Key}", key);
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    private static DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateOnly.TryParse(value, out var d) ? d : null;
    }
}
