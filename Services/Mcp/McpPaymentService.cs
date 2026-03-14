using ProjectLedger.API.DTOs.Mcp;

namespace ProjectLedger.API.Services;

public partial class McpService
{
    public async Task<McpPagedResponse<McpPaymentObligationItemResponse>> GetPendingPaymentsAsync(
        Guid userId,
        McpPendingPaymentsQuery query,
        CancellationToken ct = default)
    {
        EnsureValidDateRange(query.DueAfter, query.DueBefore);

        var scope = await ResolveScopeAsync(userId, query.ProjectId, query.ProjectName, ct);
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
            .Where(x => string.IsNullOrWhiteSpace(query.Search)
                || ContainsText(x.Obligation.OblTitle, query.Search)
                || ContainsText(x.Obligation.OblDescription, query.Search))
            .Select(x => new McpPaymentObligationItemResponse
            {
                ObligationId = x.Obligation.OblId,
                ProjectId = x.Obligation.OblProjectId,
                ProjectName = x.Obligation.Project.PrjName,
                Title = x.Obligation.OblTitle,
                DueDate = x.Obligation.OblDueDate,
                DaysUntilDue = x.Obligation.OblDueDate.HasValue && x.Obligation.OblDueDate.Value >= today
                    ? x.Obligation.OblDueDate.Value.DayNumber - today.DayNumber
                    : null,
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

        return ToMcpPagedResponse(ordered, query, scope.SearchNote);
    }

    public async Task<McpPagedResponse<McpReceivedPaymentItemResponse>> GetReceivedPaymentsAsync(
        Guid userId,
        McpReceivedPaymentsQuery query,
        CancellationToken ct = default)
    {
        EnsureValidDateRange(query.From, query.To);

        var scope = await ResolveScopeAsync(userId, query.ProjectId, query.ProjectName, ct);
        var incomes = await LoadIncomesAsync(scope.SelectedProjects, query.From, query.To, ct);
        var paymentMethodMap = await BuildPaymentMethodMapAsync(incomes.Select(i => i.IncPaymentMethodId), ct);

        string? paymentMethodSearchNote = null;
        HashSet<Guid>? paymentMethodIdsFromName = null;
        if (!query.PaymentMethodId.HasValue && !string.IsNullOrWhiteSpace(query.PaymentMethodName))
        {
            paymentMethodIdsFromName = FilterByNameWithPriority(paymentMethodMap, kvp => kvp.Value.PmtName, query.PaymentMethodName)
                .Select(kvp => kvp.Key)
                .ToHashSet();

            if (paymentMethodIdsFromName.Count == 0)
                paymentMethodSearchNote = $"No payment methods matched paymentMethodName '{query.PaymentMethodName}'. Returned empty results.";
        }

        string? categorySearchNote = null;
        HashSet<Guid>? categoryIdsFromName = null;
        if (!query.CategoryId.HasValue && !string.IsNullOrWhiteSpace(query.CategoryName))
        {
            categoryIdsFromName = FilterByNameWithPriority(incomes, i => i.Category?.CatName, query.CategoryName)
                .Select(i => i.IncCategoryId)
                .Distinct()
                .ToHashSet();

            if (categoryIdsFromName.Count == 0)
                categorySearchNote = $"No categories matched categoryName '{query.CategoryName}'. Returned empty results.";
        }

        var filtered = incomes
            .Where(i => !query.IsActive.HasValue || i.IncIsActive == query.IsActive.Value)
            .Where(i => !query.PaymentMethodId.HasValue || i.IncPaymentMethodId == query.PaymentMethodId.Value)
            .Where(i => paymentMethodIdsFromName is null || paymentMethodIdsFromName.Contains(i.IncPaymentMethodId))
            .Where(i => !query.CategoryId.HasValue || i.IncCategoryId == query.CategoryId.Value)
            .Where(i => categoryIdsFromName is null || categoryIdsFromName.Contains(i.IncCategoryId))
            .Where(i => !query.MinAmount.HasValue || i.IncConvertedAmount >= query.MinAmount.Value)
            .Where(i => string.IsNullOrWhiteSpace(query.Search)
                || ContainsText(i.IncTitle, query.Search)
                || ContainsText(i.IncDescription, query.Search))
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
                ConvertedAmount = i.IncConvertedAmount,
                IsActive = i.IncIsActive
            })
            .ToList();

        var ordered = ApplyReceivedPaymentsSorting(filtered, query.SortBy, query.IsDescending);
        var searchNote = CombineSearchNotes(scope.SearchNote, paymentMethodSearchNote, categorySearchNote);
        return ToMcpPagedResponse(ordered, query, searchNote);
    }

    public async Task<McpPagedResponse<McpPaymentObligationItemResponse>> GetOverduePaymentsAsync(
        Guid userId,
        McpOverduePaymentsQuery query,
        CancellationToken ct = default)
    {
        var scope = await ResolveScopeAsync(userId, query.ProjectId, query.ProjectName, ct);
        var obligations = await LoadObligationsWithPaymentsAsync(scope.SelectedProjects, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var overdueDaysMin = query.OverdueDaysMin ?? 0;

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
            .Where(x => x.DaysOverdue >= overdueDaysMin)
            .Where(x => !query.MinRemainingAmount.HasValue || x.Remaining >= query.MinRemainingAmount.Value)
            .Where(x => string.IsNullOrWhiteSpace(query.Search)
                || ContainsText(x.Obligation.OblTitle, query.Search)
                || ContainsText(x.Obligation.OblDescription, query.Search))
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

        return ToMcpPagedResponse(items, query, scope.SearchNote);
    }

    public async Task<McpPaymentMethodUsageResponse> GetPaymentMethodUsageAsync(
        Guid userId,
        McpPaymentMethodUsageQuery query,
        CancellationToken ct = default)
    {
        EnsureValidDateRange(query.From, query.To);
        var direction = NormalizeDirection(query.Direction);
        var top = query.Top ?? 10;

        var scope = await ResolveScopeAsync(userId, query.ProjectId, query.ProjectName, ct);
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

        var totalForShare = direction.ToLowerInvariant() switch
        {
            "expense" => items.Sum(i => i.TotalOutgoing),
            "income" => items.Sum(i => i.TotalIncoming),
            _ => items.Sum(i => i.TotalOutgoing + i.TotalIncoming)
        };

        foreach (var item in items)
        {
            var value = direction.ToLowerInvariant() switch
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
            Direction = direction,
            SearchNote = scope.SearchNote,
            Items = items
                .OrderByDescending(i => i.TotalOutgoing + i.TotalIncoming)
                .Take(top)
                .ToList()
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
}
