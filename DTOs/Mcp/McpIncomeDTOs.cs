namespace ProjectLedger.API.DTOs.Mcp;

public class McpIncomeByPeriodResponse
{
    public Guid? ProjectId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string Granularity { get; set; } = null!;
    public decimal TotalIncome { get; set; }
    public int IncomeCount { get; set; }
    public decimal? PreviousPeriodTotal { get; set; }
    public decimal? DeltaAmount { get; set; }
    public decimal? DeltaPercentage { get; set; }
    public string? SearchNote { get; set; }
    public List<McpIncomePeriodPointResponse> Points { get; set; } = [];
}

public class McpIncomePeriodPointResponse
{
    public DateOnly PeriodStart { get; set; }
    public string PeriodLabel { get; set; } = null!;
    public decimal TotalIncome { get; set; }
    public int IncomeCount { get; set; }
}

public class McpIncomeByProjectResponse
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public decimal TotalIncome { get; set; }
    public List<McpIncomeByProjectItemResponse> Items { get; set; } = [];
}

public class McpIncomeByProjectItemResponse
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;
    public decimal TotalIncome { get; set; }
    public int IncomeCount { get; set; }
}
