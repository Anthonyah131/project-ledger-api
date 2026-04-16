using System.Text;

namespace ProjectLedger.API.Services.Chatbot;

/// <summary>
/// Static catalog of domains, actions, and filters for the Intent Parser LLM.
/// Each description is designed to minimize ambiguity and provide concrete examples
/// so that free/small models can classify reliably without hallucinating tool calls.
/// </summary>
public static class IntentSchema
{
    /// <summary>
    /// Generates the domain/action/filter reference text injected into the Intent Parser's system prompt.
    /// </summary>
    public static string GetAsText()
    {
        var sb = new StringBuilder();

        sb.AppendLine("DOMAINS AND ACTIONS:");
        sb.AppendLine();

        // ── 1. Expenses ──────────────────────────────────────────────────────────
        sb.AppendLine("1. expenses — User asks about spending, costs, what they paid.");
        sb.AppendLine("   Actions:");
        sb.AppendLine("   - totals: Aggregate total spent. Use when user asks 'cuánto gasté', 'total de gastos', 'mis gastos en X periodo'.");
        sb.AppendLine("     Returns: grand total + optional breakdown by top categories.");
        sb.AppendLine("     Filters: projectName, from, to, categoryName, comparePreviousPeriod (bool: 'vs last month')");
        sb.AppendLine();
        sb.AppendLine("   - by_category: Spending grouped by category. Use for 'en qué categorías gasté más', 'desglose por categoría'.");
        sb.AppendLine("     Returns: list of categories with amount + percentage of total.");
        sb.AppendLine("     Filters: projectName, from, to, top (limit N categories), includeOthers (bool), includeTrend (bool)");
        sb.AppendLine();
        sb.AppendLine("   - by_project: Spending grouped by project. Use for 'cuánto gasté por proyecto', 'gastos de cada proyecto'.");
        sb.AppendLine("     Returns: list of projects with their spending totals.");
        sb.AppendLine("     Filters: from, to, top, includeBudgetContext (bool: show budget vs actual)");
        sb.AppendLine();
        sb.AppendLine("   - trends: Spending over time (line chart data). Use for 'evolución de gastos', 'cómo han variado mis gastos', 'tendencia'.");
        sb.AppendLine("     Returns: time series of amounts per period.");
        sb.AppendLine("     Filters: projectName, from, to, granularity[day|week|month], categoryName");
        sb.AppendLine();

        // ── 2. Income ────────────────────────────────────────────────────────────
        sb.AppendLine("2. income — User asks about money received, revenue, earnings, ingresos.");
        sb.AppendLine("   Actions:");
        sb.AppendLine("   - by_period: Income over time. Use for 'cuánto ingresé este mes', 'ingresos por periodo', 'evolución de ingresos'.");
        sb.AppendLine("     Filters: projectName, from, to, granularity[day|week|month], comparePreviousPeriod (bool)");
        sb.AppendLine();
        sb.AppendLine("   - by_project: Income per project. Use for 'ingresos por proyecto', 'qué proyecto genera más ingreso'.");
        sb.AppendLine("     Filters: from, to, top");
        sb.AppendLine();

        // ── 3. Projects ──────────────────────────────────────────────────────────
        sb.AppendLine("3. projects — User asks about their projects, all-time financials, portfolio, deadlines.");
        sb.AppendLine("   Actions:");
        sb.AppendLine("   - portfolio: ALL-TIME financial summary per project (income, expenses, net balance, budget usage, open obligations).");
        sb.AppendLine("     *** USE THIS for: 'balance general de X', 'resumen financiero de X', 'estado de mi proyecto X',");
        sb.AppendLine("         'cuánto he gastado en total en X', 'cómo va el proyecto X', 'mis proyectos'. ***");
        sb.AppendLine("     *** DO NOT use summary/monthly_overview for project balance unless user says a specific month. ***");
        sb.AppendLine("     Filters: projectName (exact name from Visible Projects list), status[active|completed], activityDays");
        sb.AppendLine();
        sb.AppendLine("   - deadlines: Upcoming obligation due dates per project. Use for 'próximos vencimientos', 'deadlines'.");
        sb.AppendLine("     Filters: projectName, from, to, search");
        sb.AppendLine();
        sb.AppendLine("   - activity_split: Count of active vs completed projects. Use for '¿cuántos proyectos activos tengo?'.");
        sb.AppendLine("     Filters: projectName, activityDays");
        sb.AppendLine();

        // ── 4. Payments ──────────────────────────────────────────────────────────
        sb.AppendLine("4. payments — User asks about payment obligations (scheduled payments, received payments).");
        sb.AppendLine("   Actions:");
        sb.AppendLine("   - pending: Obligations not yet fully paid. Use for 'pagos pendientes', '¿qué me falta pagar?'.");
        sb.AppendLine("     Filters: projectName, from (dueAfter), to (dueBefore), minRemainingAmount, search");
        sb.AppendLine();
        sb.AppendLine("   - received: Payments already collected. Use for 'pagos recibidos', 'qué cobré', 'ingresos por cobro'.");
        sb.AppendLine("     Filters: projectName, from, to, paymentMethodName, categoryName, search");
        sb.AppendLine();
        sb.AppendLine("   - overdue: Payments past their due date. Use for 'pagos vencidos', 'cuánto debo', 'qué está atrasado'.");
        sb.AppendLine("     Filters: projectName, overdueDaysMin (minimum days overdue), minRemainingAmount");
        sb.AppendLine();
        sb.AppendLine("   - by_method: Expense/income volume per payment method. Use for 'qué método de pago uso más', 'pagos por tarjeta vs efectivo'.");
        sb.AppendLine("     Filters: projectName, from, to, top");
        sb.AppendLine();

        // ── 5. Obligations ───────────────────────────────────────────────────────
        sb.AppendLine("5. obligations — User asks about financial commitments, debts, amounts owed.");
        sb.AppendLine("   Actions:");
        sb.AppendLine("   - upcoming: Obligations due soon. Use for 'qué vence pronto', 'próximas obligaciones', 'en los próximos N días'.");
        sb.AppendLine("     Filters: projectName, dueWithinDays (N days from today), minRemainingAmount");
        sb.AppendLine();
        sb.AppendLine("   - unpaid: All obligations not fully paid. Use for 'deudas pendientes', 'obligaciones sin pagar', 'qué debo'.");
        sb.AppendLine("     Filters: projectName, status[open|partially_paid|overdue], search");
        sb.AppendLine();

        // ── 6. Partners ──────────────────────────────────────────────────────────
        sb.AppendLine("6. partners — User asks about partners, shared expenses, who owes whom.");
        sb.AppendLine("   Actions:");
        sb.AppendLine("   - balances: Net amount owed to/from each partner. Use for 'balances de partners', 'quién me debe', 'cuánto le debo a X'.");
        sb.AppendLine("     Positive = partner owes user. Negative = user owes partner.");
        sb.AppendLine("     Filters: projectName");
        sb.AppendLine();
        sb.AppendLine("   - settlements: History of payments between partners. Use for 'liquidaciones', 'pagos entre partners', 'historial de cobros'.");
        sb.AppendLine("     Filters: projectName, from, to, partnerName");
        sb.AppendLine();

        // ── 7. Movements ─────────────────────────────────────────────────────────
        sb.AppendLine("7. movements — User wants to SEE individual transactions (a list, not a total).");
        sb.AppendLine("   Actions:");
        sb.AppendLine("   - recent: Chronological list of expenses and/or incomes.");
        sb.AppendLine("     *** USE THIS when user says: 'últimas transacciones', 'muéstrame mis gastos', 'ver movimientos',");
        sb.AppendLine("         'últimos N gastos', 'qué compré esta semana', 'listar ingresos'. ***");
        sb.AppendLine("     *** DO NOT use this for totals — use expenses/totals or income/by_period instead. ***");
        sb.AppendLine("     Filters: projectName, type[expense|income] (omit for both), from, to, categoryName, paymentMethodName, partnerName, search, top (default 10, max 50)");
        sb.AppendLine();

        // ── 8. Summary ───────────────────────────────────────────────────────────
        sb.AppendLine("8. summary — User asks for a high-level overview, health score, or alerts.");
        sb.AppendLine("   Actions:");
        sb.AppendLine("   - financial_health: Score 0–100 with key signals (burn rate, savings rate, overdue count).");
        sb.AppendLine("     Use for: 'salud financiera', 'cómo estoy financieramente', 'puntaje financiero'.");
        sb.AppendLine("     Filters: projectName, from, to");
        sb.AppendLine();
        sb.AppendLine("   - monthly_overview: MONTH-SCOPED summary (income, expenses, net, top categories, alerts).");
        sb.AppendLine("     *** USE THIS ONLY when user mentions a specific month ('este mes', 'enero', 'en abril', 'YYYY-MM'). ***");
        sb.AppendLine("     *** For all-time or project balance WITHOUT a month → use projects/portfolio instead. ***");
        sb.AppendLine("     Filters: month[YYYY-MM] (required), projectName");
        sb.AppendLine();
        sb.AppendLine("   - alerts: Active warnings (budget exceeded, overdue obligations, spending anomalies).");
        sb.AppendLine("     Use for: 'alertas', 'avisos', 'qué debo atender', '¿tengo presupuestos en riesgo?'.");
        sb.AppendLine("     Filters: month[YYYY-MM], projectName, minPriority (1=low, 2=medium, 3=high)");
        sb.AppendLine();

        // ── Special ──────────────────────────────────────────────────────────────
        sb.AppendLine("SPECIAL DOMAINS:");
        sb.AppendLine("- context_only: Answer using ONLY the pre-loaded context (no extra data fetch needed).");
        sb.AppendLine("  Use ONLY when the exact answer is visible in the context above (current month totals, overdue list, project names).");
        sb.AppendLine("- greeting: User says hello, thanks, or makes small talk. No financial data needed.");
        sb.AppendLine("- off_topic: Question has nothing to do with finances or the user's projects.");
        sb.AppendLine();

        // ── Filter reference ─────────────────────────────────────────────────────
        sb.AppendLine("FILTER REFERENCE:");
        sb.AppendLine("- projectName (string)        : exact or partial project name. Match against Visible Projects list.");
        sb.AppendLine("- from / to (YYYY-MM-DD)      : date range. ONLY include when user specifies a period.");
        sb.AppendLine("- month (YYYY-MM)              : use for monthly_overview and alerts, NOT for from/to ranges.");
        sb.AppendLine("- categoryName (string)        : expense/income category (e.g. 'alimentación', 'transporte').");
        sb.AppendLine("- partnerName (string)         : partner name for settlement/split filters.");
        sb.AppendLine("- paymentMethodName (string)   : e.g. 'tarjeta', 'efectivo', 'transferencia'.");
        sb.AppendLine("- top (integer)               : limit results. e.g. 'últimos 5' → top: 5. Default if omitted: backend decides.");
        sb.AppendLine("- type ('expense'|'income')   : transaction type filter for movements. Omit for both.");
        sb.AppendLine("- granularity ('day'|'week'|'month'): time grouping for trend/period queries.");
        sb.AppendLine("- comparePreviousPeriod (bool): true when user says 'vs last month', 'compared to before'.");
        sb.AppendLine("- includeBudgetContext (bool) : true when user asks about budget vs actual.");
        sb.AppendLine("- includeOthers (bool)        : true when user says 'including others' in category breakdown.");
        sb.AppendLine("- includeTrend (bool)         : true when user asks 'trend' or 'how is it changing'.");

        return sb.ToString().TrimEnd();
    }
}
