namespace ProjectLedger.API.DTOs.Mcp;

public class McpFinancialHealthResponse
{
    public Guid? ProjectId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int Score { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal NetBalance { get; set; }
    public decimal BurnRatePerDay { get; set; }
    public int BudgetRiskProjects { get; set; }
    public int OverdueObligationsCount { get; set; }
    public string? SearchNote { get; set; }
    public List<string> KeySignals { get; set; } = [];
}

public class McpMonthlyOverviewResponse
{
    public string Month { get; set; } = null!;
    public Guid? ProjectId { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public DateTime GeneratedAtUtc { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal NetBalance { get; set; }
    public int ExpenseCount { get; set; }
    public int IncomeCount { get; set; }
    public string? SearchNote { get; set; }
    public List<McpExpenseByCategoryItemResponse> TopCategories { get; set; } = [];
    public List<McpPaymentMethodUsageItemResponse> PaymentMethodSplit { get; set; } = [];
    public List<McpProjectHealthItemResponse> ProjectHealth { get; set; } = [];
    public List<McpAlertResponse> Alerts { get; set; } = [];
}

public class McpProjectHealthItemResponse
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public decimal Spent { get; set; }
    public decimal Income { get; set; }
    public decimal Net { get; set; }
    public decimal? Budget { get; set; }
    public decimal? BudgetUsedPercentage { get; set; }
}

public class McpAlertsResponse
{
    public string? Month { get; set; }
    public Guid? ProjectId { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public string? SearchNote { get; set; }
    public List<McpAlertResponse> Items { get; set; } = [];
}

public class McpAlertResponse
{
    public string Code { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string Message { get; set; } = null!;
    public int Priority { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? ObligationId { get; set; }
}
