namespace ProjectLedger.API.DTOs.Mcp;

public class McpRecentMovementsResponse
{
    public int TotalCount { get; set; }
    public string? SearchNote { get; set; }
    public List<McpMovementItemResponse> Items { get; set; } = [];
}

public class McpMovementItemResponse
{
    /// <summary>"expense" or "income"</summary>
    public string Type { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = null!;
    public decimal OriginalAmount { get; set; }
    public string OriginalCurrency { get; set; } = null!;
    public string ProjectName { get; set; } = null!;
    public string CategoryName { get; set; } = null!;
    public string PaymentMethodName { get; set; } = null!;
    public bool HasSplits { get; set; }
    /// <summary>Names of partners participating in the split.</summary>
    public List<string> SplitPartners { get; set; } = [];
}
