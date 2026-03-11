using System.Globalization;
using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.DTOs.Mcp;
using ProjectLedger.API.Models;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

public class McpService : IMcpService
{
    private readonly IProjectService _projectService;
    private readonly IProjectAccessService _accessService;
    private readonly IExpenseRepository _expenseRepo;
    private readonly IIncomeRepository _incomeRepo;
    private readonly IObligationRepository _obligationRepo;
    private readonly IProjectBudgetRepository _budgetRepo;
    private readonly IPaymentMethodRepository _paymentMethodRepo;
    private readonly IPlanAuthorizationService _planAuth;

    public McpService(
        IProjectService projectService,
        IProjectAccessService accessService,
        IExpenseRepository expenseRepo,
        IIncomeRepository incomeRepo,
        IObligationRepository obligationRepo,
        IProjectBudgetRepository budgetRepo,
        IPaymentMethodRepository paymentMethodRepo,
        IPlanAuthorizationService planAuth)
    {
        _projectService = projectService;
        _accessService = accessService;
        _expenseRepo = expenseRepo;
        _incomeRepo = incomeRepo;
        _obligationRepo = obligationRepo;
        _budgetRepo = budgetRepo;
        _paymentMethodRepo = paymentMethodRepo;
        _planAuth = planAuth;
    }

    public async Task<McpContextResponse> GetContextAsync(Guid userId, CancellationToken ct = default)
    {
        var scope = await ResolveScopeAsync(userId, null, ct);
        var capabilities = await _planAuth.GetCapabilitiesAsync(userId, ct);
        var roleMap = await BuildRoleMapAsync(userId, scope.VisibleProjects, ct);

        return new McpContextResponse
        {
            UserId = userId,
            GeneratedAtUtc = DateTime.UtcNow,
            DefaultCurrencyCode = ResolveCurrencyCode(scope.VisibleProjects),
            Permissions = capabilities.Permissions,
            Limits = capabilities.Limits,
            VisibleProjects = scope.VisibleProjects
                .OrderBy(p => p.PrjName)
                .Select(p => new McpVisibleProjectResponse
                {
                    ProjectId = p.PrjId,
                    ProjectName = p.PrjName,
                    CurrencyCode = p.PrjCurrencyCode,
                    UserRole = roleMap.GetValueOrDefault(p.PrjId, ProjectRoles.Viewer)
                })
                .ToList()
        };
    }

    public async Task<PagedResponse<McpProjectPortfolioItemResponse>> GetProjectPortfolioAsync(
        Guid userId,
        McpProjectPortfolioQuery query,
        CancellationToken ct = default)
    {
        var scope = await ResolveScopeAsync(userId, query.ProjectId, ct);
        var items = await BuildPortfolioItemsAsync(userId, scope.SelectedProjects, query.ActivityDays, ct);

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            items = items
                .Where(i => i.Status.Equals(query.Status, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var ordered = ApplyProjectPortfolioSorting(items, query.SortBy, query.IsDescending);
        return ToPagedResponse(ordered, query);
    }

    public async Task<PagedResponse<McpProjectDeadlineItemResponse>> GetProjectDeadlinesAsync(
        Guid userId,
        McpProjectDeadlinesQuery query,
        CancellationToken ct = default)
    {
        EnsureValidDateRange(query.DueFrom, query.DueTo);

        var scope = await ResolveScopeAsync(userId, query.ProjectId, ct);
        var obligations = await LoadObligationsWithPaymentsAsync(scope.SelectedProjects, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var items = new List<McpProjectDeadlineItemResponse>();
        foreach (var obligation in obligations)
        {
            if (!obligation.OblDueDate.HasValue)
                continue;

            var paid = ComputePaidAmount(obligation);
            var remaining = Math.Max(0m, obligation.OblTotalAmount - paid);
            var status = ComputeObligationStatus(obligation, paid, today);
            if (remaining <= 0)
                continue;

            if (!query.IncludeOverdue && obligation.OblDueDate.Value < today)
                continue;

            if (query.DueFrom.HasValue && obligation.OblDueDate.Value < query.DueFrom.Value)
                continue;

            if (query.DueTo.HasValue && obligation.OblDueDate.Value > query.DueTo.Value)
                continue;

            items.Add(new McpProjectDeadlineItemResponse
            {
                ProjectId = obligation.OblProjectId,
                ProjectName = obligation.Project.PrjName,
                ObligationId = obligation.OblId,
                Title = obligation.OblTitle,
                DueDate = obligation.OblDueDate.Value,
                DaysUntilDue = obligation.OblDueDate.Value.DayNumber - today.DayNumber,
                RemainingAmount = remaining,
                Currency = obligation.OblCurrency,
                Status = status
            });
        }

        var ordered = query.IsDescending
            ? items.OrderByDescending(i => i.DueDate).ThenBy(i => i.ProjectName)
            : items.OrderBy(i => i.DueDate).ThenBy(i => i.ProjectName);

        return ToPagedResponse(ordered, query);
    }

    public async Task<McpProjectActivitySplitResponse> GetProjectActivitySplitAsync(
        Guid userId,
        McpProjectActivitySplitQuery query,
        CancellationToken ct = default)
    {
        var scope = await ResolveScopeAsync(userId, query.ProjectId, ct);
        var portfolio = await BuildPortfolioItemsAsync(userId, scope.SelectedProjects, query.ActivityDays, ct);

        var response = new McpProjectActivitySplitResponse
        {
            ActiveCount = portfolio.Count(p => p.Status == "active"),
            CompletedCount = portfolio.Count(p => p.Status == "completed"),
            AtRiskCount = portfolio.Count(p => p.Status == "at_risk"),
            InactiveCount = portfolio.Count(p => p.Status == "inactive"),
            Items = portfolio
                .OrderBy(p => p.ProjectName)
                .Select(p => new McpProjectActivityItemResponse
                {
                    ProjectId = p.ProjectId,
                    ProjectName = p.ProjectName,
                    Status = p.Status
                })
                .ToList()
        };

        return response;
    }

    public async Task<PagedResponse<McpPaymentObligationItemResponse>> GetPendingPaymentsAsync(
        Guid userId,
        McpPendingPaymentsQuery query,
        CancellationToken ct = default)
    {
        EnsureValidDateRange(query.DueAfter, query.DueBefore);

        var scope = await ResolveScopeAsync(userId, query.ProjectId, ct);
        var obligations = await LoadObligationsWithPaymentsAsync(scope.SelectedProjects, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var items = obligations
            .Select(o =>
            {
                var paid = ComputePaidAmount(o);
                var remaining = Math.Max(0m, o.OblTotalAmount - paid);
                return new { Obligation = o, Paid = paid, Remaining = remaining, Status = ComputeObligationStatus(o, paid, today) };
            })
            .Where(x => x.Remaining > 0)
            .Where(x => !query.MinRemainingAmount.HasValue || x.Remaining >= query.MinRemainingAmount.Value)
            .Where(x => !query.DueAfter.HasValue || (x.Obligation.OblDueDate.HasValue && x.Obligation.OblDueDate.Value >= query.DueAfter.Value))
            .Where(x => !query.DueBefore.HasValue || (x.Obligation.OblDueDate.HasValue && x.Obligation.OblDueDate.Value <= query.DueBefore.Value))
            .Select(x => new McpPaymentObligationItemResponse
            {
                ObligationId = x.Obligation.OblId,
                ProjectId = x.Obligation.OblProjectId,
                ProjectName = x.Obligation.Project.PrjName,
                Title = x.Obligation.OblTitle,
                DueDate = x.Obligation.OblDueDate,
                TotalAmount = x.Obligation.OblTotalAmount,
                PaidAmount = x.Paid,
                RemainingAmount = x.Remaining,
                Currency = x.Obligation.OblCurrency,
                Status = x.Status
            })
            .ToList();

        var ordered = query.IsDescending
            ? items.OrderByDescending(i => i.DueDate).ThenBy(i => i.ProjectName)
            : items.OrderBy(i => i.DueDate).ThenBy(i => i.ProjectName);

        return ToPagedResponse(ordered, query);
    }

    public async Task<PagedResponse<McpReceivedPaymentItemResponse>> GetReceivedPaymentsAsync(
        Guid userId,
        McpReceivedPaymentsQuery query,
        CancellationToken ct = default)
    {
        EnsureValidDateRange(query.From, query.To);

        var scope = await ResolveScopeAsync(userId, query.ProjectId, ct);
        var incomes = await LoadIncomesAsync(scope.SelectedProjects, query.From, query.To, ct);

        var paymentMethodMap = await BuildPaymentMethodMapAsync(incomes.Select(i => i.IncPaymentMethodId), ct);

        var filtered = incomes
            .Where(i => !query.PaymentMethodId.HasValue || i.IncPaymentMethodId == query.PaymentMethodId.Value)
            .Where(i => !query.CategoryId.HasValue || i.IncCategoryId == query.CategoryId.Value)
            .Where(i => !query.MinAmount.HasValue || i.IncConvertedAmount >= query.MinAmount.Value)
            .Select(i => new McpReceivedPaymentItemResponse
            {
                IncomeId = i.IncId,
                ProjectId = i.IncProjectId,
                ProjectName = i.Project?.PrjName ?? "Unknown",
                CategoryId = i.IncCategoryId,
                CategoryName = i.Category?.CatName ?? "Unknown",
                PaymentMethodId = i.IncPaymentMethodId,
                PaymentMethodName = paymentMethodMap.GetValueOrDefault(i.IncPaymentMethodId)?.PmtName ?? "Unknown",
                IncomeDate = i.IncIncomeDate,
                Title = i.IncTitle,
                OriginalAmount = i.IncOriginalAmount,
                OriginalCurrency = i.IncOriginalCurrency,
                ConvertedAmount = i.IncConvertedAmount
            })
            .ToList();

        var ordered = ApplyReceivedPaymentsSorting(filtered, query.SortBy, query.IsDescending);
        return ToPagedResponse(ordered, query);
    }

    public async Task<PagedResponse<McpPaymentObligationItemResponse>> GetOverduePaymentsAsync(
        Guid userId,
        McpOverduePaymentsQuery query,
        CancellationToken ct = default)
    {
        var scope = await ResolveScopeAsync(userId, query.ProjectId, ct);
        var obligations = await LoadObligationsWithPaymentsAsync(scope.SelectedProjects, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var items = obligations
            .Select(o =>
            {
                var paid = ComputePaidAmount(o);
                var remaining = Math.Max(0m, o.OblTotalAmount - paid);
                var status = ComputeObligationStatus(o, paid, today);
                var daysOverdue = o.OblDueDate.HasValue ? Math.Max(0, today.DayNumber - o.OblDueDate.Value.DayNumber) : 0;
                return new { Obligation = o, Paid = paid, Remaining = remaining, Status = status, DaysOverdue = daysOverdue };
            })
            .Where(x => x.Status == "overdue")
            .Where(x => x.DaysOverdue >= query.OverdueDaysMin)
            .Where(x => !query.MinRemainingAmount.HasValue || x.Remaining >= query.MinRemainingAmount.Value)
            .Select(x => new McpPaymentObligationItemResponse
            {
                ObligationId = x.Obligation.OblId,
                ProjectId = x.Obligation.OblProjectId,
                ProjectName = x.Obligation.Project.PrjName,
                Title = x.Obligation.OblTitle,
                DueDate = x.Obligation.OblDueDate,
                DaysOverdue = x.DaysOverdue,
                TotalAmount = x.Obligation.OblTotalAmount,
                PaidAmount = x.Paid,
                RemainingAmount = x.Remaining,
                Currency = x.Obligation.OblCurrency,
                Status = x.Status
            })
            .OrderByDescending(x => x.DaysOverdue)
            .ThenBy(x => x.DueDate)
            .ToList();

        return ToPagedResponse(items, query);
    }

    public async Task<McpPaymentMethodUsageResponse> GetPaymentMethodUsageAsync(
        Guid userId,
        McpPaymentMethodUsageQuery query,
        CancellationToken ct = default)
    {
        EnsureValidDateRange(query.From, query.To);

        var scope = await ResolveScopeAsync(userId, query.ProjectId, ct);
        var expenses = await LoadExpensesAsync(scope.SelectedProjects, query.From, query.To, ct);
        var incomes = await LoadIncomesAsync(scope.SelectedProjects, query.From, query.To, ct);

        var paymentMethodIds = expenses.Select(e => e.ExpPaymentMethodId)
            .Union(incomes.Select(i => i.IncPaymentMethodId))
            .Distinct()
            .ToList();

        var paymentMethodMap = await BuildPaymentMethodMapAsync(paymentMethodIds, ct);

        var items = paymentMethodIds
            .Select(pmId =>
            {
                var expenseRows = expenses.Where(e => e.ExpPaymentMethodId == pmId).ToList();
                var incomeRows = incomes.Where(i => i.IncPaymentMethodId == pmId).ToList();

                var outgoing = expenseRows.Sum(e => e.ExpConvertedAmount);
                var incoming = incomeRows.Sum(i => i.IncConvertedAmount);

                return new McpPaymentMethodUsageItemResponse
                {
                    PaymentMethodId = pmId,
                    PaymentMethodName = paymentMethodMap.GetValueOrDefault(pmId)?.PmtName ?? "Unknown",
                    PaymentMethodType = paymentMethodMap.GetValueOrDefault(pmId)?.PmtType ?? "unknown",
                    TotalOutgoing = outgoing,
                    TotalIncoming = incoming,
                    NetFlow = incoming - outgoing,
                    ExpenseCount = expenseRows.Count,
                    IncomeCount = incomeRows.Count
                };
            })
            .ToList();

        var totalForShare = query.Direction.ToLowerInvariant() switch
        {
            "expense" => items.Sum(i => i.TotalOutgoing),
            "income" => items.Sum(i => i.TotalIncoming),
            _ => items.Sum(i => i.TotalOutgoing + i.TotalIncoming)
        };

        foreach (var item in items)
        {
            var value = query.Direction.ToLowerInvariant() switch
            {
                "expense" => item.TotalOutgoing,
                "income" => item.TotalIncoming,
                _ => item.TotalOutgoing + item.TotalIncoming
            };

            item.UsagePercentage = totalForShare > 0
                ? Math.Round(value / totalForShare * 100m, 2)
                : 0m;
        }

        return new McpPaymentMethodUsageResponse
        {
            From = query.From,
            To = query.To,
            Direction = query.Direction,
            Items = items
                .OrderByDescending(i => i.TotalOutgoing + i.TotalIncoming)
                .Take(query.Top)
                .ToList()
        };
    }

    public async Task<McpExpenseTotalsResponse> GetExpenseTotalsAsync(
        Guid userId,
        McpExpenseTotalsQuery query,
        CancellationToken ct = default)
    {
        EnsureValidDateRange(query.From, query.To);

        var scope = await ResolveScopeAsync(userId, query.ProjectId, ct);
        var expenses = await LoadExpensesAsync(scope.SelectedProjects, query.From, query.To, ct);

        var total = expenses.Sum(e => e.ExpConvertedAmount);
        var count = expenses.Count;

        var response = new McpExpenseTotalsResponse
        {
            ProjectId = query.ProjectId,
            From = query.From,
            To = query.To,
            TotalSpent = total,
            TransactionCount = count,
            AverageExpense = count > 0 ? Math.Round(total / count, 2) : 0m
        };

        if (query.ComparePreviousPeriod && query.From.HasValue && query.To.HasValue)
        {
            var length = query.To.Value.DayNumber - query.From.Value.DayNumber + 1;
            var prevFrom = query.From.Value.AddDays(-length);
            var prevTo = query.From.Value.AddDays(-1);

            var previousExpenses = await LoadExpensesAsync(scope.SelectedProjects, prevFrom, prevTo, ct);
            var previousTotal = previousExpenses.Sum(e => e.ExpConvertedAmount);

            response.PreviousPeriodTotal = previousTotal;
            response.DeltaAmount = total - previousTotal;
            response.DeltaPercentage = previousTotal == 0
                ? null
                : Math.Round((total - previousTotal) / previousTotal * 100m, 2);
        }

        return response;
    }

    public async Task<McpExpenseByCategoryResponse> GetExpenseByCategoryAsync(
        Guid userId,
        McpExpenseByCategoryQuery query,
        CancellationToken ct = default)
    {
        EnsureValidDateRange(query.From, query.To);

        var scope = await ResolveScopeAsync(userId, query.ProjectId, ct);
        var expenses = await LoadExpensesAsync(scope.SelectedProjects, query.From, query.To, ct);
        var totalSpent = expenses.Sum(e => e.ExpConvertedAmount);

        Dictionary<Guid, decimal>? previousPeriodByCategory = null;
        if (query.IncludeTrend && query.From.HasValue && query.To.HasValue)
        {
            var length = query.To.Value.DayNumber - query.From.Value.DayNumber + 1;
            var prevFrom = query.From.Value.AddDays(-length);
            var prevTo = query.From.Value.AddDays(-1);

            var previous = await LoadExpensesAsync(scope.SelectedProjects, prevFrom, prevTo, ct);
            previousPeriodByCategory = previous
                .GroupBy(e => e.ExpCategoryId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.ExpConvertedAmount));
        }

        var grouped = expenses
            .GroupBy(e => new { e.ExpCategoryId, Name = e.Category?.CatName ?? "Unknown" })
            .Select(g =>
            {
                var amount = g.Sum(x => x.ExpConvertedAmount);
                decimal? trendDelta = null;
                if (previousPeriodByCategory is not null)
                {
                    var previousAmount = previousPeriodByCategory.GetValueOrDefault(g.Key.ExpCategoryId, 0m);
                    trendDelta = amount - previousAmount;
                }

                return new McpExpenseByCategoryItemResponse
                {
                    CategoryId = g.Key.ExpCategoryId,
                    CategoryName = g.Key.Name,
                    TotalAmount = amount,
                    ExpenseCount = g.Count(),
                    Percentage = totalSpent > 0 ? Math.Round(amount / totalSpent * 100m, 2) : 0m,
                    TrendDelta = trendDelta
                };
            })
            .OrderByDescending(x => x.TotalAmount)
            .ToList();

        var topItems = grouped.Take(query.Top).ToList();
        if (query.IncludeOthers && grouped.Count > query.Top)
        {
            var rest = grouped.Skip(query.Top).ToList();
            topItems.Add(new McpExpenseByCategoryItemResponse
            {
                CategoryId = Guid.Empty,
                CategoryName = "Others",
                TotalAmount = rest.Sum(r => r.TotalAmount),
                ExpenseCount = rest.Sum(r => r.ExpenseCount),
                Percentage = totalSpent > 0 ? Math.Round(rest.Sum(r => r.TotalAmount) / totalSpent * 100m, 2) : 0m
            });
        }

        return new McpExpenseByCategoryResponse
        {
            ProjectId = query.ProjectId,
            From = query.From,
            To = query.To,
            TotalSpent = totalSpent,
            Items = topItems
        };
    }

    public async Task<McpExpenseByProjectResponse> GetExpenseByProjectAsync(
        Guid userId,
        McpExpenseByProjectQuery query,
        CancellationToken ct = default)
    {
        EnsureValidDateRange(query.From, query.To);

        var scope = await ResolveScopeAsync(userId, null, ct);
        var expenses = await LoadExpensesAsync(scope.SelectedProjects, query.From, query.To, ct);
        var totalSpent = expenses.Sum(e => e.ExpConvertedAmount);

        var budgetMap = new Dictionary<Guid, ProjectBudget?>();
        if (query.IncludeBudgetContext)
        {
            foreach (var project in scope.SelectedProjects)
                budgetMap[project.PrjId] = await _budgetRepo.GetActiveByProjectIdAsync(project.PrjId, ct);
        }

        var grouped = scope.SelectedProjects
            .Select(project =>
            {
                var projectExpenses = expenses.Where(e => e.ExpProjectId == project.PrjId).ToList();
                var projectSpent = projectExpenses.Sum(e => e.ExpConvertedAmount);
                var budget = budgetMap.GetValueOrDefault(project.PrjId);

                return new McpExpenseByProjectItemResponse
                {
                    ProjectId = project.PrjId,
                    ProjectName = project.PrjName,
                    CurrencyCode = project.PrjCurrencyCode,
                    TotalSpent = projectSpent,
                    ExpenseCount = projectExpenses.Count,
                    Budget = budget?.PjbTotalBudget,
                    BudgetUsedPercentage = budget is not null && budget.PjbTotalBudget > 0
                        ? Math.Round(projectSpent / budget.PjbTotalBudget * 100m, 2)
                        : null
                };
            })
            .OrderByDescending(x => x.TotalSpent)
            .Take(query.Top)
            .ToList();

        return new McpExpenseByProjectResponse
        {
            From = query.From,
            To = query.To,
            TotalSpent = totalSpent,
            Items = grouped
        };
    }

    public async Task<McpExpenseTrendsResponse> GetExpenseTrendsAsync(
        Guid userId,
        McpExpenseTrendsQuery query,
        CancellationToken ct = default)
    {
        EnsureValidDateRange(query.From, query.To);

        var (from, to) = ResolveRangeOrDefaults(query.From, query.To, query.Granularity);

        var scope = await ResolveScopeAsync(userId, query.ProjectId, ct);
        var expenses = await LoadExpensesAsync(scope.SelectedProjects, from, to, ct);

        if (query.CategoryId.HasValue)
            expenses = expenses.Where(e => e.ExpCategoryId == query.CategoryId.Value).ToList();

        var points = expenses
            .GroupBy(e => GetPeriodStart(e.ExpExpenseDate, query.Granularity))
            .OrderBy(g => g.Key)
            .Select(g => new McpExpenseTrendPointResponse
            {
                PeriodStart = g.Key,
                PeriodLabel = BuildPeriodLabel(g.Key, query.Granularity),
                TotalSpent = g.Sum(x => x.ExpConvertedAmount),
                ExpenseCount = g.Count()
            })
            .ToList();

        return new McpExpenseTrendsResponse
        {
            ProjectId = query.ProjectId,
            From = from,
            To = to,
            Granularity = query.Granularity,
            Points = points
        };
    }

    public async Task<McpIncomeByPeriodResponse> GetIncomeByPeriodAsync(
        Guid userId,
        McpIncomeByPeriodQuery query,
        CancellationToken ct = default)
    {
        EnsureValidDateRange(query.From, query.To);

        var (from, to) = ResolveRangeOrDefaults(query.From, query.To, query.Granularity);

        var scope = await ResolveScopeAsync(userId, query.ProjectId, ct);
        var incomes = await LoadIncomesAsync(scope.SelectedProjects, from, to, ct);

        var points = incomes
            .GroupBy(i => GetPeriodStart(i.IncIncomeDate, query.Granularity))
            .OrderBy(g => g.Key)
            .Select(g => new McpIncomePeriodPointResponse
            {
                PeriodStart = g.Key,
                PeriodLabel = BuildPeriodLabel(g.Key, query.Granularity),
                TotalIncome = g.Sum(x => x.IncConvertedAmount),
                IncomeCount = g.Count()
            })
            .ToList();

        var total = points.Sum(p => p.TotalIncome);
        var count = points.Sum(p => p.IncomeCount);

        var response = new McpIncomeByPeriodResponse
        {
            ProjectId = query.ProjectId,
            From = from,
            To = to,
            Granularity = query.Granularity,
            TotalIncome = total,
            IncomeCount = count,
            Points = points
        };

        if (query.ComparePreviousPeriod && from.HasValue && to.HasValue)
        {
            var length = to.Value.DayNumber - from.Value.DayNumber + 1;
            var prevFrom = from.Value.AddDays(-length);
            var prevTo = from.Value.AddDays(-1);
            var previous = await LoadIncomesAsync(scope.SelectedProjects, prevFrom, prevTo, ct);
            var previousTotal = previous.Sum(i => i.IncConvertedAmount);

            response.PreviousPeriodTotal = previousTotal;
            response.DeltaAmount = total - previousTotal;
            response.DeltaPercentage = previousTotal == 0
                ? null
                : Math.Round((total - previousTotal) / previousTotal * 100m, 2);
        }

        return response;
    }

    public async Task<McpIncomeByProjectResponse> GetIncomeByProjectAsync(
        Guid userId,
        McpIncomeByProjectQuery query,
        CancellationToken ct = default)
    {
        EnsureValidDateRange(query.From, query.To);

        var scope = await ResolveScopeAsync(userId, null, ct);
        var incomes = await LoadIncomesAsync(scope.SelectedProjects, query.From, query.To, ct);

        var grouped = scope.SelectedProjects
            .Select(project =>
            {
                var projectIncomes = incomes.Where(i => i.IncProjectId == project.PrjId).ToList();
                return new McpIncomeByProjectItemResponse
                {
                    ProjectId = project.PrjId,
                    ProjectName = project.PrjName,
                    CurrencyCode = project.PrjCurrencyCode,
                    TotalIncome = projectIncomes.Sum(i => i.IncConvertedAmount),
                    IncomeCount = projectIncomes.Count
                };
            })
            .OrderByDescending(i => i.TotalIncome)
            .Take(query.Top)
            .ToList();

        return new McpIncomeByProjectResponse
        {
            From = query.From,
            To = query.To,
            TotalIncome = grouped.Sum(g => g.TotalIncome),
            Items = grouped
        };
    }

    public async Task<PagedResponse<McpObligationItemResponse>> GetUpcomingObligationsAsync(
        Guid userId,
        McpUpcomingObligationsQuery query,
        CancellationToken ct = default)
    {
        var scope = await ResolveScopeAsync(userId, query.ProjectId, ct);
        var obligations = await LoadObligationsWithPaymentsAsync(scope.SelectedProjects, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var end = today.AddDays(query.DueWithinDays);

        var items = obligations
            .Where(o => o.OblDueDate.HasValue && o.OblDueDate.Value >= today && o.OblDueDate.Value <= end)
            .Select(o =>
            {
                var paid = ComputePaidAmount(o);
                var remaining = Math.Max(0m, o.OblTotalAmount - paid);
                var status = ComputeObligationStatus(o, paid, today);
                return new McpObligationItemResponse
                {
                    ObligationId = o.OblId,
                    ProjectId = o.OblProjectId,
                    ProjectName = o.Project.PrjName,
                    Title = o.OblTitle,
                    DueDate = o.OblDueDate,
                    TotalAmount = o.OblTotalAmount,
                    PaidAmount = paid,
                    RemainingAmount = remaining,
                    Currency = o.OblCurrency,
                    Status = status,
                    DaysUntilDue = o.OblDueDate.HasValue ? o.OblDueDate.Value.DayNumber - today.DayNumber : null
                };
            })
            .Where(i => i.RemainingAmount > 0)
            .Where(i => !query.MinRemainingAmount.HasValue || i.RemainingAmount >= query.MinRemainingAmount.Value)
            .OrderBy(i => i.DueDate)
            .ThenByDescending(i => i.RemainingAmount)
            .ToList();

        return ToPagedResponse(items, query);
    }

    public async Task<PagedResponse<McpObligationItemResponse>> GetUnpaidObligationsAsync(
        Guid userId,
        McpUnpaidObligationsQuery query,
        CancellationToken ct = default)
    {
        var scope = await ResolveScopeAsync(userId, query.ProjectId, ct);
        var obligations = await LoadObligationsWithPaymentsAsync(scope.SelectedProjects, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var items = obligations
            .Select(o =>
            {
                var paid = ComputePaidAmount(o);
                var remaining = Math.Max(0m, o.OblTotalAmount - paid);
                var status = ComputeObligationStatus(o, paid, today);
                return new McpObligationItemResponse
                {
                    ObligationId = o.OblId,
                    ProjectId = o.OblProjectId,
                    ProjectName = o.Project.PrjName,
                    Title = o.OblTitle,
                    DueDate = o.OblDueDate,
                    TotalAmount = o.OblTotalAmount,
                    PaidAmount = paid,
                    RemainingAmount = remaining,
                    Currency = o.OblCurrency,
                    Status = status,
                    DaysUntilDue = o.OblDueDate.HasValue && o.OblDueDate.Value >= today
                        ? o.OblDueDate.Value.DayNumber - today.DayNumber
                        : null,
                    DaysOverdue = o.OblDueDate.HasValue && o.OblDueDate.Value < today
                        ? today.DayNumber - o.OblDueDate.Value.DayNumber
                        : null
                };
            })
            .Where(i => i.RemainingAmount > 0)
            .Where(i => string.IsNullOrWhiteSpace(query.Status) || i.Status.Equals(query.Status, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(i => i.DaysOverdue ?? 0)
            .ThenBy(i => i.DueDate)
            .ToList();

        return ToPagedResponse(items, query);
    }

    public async Task<McpFinancialHealthResponse> GetFinancialHealthAsync(
        Guid userId,
        McpFinancialHealthQuery query,
        CancellationToken ct = default)
    {
        EnsureValidDateRange(query.From, query.To);

        var (from, to) = ResolveRangeOrDefaults(query.From, query.To, "month");
        var scope = await ResolveScopeAsync(userId, query.ProjectId, ct);

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

        var scope = await ResolveScopeAsync(userId, query.ProjectId, ct);
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
        DateOnly? from = null;
        DateOnly? to = null;

        if (!string.IsNullOrWhiteSpace(query.Month))
        {
            var monthStart = ParseMonthOrDefault(query.Month);
            from = monthStart;
            to = monthStart.AddMonths(1).AddDays(-1);
        }

        var scope = await ResolveScopeAsync(userId, query.ProjectId, ct);
        var items = await BuildAlertsAsync(scope.SelectedProjects, from, to, query.MinPriority, ct);

        return new McpAlertsResponse
        {
            Month = query.Month,
            ProjectId = query.ProjectId,
            GeneratedAtUtc = DateTime.UtcNow,
            Items = items
        };
    }

    private async Task<List<McpProjectPortfolioItemResponse>> BuildPortfolioItemsAsync(
        Guid userId,
        IReadOnlyCollection<Project> selectedProjects,
        int activityDays,
        CancellationToken ct)
    {
        var roleMap = await BuildRoleMapAsync(userId, selectedProjects, ct);
        var expenses = await LoadExpensesAsync(selectedProjects, null, null, ct);
        var incomes = await LoadIncomesAsync(selectedProjects, null, null, ct);
        var obligations = await LoadObligationsWithPaymentsAsync(selectedProjects, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var activityThreshold = DateTime.UtcNow.AddDays(-activityDays);

        var obligationByProject = obligations.GroupBy(o => o.OblProjectId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<McpProjectPortfolioItemResponse>();

        foreach (var project in selectedProjects)
        {
            var projectExpenses = expenses.Where(e => e.ExpProjectId == project.PrjId).ToList();
            var projectIncomes = incomes.Where(i => i.IncProjectId == project.PrjId).ToList();
            var projectObligations = obligationByProject.GetValueOrDefault(project.PrjId, []);

            var totalSpent = projectExpenses.Sum(e => e.ExpConvertedAmount);
            var totalIncome = projectIncomes.Sum(i => i.IncConvertedAmount);

            var expenseLastActivity = projectExpenses
                .OrderByDescending(e => e.ExpUpdatedAt)
                .Select(e => (DateTime?)e.ExpUpdatedAt)
                .FirstOrDefault();

            var incomeLastActivity = projectIncomes
                .OrderByDescending(i => i.IncUpdatedAt)
                .Select(i => (DateTime?)i.IncUpdatedAt)
                .FirstOrDefault();

            var lastActivity = MaxDate(expenseLastActivity, incomeLastActivity);

            var budget = await _budgetRepo.GetActiveByProjectIdAsync(project.PrjId, ct);
            var budgetUsedPercentage = budget is not null && budget.PjbTotalBudget > 0
                ? Math.Round(totalSpent / budget.PjbTotalBudget * 100m, 2)
                : (decimal?)null;

            var obligationStatuses = projectObligations
                .Select(o =>
                {
                    var paid = ComputePaidAmount(o);
                    var status = ComputeObligationStatus(o, paid, today);
                    var remaining = Math.Max(0m, o.OblTotalAmount - paid);
                    return new { Obligation = o, Status = status, Remaining = remaining };
                })
                .ToList();

            var nextDeadline = obligationStatuses
                .Where(x => x.Remaining > 0 && x.Obligation.OblDueDate.HasValue)
                .Select(x => x.Obligation.OblDueDate!.Value)
                .OrderBy(d => d)
                .FirstOrDefault();

            var overdueCount = obligationStatuses.Count(x => x.Status == "overdue");
            var openCount = obligationStatuses.Count(x => x.Remaining > 0);

            var progressPercent = 0m;
            if (budgetUsedPercentage.HasValue)
            {
                progressPercent = Math.Min(100m, budgetUsedPercentage.Value);
            }
            else if (projectObligations.Count > 0)
            {
                var totalObligations = projectObligations.Sum(o => o.OblTotalAmount);
                var paidObligations = projectObligations.Sum(o => ComputePaidAmount(o));
                progressPercent = totalObligations > 0
                    ? Math.Min(100m, Math.Round(paidObligations / totalObligations * 100m, 2))
                    : 0m;
            }

            var status = ComputeProjectStatus(
                lastActivity,
                activityThreshold,
                overdueCount,
                openCount,
                budgetUsedPercentage,
                totalSpent,
                totalIncome);

            result.Add(new McpProjectPortfolioItemResponse
            {
                ProjectId = project.PrjId,
                ProjectName = project.PrjName,
                UserRole = roleMap.GetValueOrDefault(project.PrjId, ProjectRoles.Viewer),
                CurrencyCode = project.PrjCurrencyCode,
                LastActivityAtUtc = lastActivity,
                NextDeadline = nextDeadline == default ? null : nextDeadline,
                Status = status,
                ProgressPercent = progressPercent,
                TotalSpent = totalSpent,
                TotalIncome = totalIncome,
                NetBalance = totalIncome - totalSpent,
                BudgetUsedPercentage = budgetUsedPercentage,
                OpenObligations = openCount,
                OverdueObligations = overdueCount
            });
        }

        return result;
    }

    private static string ComputeProjectStatus(
        DateTime? lastActivity,
        DateTime activityThreshold,
        int overdueObligations,
        int openObligations,
        decimal? budgetUsedPercentage,
        decimal totalSpent,
        decimal totalIncome)
    {
        if (overdueObligations > 0 || budgetUsedPercentage is >= 100m)
            return "at_risk";

        if (openObligations == 0 && (totalSpent > 0 || totalIncome > 0))
            return "completed";

        if (!lastActivity.HasValue || lastActivity.Value < activityThreshold)
            return "inactive";

        return "active";
    }

    private async Task<List<McpAlertResponse>> BuildAlertsAsync(
        IReadOnlyCollection<Project> selectedProjects,
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

    private async Task<McpScope> ResolveScopeAsync(Guid userId, Guid? projectId, CancellationToken ct)
    {
        var owned = (await _projectService.GetByOwnerUserIdAsync(userId, ct)).ToList();
        var member = (await _projectService.GetByMemberUserIdAsync(userId, ct)).ToList();

        var candidates = owned
            .Union(member, new ProjectIdComparer())
            .ToList();

        // Defense-in-depth: revalidate effective access to avoid accidental cross-tenant leakage.
        var visible = new List<Project>(candidates.Count);
        foreach (var project in candidates)
        {
            if (project.PrjOwnerUserId == userId)
            {
                visible.Add(project);
                continue;
            }

            if (await _accessService.HasAccessAsync(userId, project.PrjId, ProjectRoles.Viewer, ct))
                visible.Add(project);
        }

        if (projectId.HasValue)
        {
            await _accessService.ValidateAccessAsync(userId, projectId.Value, ProjectRoles.Viewer, ct);

            if (visible.All(p => p.PrjId != projectId.Value))
                throw new ForbiddenAccessException("User does not have access to the selected project.");
        }

        var selected = projectId.HasValue
            ? visible.Where(p => p.PrjId == projectId.Value).ToList()
            : visible;

        return new McpScope(visible, selected);
    }

    private async Task<Dictionary<Guid, string>> BuildRoleMapAsync(
        Guid userId,
        IReadOnlyCollection<Project> projects,
        CancellationToken ct)
    {
        var roles = new Dictionary<Guid, string>();

        foreach (var project in projects)
        {
            if (project.PrjOwnerUserId == userId)
            {
                roles[project.PrjId] = ProjectRoles.Owner;
                continue;
            }

            var role = await _accessService.GetUserRoleAsync(userId, project.PrjId, ct);
            if (string.IsNullOrWhiteSpace(role))
                throw new ForbiddenAccessException("User does not have access to one or more projects in scope.");

            roles[project.PrjId] = role;
        }

        return roles;
    }

    private async Task<List<Expense>> LoadExpensesAsync(
        IReadOnlyCollection<Project> projects,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct)
    {
        EnsureValidDateRange(from, to);

        var result = new List<Expense>();
        foreach (var project in projects)
        {
            var rows = await _expenseRepo.GetByProjectIdWithDetailsAsync(project.PrjId, ct);
            result.AddRange(rows
                .Where(e => !e.ExpIsTemplate)
                .Where(e => !from.HasValue || e.ExpExpenseDate >= from.Value)
                .Where(e => !to.HasValue || e.ExpExpenseDate <= to.Value));
        }

        return result;
    }

    private async Task<List<Income>> LoadIncomesAsync(
        IReadOnlyCollection<Project> projects,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct)
    {
        EnsureValidDateRange(from, to);

        var result = new List<Income>();
        foreach (var project in projects)
        {
            var rows = await _incomeRepo.GetByProjectIdAsync(project.PrjId, ct);
            result.AddRange(rows
                .Where(i => !from.HasValue || i.IncIncomeDate >= from.Value)
                .Where(i => !to.HasValue || i.IncIncomeDate <= to.Value));
        }

        return result;
    }

    private async Task<List<Obligation>> LoadObligationsWithPaymentsAsync(
        IReadOnlyCollection<Project> projects,
        CancellationToken ct)
    {
        var result = new List<Obligation>();
        foreach (var project in projects)
        {
            var rows = await _obligationRepo.GetByProjectIdWithPaymentsAsync(project.PrjId, ct);
            result.AddRange(rows);
        }

        return result;
    }

    private async Task<Dictionary<Guid, PaymentMethod>> BuildPaymentMethodMapAsync(
        IEnumerable<Guid> paymentMethodIds,
        CancellationToken ct)
    {
        var map = new Dictionary<Guid, PaymentMethod>();

        foreach (var id in paymentMethodIds.Distinct())
        {
            var paymentMethod = await _paymentMethodRepo.GetByIdAsync(id, ct);
            if (paymentMethod is not null && !paymentMethod.PmtIsDeleted)
                map[id] = paymentMethod;
        }

        return map;
    }

    private static PagedResponse<T> ToPagedResponse<T>(IEnumerable<T> source, PagedRequest request)
    {
        var list = source.ToList();
        var total = list.Count;
        var items = list.Skip(request.Skip).Take(request.PageSize).ToList();
        return PagedResponse<T>.Create(items, total, request);
    }

    private static IEnumerable<McpProjectPortfolioItemResponse> ApplyProjectPortfolioSorting(
        IEnumerable<McpProjectPortfolioItemResponse> source,
        string? sortBy,
        bool desc)
    {
        return sortBy?.ToLowerInvariant() switch
        {
            "name" => desc ? source.OrderByDescending(x => x.ProjectName) : source.OrderBy(x => x.ProjectName),
            "status" => desc ? source.OrderByDescending(x => x.Status) : source.OrderBy(x => x.Status),
            "totalspent" => desc ? source.OrderByDescending(x => x.TotalSpent) : source.OrderBy(x => x.TotalSpent),
            "totalincome" => desc ? source.OrderByDescending(x => x.TotalIncome) : source.OrderBy(x => x.TotalIncome),
            "netbalance" => desc ? source.OrderByDescending(x => x.NetBalance) : source.OrderBy(x => x.NetBalance),
            "progress" => desc ? source.OrderByDescending(x => x.ProgressPercent) : source.OrderBy(x => x.ProgressPercent),
            _ => desc
                ? source.OrderByDescending(x => x.LastActivityAtUtc ?? DateTime.MinValue)
                : source.OrderBy(x => x.LastActivityAtUtc ?? DateTime.MinValue)
        };
    }

    private static IEnumerable<McpReceivedPaymentItemResponse> ApplyReceivedPaymentsSorting(
        IEnumerable<McpReceivedPaymentItemResponse> source,
        string? sortBy,
        bool desc)
    {
        return sortBy?.ToLowerInvariant() switch
        {
            "title" => desc ? source.OrderByDescending(x => x.Title) : source.OrderBy(x => x.Title),
            "amount" => desc ? source.OrderByDescending(x => x.ConvertedAmount) : source.OrderBy(x => x.ConvertedAmount),
            "project" => desc ? source.OrderByDescending(x => x.ProjectName) : source.OrderBy(x => x.ProjectName),
            _ => desc ? source.OrderByDescending(x => x.IncomeDate) : source.OrderBy(x => x.IncomeDate)
        };
    }

    private static void EnsureValidDateRange(DateOnly? from, DateOnly? to)
    {
        if (from.HasValue && to.HasValue && from.Value > to.Value)
            throw new ArgumentException("Invalid date range: 'from' cannot be greater than 'to'.");
    }

    private static (DateOnly? From, DateOnly? To) ResolveRangeOrDefaults(
        DateOnly? from,
        DateOnly? to,
        string granularity)
    {
        if (from.HasValue && to.HasValue)
            return (from, to);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return granularity.ToLowerInvariant() switch
        {
            "day" => (from ?? today.AddDays(-29), to ?? today),
            "week" => (from ?? today.AddDays(-83), to ?? today),
            _ => (from ?? new DateOnly(today.Year, today.Month, 1).AddMonths(-11), to ?? today)
        };
    }

    private static DateOnly ParseMonthOrDefault(string? month)
    {
        if (string.IsNullOrWhiteSpace(month))
            return new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

        if (!DateOnly.TryParseExact(
                $"{month}-01",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            throw new ArgumentException("Month must use YYYY-MM format.");
        }

        return parsed;
    }

    private static DateOnly GetPeriodStart(DateOnly date, string granularity)
    {
        return granularity.ToLowerInvariant() switch
        {
            "day" => date,
            "week" => date.AddDays(-((7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7)),
            _ => new DateOnly(date.Year, date.Month, 1)
        };
    }

    private static string BuildPeriodLabel(DateOnly periodStart, string granularity)
    {
        return granularity.ToLowerInvariant() switch
        {
            "day" => periodStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "week" => $"Week of {periodStart:yyyy-MM-dd}",
            _ => periodStart.ToString("yyyy-MM", CultureInfo.InvariantCulture)
        };
    }

    private static string ResolveCurrencyCode(IReadOnlyCollection<Project> projects)
    {
        return projects
            .Where(p => !string.IsNullOrWhiteSpace(p.PrjCurrencyCode))
            .GroupBy(p => p.PrjCurrencyCode)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Select(g => g.Key)
            .FirstOrDefault() ?? "USD";
    }

    private static decimal ComputePaidAmount(Obligation obligation)
    {
        return obligation.Payments.Sum(payment =>
            string.Equals(payment.ExpOriginalCurrency, obligation.OblCurrency, StringComparison.OrdinalIgnoreCase)
                ? payment.ExpOriginalAmount
                : payment.ExpObligationEquivalentAmount ?? payment.ExpConvertedAmount);
    }

    private static string ComputeObligationStatus(Obligation obligation, decimal paid, DateOnly today)
    {
        if (paid >= obligation.OblTotalAmount) return "paid";
        if (obligation.OblDueDate.HasValue && obligation.OblDueDate.Value < today) return "overdue";
        if (paid > 0) return "partially_paid";
        return "open";
    }

    private static DateTime? MaxDate(DateTime? left, DateTime? right)
    {
        if (!left.HasValue) return right;
        if (!right.HasValue) return left;
        return left > right ? left : right;
    }

    private sealed class ProjectIdComparer : IEqualityComparer<Project>
    {
        public bool Equals(Project? x, Project? y) => x?.PrjId == y?.PrjId;
        public int GetHashCode(Project obj) => obj.PrjId.GetHashCode();
    }

    private sealed record McpScope(
        IReadOnlyCollection<Project> VisibleProjects,
        IReadOnlyCollection<Project> SelectedProjects);
}
