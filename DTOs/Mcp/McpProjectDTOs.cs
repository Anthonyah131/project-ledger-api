namespace ProjectLedger.API.DTOs.Mcp;

public class McpProjectPortfolioItemResponse
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public string UserRole { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastActivityAtUtc { get; set; }
    public DateOnly? NextDeadline { get; set; }
    public string Status { get; set; } = null!;
    public decimal ProgressPercent { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal NetBalance { get; set; }
    public decimal? BudgetUsedPercentage { get; set; }
    public int OpenObligations { get; set; }
    public int OverdueObligations { get; set; }
}

public class McpProjectDeadlineItemResponse
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public string DeadlineType { get; set; } = "obligation_due";
    public Guid ObligationId { get; set; }
    public string Title { get; set; } = null!;
    public DateOnly DueDate { get; set; }
    public int DaysUntilDue { get; set; }
    public decimal RemainingAmount { get; set; }
    public string Currency { get; set; } = null!;
    public string Status { get; set; } = null!;
}

public class McpProjectActivitySplitResponse
{
    public int ActiveCount { get; set; }
    public int CompletedCount { get; set; }
    public int AtRiskCount { get; set; }
    public int InactiveCount { get; set; }
    public string? SearchNote { get; set; }
    public List<McpProjectActivityItemResponse> Items { get; set; } = [];
}

public class McpProjectActivityItemResponse
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public string Status { get; set; } = null!;
}
