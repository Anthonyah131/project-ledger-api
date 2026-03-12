using System.Globalization;
using ProjectLedger.API.DTOs.Mcp;

namespace ProjectLedger.API.Services;

public partial class McpService
{
    public async Task<McpFinancialHealthResponse> GetFinancialHealthAsync(
        Guid userId,
        McpFinancialHealthQuery query,
        CancellationToken ct = default)
    {
        EnsureValidDateRange(query.From, query.To);

        var (from, to) = ResolveRangeOrDefaults(query.From, query.To, "month");
        var scope = await ResolveScopeAsync(userId, query.ProjectId, query.ProjectName, ct);

        var expenses = await LoadExpensesAsync(scope.SelectedProjects, from, to, ct);
        var incomes = await LoadIncomesAsync(scope.SelectedProjects, from, to, ct);
        var obligations = await LoadObligationsWithPaymentsAsync(scope.SelectedProjects, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var totalSpent = expenses.Sum(e => e.ExpConvertedAmount);
        var totalIncome = incomes.Sum(i => i.IncConvertedAmount);
        var net = totalIncome - totalSpent;

        var days = from.HasValue && to.HasValue
            ? Math.Max(1, to.Value.DayNumber - from.Value.DayNumber + 1)
            : 30;

        var burnRate = Math.Round(totalSpent / days, 2);

        var overdueCount = obligations
            .Select(o => ComputeObligationStatus(o, ComputePaidAmount(o), today))
            .Count(status => status == "overdue");

        var budgetRiskProjects = 0;
        foreach (var project in scope.SelectedProjects)
        {
            var budget = await _budgetRepo.GetActiveByProjectIdAsync(project.PrjId, ct);
            if (budget is null || budget.PjbTotalBudget <= 0)
                continue;

            var spent = expenses
                .Where(e => e.ExpProjectId == project.PrjId)
                .Sum(e => e.ExpConvertedAmount);

            var used = spent / budget.PjbTotalBudget * 100m;
            if (used >= 80m)
                budgetRiskProjects++;
        }

        var score = 50;
        score += net >= 0 ? 20 : -20;
        score += overdueCount == 0 ? 10 : -Math.Min(20, overdueCount * 5);
        score += budgetRiskProjects == 0 ? 10 : -Math.Min(20, budgetRiskProjects * 5);
        score += totalIncome > 0 && totalSpent <= totalIncome ? 10 : -10;
        score = Math.Clamp(score, 0, 100);

        var signals = new List<string>();
        if (net < 0) signals.Add("Net balance is negative in the selected period.");
        if (overdueCount > 0) signals.Add($"There are {overdueCount} overdue obligations.");
        if (budgetRiskProjects > 0) signals.Add($"{budgetRiskProjects} projects are above 80% of budget.");
        if (signals.Count == 0) signals.Add("Financial indicators are stable for the selected period.");

        return new McpFinancialHealthResponse
        {
            ProjectId = query.ProjectId,
            From = from,
            To = to,
            Score = score,
            TotalIncome = totalIncome,
            TotalSpent = totalSpent,
            NetBalance = net,
            BurnRatePerDay = burnRate,
            BudgetRiskProjects = budgetRiskProjects,
            OverdueObligationsCount = overdueCount,
            SearchNote = scope.SearchNote,
            KeySignals = signals
        };
    }

    public async Task<McpMonthlyOverviewResponse> GetMonthlyOverviewAsync(
        Guid userId,
        McpMonthlyOverviewQuery query,
        CancellationToken ct = default)
    {
        var monthStart = ParseMonthOrDefault(query.Month);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var scope = await ResolveScopeAsync(userId, query.ProjectId, query.ProjectName, ct);
        var expenses = await LoadExpensesAsync(scope.SelectedProjects, monthStart, monthEnd, ct);
        var incomes = await LoadIncomesAsync(scope.SelectedProjects, monthStart, monthEnd, ct);

        var totalSpent = expenses.Sum(e => e.ExpConvertedAmount);
        var totalIncome = incomes.Sum(i => i.IncConvertedAmount);

        var topCategories = expenses
            .GroupBy(e => new { e.ExpCategoryId, Name = e.Category?.CatName ?? "Unknown" })
            .Select(g => new McpExpenseByCategoryItemResponse
            {
                CategoryId = g.Key.ExpCategoryId,
                CategoryName = g.Key.Name,
                TotalAmount = g.Sum(x => x.ExpConvertedAmount),
                ExpenseCount = g.Count(),
                Percentage = totalSpent > 0
                    ? Math.Round(g.Sum(x => x.ExpConvertedAmount) / totalSpent * 100m, 2)
                    : 0m
            })
            .OrderByDescending(c => c.TotalAmount)
            .Take(5)
            .ToList();

        var paymentMethodMap = await BuildPaymentMethodMapAsync(
            expenses.Select(e => e.ExpPaymentMethodId)
                .Union(incomes.Select(i => i.IncPaymentMethodId)),
            ct);

        var paymentSplit = expenses
            .GroupBy(e => e.ExpPaymentMethodId)
            .Select(g =>
            {
                var outgoing = g.Sum(x => x.ExpConvertedAmount);
                var incoming = incomes
                    .Where(i => i.IncPaymentMethodId == g.Key)
                    .Sum(i => i.IncConvertedAmount);

                return new McpPaymentMethodUsageItemResponse
                {
                    PaymentMethodId = g.Key,
                    PaymentMethodName = paymentMethodMap.GetValueOrDefault(g.Key)?.PmtName ?? "Unknown",
                    PaymentMethodType = paymentMethodMap.GetValueOrDefault(g.Key)?.PmtType ?? "unknown",
                    TotalOutgoing = outgoing,
                    TotalIncoming = incoming,
                    NetFlow = incoming - outgoing,
                    ExpenseCount = g.Count(),
                    IncomeCount = incomes.Count(i => i.IncPaymentMethodId == g.Key),
                    UsagePercentage = totalSpent > 0
                        ? Math.Round(outgoing / totalSpent * 100m, 2)
                        : 0m
                };
            })
            .OrderByDescending(x => x.TotalOutgoing)
            .ToList();

        var projectHealth = new List<McpProjectHealthItemResponse>();
        foreach (var project in scope.SelectedProjects)
        {
            var spent = expenses.Where(e => e.ExpProjectId == project.PrjId).Sum(e => e.ExpConvertedAmount);
            var income = incomes.Where(i => i.IncProjectId == project.PrjId).Sum(i => i.IncConvertedAmount);
            var budget = await _budgetRepo.GetActiveByProjectIdAsync(project.PrjId, ct);

            projectHealth.Add(new McpProjectHealthItemResponse
            {
                ProjectId = project.PrjId,
                ProjectName = project.PrjName,
                Spent = spent,
                Income = income,
                Net = income - spent,
                Budget = budget?.PjbTotalBudget,
                BudgetUsedPercentage = budget is not null && budget.PjbTotalBudget > 0
                    ? Math.Round(spent / budget.PjbTotalBudget * 100m, 2)
                    : null
            });
        }

        var alerts = await BuildAlertsAsync(scope.SelectedProjects, monthStart, monthEnd, 0, ct);

        return new McpMonthlyOverviewResponse
        {
            Month = monthStart.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            ProjectId = query.ProjectId,
            CurrencyCode = ResolveCurrencyCode(scope.SelectedProjects),
            GeneratedAtUtc = DateTime.UtcNow,
            TotalSpent = totalSpent,
            TotalIncome = totalIncome,
            NetBalance = totalIncome - totalSpent,
            ExpenseCount = expenses.Count,
            IncomeCount = incomes.Count,
            SearchNote = scope.SearchNote,
            TopCategories = topCategories,
            PaymentMethodSplit = paymentSplit,
            ProjectHealth = projectHealth.OrderByDescending(p => p.Spent).ToList(),
            Alerts = alerts
        };
    }

    public async Task<McpAlertsResponse> GetAlertsAsync(
        Guid userId,
        McpAlertsQuery query,
        CancellationToken ct = default)
    {
        var minPriority = query.MinPriority ?? 0;
        DateOnly? from = null;
        DateOnly? to = null;

        if (!string.IsNullOrWhiteSpace(query.Month))
        {
            var monthStart = ParseMonthOrDefault(query.Month);
            from = monthStart;
            to = monthStart.AddMonths(1).AddDays(-1);
        }

        var scope = await ResolveScopeAsync(userId, query.ProjectId, query.ProjectName, ct);
        var items = await BuildAlertsAsync(scope.SelectedProjects, from, to, minPriority, ct);

        return new McpAlertsResponse
        {
            Month = query.Month,
            ProjectId = query.ProjectId,
            GeneratedAtUtc = DateTime.UtcNow,
            SearchNote = scope.SearchNote,
            Items = items
        };
    }

    private async Task<List<McpAlertResponse>> BuildAlertsAsync(
        IReadOnlyCollection<ProjectLedger.API.Models.Project> selectedProjects,
        DateOnly? from,
        DateOnly? to,
        int minPriority,
        CancellationToken ct)
    {
        var rangeFrom = from ?? new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var rangeTo = to ?? rangeFrom.AddMonths(1).AddDays(-1);

        var expenses = await LoadExpensesAsync(selectedProjects, rangeFrom, rangeTo, ct);
        var incomes = await LoadIncomesAsync(selectedProjects, rangeFrom, rangeTo, ct);
        var obligations = await LoadObligationsWithPaymentsAsync(selectedProjects, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var alerts = new List<McpAlertResponse>();

        foreach (var project in selectedProjects)
        {
            var budget = await _budgetRepo.GetActiveByProjectIdAsync(project.PrjId, ct);
            if (budget is not null && budget.PjbTotalBudget > 0)
            {
                var spent = expenses.Where(e => e.ExpProjectId == project.PrjId).Sum(e => e.ExpConvertedAmount);
                var used = spent / budget.PjbTotalBudget * 100m;
                if (used >= 80m)
                {
                    alerts.Add(new McpAlertResponse
                    {
                        Code = "BUDGET_OVER_80",
                        Type = "warning",
                        Message = $"Project '{project.PrjName}' is at {Math.Round(used, 2)}% of budget.",
                        Priority = used >= 100m ? 95 : 80,
                        ProjectId = project.PrjId
                    });
                }
            }
        }

        var overdueRows = obligations
            .Select(o =>
            {
                var paid = ComputePaidAmount(o);
                var status = ComputeObligationStatus(o, paid, today);
                var remaining = Math.Max(0m, o.OblTotalAmount - paid);
                return new { Obligation = o, Status = status, Remaining = remaining };
            })
            .Where(x => x.Status == "overdue" && x.Remaining > 0)
            .ToList();

        if (overdueRows.Count > 0)
        {
            var top = overdueRows
                .GroupBy(x => x.Obligation.OblProjectId)
                .OrderByDescending(g => g.Count())
                .First();

            alerts.Add(new McpAlertResponse
            {
                Code = "OVERDUE_OBLIGATIONS",
                Type = "warning",
                Message = $"There are {overdueRows.Count} overdue obligations.",
                Priority = 90,
                ProjectId = top.Key
            });
        }

        var incomeTotal = incomes.Sum(i => i.IncConvertedAmount);
        var expenseTotal = expenses.Sum(e => e.ExpConvertedAmount);
        if (expenseTotal > incomeTotal)
        {
            alerts.Add(new McpAlertResponse
            {
                Code = "NEGATIVE_NET",
                Type = "info",
                Message = "Net balance is negative for the selected period.",
                Priority = 60
            });
        }

        return alerts
            .Where(a => a.Priority >= minPriority)
            .OrderByDescending(a => a.Priority)
            .ToList();
    }
}
