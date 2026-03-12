namespace ProjectLedger.API.DTOs.Mcp;

public class McpExpenseTotalsResponse
{
    public Guid? ProjectId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public decimal TotalSpent { get; set; }
    public int TransactionCount { get; set; }
    public decimal AverageExpense { get; set; }
    public decimal? PreviousPeriodTotal { get; set; }
    public decimal? DeltaAmount { get; set; }
    public decimal? DeltaPercentage { get; set; }
    public string? SearchNote { get; set; }
}

public class McpExpenseByCategoryResponse
{
    public Guid? ProjectId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public decimal TotalSpent { get; set; }
    public string? SearchNote { get; set; }
    public List<McpExpenseByCategoryItemResponse> Items { get; set; } = [];
}

public class McpExpenseByCategoryItemResponse
{
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public int ExpenseCount { get; set; }
    public decimal Percentage { get; set; }
    public decimal? TrendDelta { get; set; }
}

public class McpExpenseByProjectResponse
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public decimal TotalSpent { get; set; }
    public List<McpExpenseByProjectItemResponse> Items { get; set; } = [];
}

public class McpExpenseByProjectItemResponse
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;
    public decimal TotalSpent { get; set; }
    public int ExpenseCount { get; set; }
    public decimal? Budget { get; set; }
    public decimal? BudgetUsedPercentage { get; set; }
}

public class McpExpenseTrendsResponse
{
    public Guid? ProjectId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string Granularity { get; set; } = null!;
    public string? SearchNote { get; set; }
    public List<McpExpenseTrendPointResponse> Points { get; set; } = [];
}

public class McpExpenseTrendPointResponse
{
    public DateOnly PeriodStart { get; set; }
    public string PeriodLabel { get; set; } = null!;
    public decimal TotalSpent { get; set; }
    public int ExpenseCount { get; set; }
}
