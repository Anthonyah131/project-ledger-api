using System.Text;

namespace ProjectLedger.API.Services.Chatbot;

/// <summary>
/// Catálogo estático de dominios, acciones y filtros disponibles para el intent parser.
/// Reemplaza a McpToolManifest — en vez de describir 16 tools individuales,
/// presenta ~28 combinaciones domain+action de forma compacta para que modelos
/// gratuitos clasifiquen con mayor fiabilidad.
/// </summary>
public static class IntentSchema
{
    /// <summary>
    /// Genera un texto compacto con los dominios, acciones y filtros disponibles
    /// para incluir en el system prompt del Intent Parser.
    /// </summary>
    public static string GetAsText()
    {
        var sb = new StringBuilder();

        sb.AppendLine("DOMAINS AND ACTIONS:");
        sb.AppendLine();

        sb.AppendLine("1. expenses — User asks about spending, costs, expenditures");
        sb.AppendLine("   Actions:");
        sb.AppendLine("   - totals: total amount spent (filters: projectName, from, to, categoryName, comparePreviousPeriod)");
        sb.AppendLine("   - by_category: spending breakdown by category (filters: projectName, from, to, top, includeOthers, includeTrend)");
        sb.AppendLine("   - by_project: spending per project (filters: from, to, top, includeBudgetContext)");
        sb.AppendLine("   - trends: spending over time (filters: projectName, from, to, granularity[day|week|month], categoryName)");
        sb.AppendLine();

        sb.AppendLine("2. income — User asks about revenue, earnings, money received");
        sb.AppendLine("   Actions:");
        sb.AppendLine("   - by_period: income over time (filters: projectName, from, to, granularity, comparePreviousPeriod)");
        sb.AppendLine("   - by_project: income per project (filters: from, to, top)");
        sb.AppendLine();

        sb.AppendLine("3. projects — User asks about their projects, portfolio, deadlines");
        sb.AppendLine("   Actions:");
        sb.AppendLine("   - portfolio: list projects with status and budget (filters: status[active|completed], activityDays)");
        sb.AppendLine("   - deadlines: project-related deadlines (filters: projectName, from, to, search)");
        sb.AppendLine("   - activity_split: active vs completed project breakdown (filters: projectName, activityDays)");
        sb.AppendLine();

        sb.AppendLine("4. payments — User asks about payments made or due");
        sb.AppendLine("   Actions:");
        sb.AppendLine("   - pending: upcoming payments not yet fully paid (filters: projectName, from, to, minRemainingAmount, search)");
        sb.AppendLine("   - received: payments already received (filters: projectName, from, to, paymentMethodName, categoryName, search)");
        sb.AppendLine("   - overdue: past-due payments (filters: projectName, overdueDaysMin, minRemainingAmount)");
        sb.AppendLine("   - by_method: payment method usage breakdown (filters: projectName, from, to, top)");
        sb.AppendLine();

        sb.AppendLine("5. obligations — User asks about debts, commitments, amounts owed");
        sb.AppendLine("   Actions:");
        sb.AppendLine("   - upcoming: obligations due soon (filters: projectName, dueWithinDays, minRemainingAmount)");
        sb.AppendLine("   - unpaid: all not-fully-paid obligations (filters: projectName, status[open|partially_paid|overdue], search)");
        sb.AppendLine();

        sb.AppendLine("6. partners — User asks about partners, shared expenses, who owes whom");
        sb.AppendLine("   Actions:");
        sb.AppendLine("   - balances: net balance per partner (filters: projectName)");
        sb.AppendLine("   - settlements: payment history between partners (filters: projectName, from, to, partnerName)");
        sb.AppendLine();

        sb.AppendLine("7. movements — User asks for a list of individual transactions (expenses/incomes), e.g. 'last 5 expenses', 'movements this week'");
        sb.AppendLine("   Actions:");
        sb.AppendLine("   - recent: list of recent transactions sorted by date (filters: projectName, type[expense|income], from, to, categoryName, paymentMethodName, partnerName, search, top)");
        sb.AppendLine("   NOTE: use 'movements/recent' when the user wants to SEE individual transactions. Use 'expenses/totals' only when they ask for a TOTAL or SUMMARY.");
        sb.AppendLine();

        sb.AppendLine("8. summary — User asks for overviews, health scores, alerts");
        sb.AppendLine("   Actions:");
        sb.AppendLine("   - financial_health: health score 0-100 with signals (filters: projectName, from, to)");
        sb.AppendLine("   - monthly_overview: full month summary (filters: month[YYYY-MM], projectName)");
        sb.AppendLine("   - alerts: financial warnings and alerts (filters: month[YYYY-MM], projectName, minPriority)");
        sb.AppendLine();

        sb.AppendLine("SPECIAL DOMAINS (no filters needed):");
        sb.AppendLine("- context_only: The question can be answered using the pre-loaded financial context above.");
        sb.AppendLine("- greeting: User is greeting or making small talk.");
        sb.AppendLine("- off_topic: Question is unrelated to finances.");
        sb.AppendLine();

        sb.AppendLine("COMMON FILTERS:");
        sb.AppendLine("- projectName (string): project name or partial name");
        sb.AppendLine("- from/to (string): date range in YYYY-MM-DD format. Convert relative dates (e.g. 'this month' -> first/last day).");
        sb.AppendLine("- month (string): month in YYYY-MM format");
        sb.AppendLine("- categoryName (string): expense/income category name");
        sb.AppendLine("- partnerName (string): partner name");
        sb.AppendLine("- paymentMethodName (string): payment method name (e.g. 'tarjeta', 'efectivo')");
        sb.AppendLine("- top (integer): limit number of results (e.g. 'last 5' -> top: 5)");
        sb.AppendLine("- type (string): 'expense' or 'income' — use when the user specifies the transaction type");
        sb.AppendLine("- granularity (string): 'day', 'week', or 'month'");
        sb.AppendLine("- Only include filters the user explicitly mentions. Omit filters not mentioned.");

        return sb.ToString().TrimEnd();
    }
}
