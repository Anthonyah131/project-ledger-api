using ProjectLedger.API.DTOs.Mcp;

namespace ProjectLedger.API.Services;

public partial class McpService
{
    public async Task<McpRecentMovementsResponse> GetRecentMovementsAsync(
        Guid userId,
        McpRecentMovementsQuery query,
        CancellationToken ct = default)
    {
        EnsureValidDateRange(query.From, query.To);

        var top  = Math.Min(query.Top ?? 10, 50);
        var type = NormalizeDirection(query.Type);

        var scope = await ResolveScopeAsync(userId, query.ProjectId, query.ProjectName, ct);

        var expenseItems = new List<McpMovementItemResponse>();
        var incomeItems  = new List<McpMovementItemResponse>();

        // ── Load expenses ──────────────────────────────────────────────────
        if (type is "expense" or "both")
        {
            foreach (var project in scope.SelectedProjects)
            {
                var rows = await _expenseRepo.GetDetailedByProjectIdAsync(project.PrjId, query.From, query.To, ct);

                foreach (var e in rows)
                {
                    // category filter
                    if (!string.IsNullOrWhiteSpace(query.CategoryName) &&
                        !ContainsText(e.Category?.CatName, query.CategoryName))
                        continue;

                    // payment method filter
                    if (!string.IsNullOrWhiteSpace(query.PaymentMethodName) &&
                        !ContainsText(e.PaymentMethod?.PmtName, query.PaymentMethodName))
                        continue;

                    // partner filter: must have a split with this partner
                    if (!string.IsNullOrWhiteSpace(query.PartnerName))
                    {
                        var hasPartner = e.Splits.Any(s =>
                            ContainsText(s.Partner?.PtrName, query.PartnerName));
                        if (!hasPartner) continue;
                    }

                    // search
                    if (!string.IsNullOrWhiteSpace(query.Search) &&
                        !ContainsText(e.ExpTitle, query.Search) &&
                        !ContainsText(e.ExpDescription, query.Search))
                        continue;

                    expenseItems.Add(new McpMovementItemResponse
                    {
                        Type              = "expense",
                        Title             = e.ExpTitle,
                        Description       = e.ExpDescription,
                        Date              = e.ExpExpenseDate,
                        Amount            = e.ExpConvertedAmount,
                        Currency          = project.PrjCurrencyCode,
                        OriginalAmount    = e.ExpOriginalAmount,
                        OriginalCurrency  = e.ExpOriginalCurrency,
                        ProjectName       = project.PrjName,
                        CategoryName      = e.Category?.CatName ?? "Unknown",
                        PaymentMethodName = e.PaymentMethod?.PmtName ?? "Unknown",
                        HasSplits         = e.Splits.Count > 0,
                        SplitPartners     = e.Splits
                            .Select(s => s.Partner?.PtrName)
                            .Where(n => !string.IsNullOrWhiteSpace(n))
                            .Select(n => n!)
                            .ToList()
                    });
                }
            }
        }

        // ── Load incomes ───────────────────────────────────────────────────
        if (type is "income" or "both")
        {
            foreach (var project in scope.SelectedProjects)
            {
                var rows = await _incomeRepo.GetDetailedByProjectIdAsync(project.PrjId, query.From, query.To, ct);

                foreach (var i in rows)
                {
                    // category filter
                    if (!string.IsNullOrWhiteSpace(query.CategoryName) &&
                        !ContainsText(i.Category?.CatName, query.CategoryName))
                        continue;

                    // payment method filter
                    if (!string.IsNullOrWhiteSpace(query.PaymentMethodName) &&
                        !ContainsText(i.PaymentMethod?.PmtName, query.PaymentMethodName))
                        continue;

                    // partner filter
                    if (!string.IsNullOrWhiteSpace(query.PartnerName))
                    {
                        var hasPartner = i.Splits.Any(s =>
                            ContainsText(s.Partner?.PtrName, query.PartnerName));
                        if (!hasPartner) continue;
                    }

                    // search
                    if (!string.IsNullOrWhiteSpace(query.Search) &&
                        !ContainsText(i.IncTitle, query.Search) &&
                        !ContainsText(i.IncDescription, query.Search))
                        continue;

                    incomeItems.Add(new McpMovementItemResponse
                    {
                        Type              = "income",
                        Title             = i.IncTitle,
                        Description       = i.IncDescription,
                        Date              = i.IncIncomeDate,
                        Amount            = i.IncConvertedAmount,
                        Currency          = project.PrjCurrencyCode,
                        OriginalAmount    = i.IncOriginalAmount,
                        OriginalCurrency  = i.IncOriginalCurrency,
                        ProjectName       = project.PrjName,
                        CategoryName      = i.Category?.CatName ?? "Unknown",
                        PaymentMethodName = i.PaymentMethod?.PmtName ?? "Unknown",
                        HasSplits         = i.Splits.Count > 0,
                        SplitPartners     = i.Splits
                            .Select(s => s.Partner?.PtrName)
                            .Where(n => !string.IsNullOrWhiteSpace(n))
                            .Select(n => n!)
                            .ToList()
                    });
                }
            }
        }

        // ── Merge, sort by date desc, take top N ───────────────────────────
        var all = expenseItems
            .Concat(incomeItems)
            .OrderByDescending(m => m.Date)
            .ToList();

        var totalCount = all.Count;
        var items      = all.Take(top).ToList();

        return new McpRecentMovementsResponse
        {
            TotalCount = totalCount,
            SearchNote = scope.SearchNote,
            Items      = items
        };
    }
}
