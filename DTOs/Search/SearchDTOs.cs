namespace ProjectLedger.API.DTOs.Search;

// ── Responses ────────────────────────────────────────────────

public class GlobalSearchResponse
{
    public IReadOnlyList<ExpenseSearchResult> Expenses { get; set; } = [];
    public IReadOnlyList<IncomeSearchResult> Incomes { get; set; } = [];
}

public class ExpenseSearchResult
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = null!;
    public DateOnly Date { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public string CategoryName { get; set; } = null!;
}

public class IncomeSearchResult
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = null!;
    public DateOnly Date { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public string CategoryName { get; set; } = null!;
}
