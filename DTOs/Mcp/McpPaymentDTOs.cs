namespace ProjectLedger.API.DTOs.Mcp;

public class McpPaymentObligationItemResponse
{
    public Guid ObligationId { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public string Title { get; set; } = null!;
    public DateOnly? DueDate { get; set; }
    public int? DaysUntilDue { get; set; }
    public int? DaysOverdue { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public string Currency { get; set; } = null!;
    public string Status { get; set; } = null!;
}

public class McpReceivedPaymentItemResponse
{
    public Guid IncomeId { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public Guid PaymentMethodId { get; set; }
    public string PaymentMethodName { get; set; } = null!;
    public DateOnly IncomeDate { get; set; }
    public string Title { get; set; } = null!;
    public decimal OriginalAmount { get; set; }
    public string OriginalCurrency { get; set; } = null!;
    public decimal ConvertedAmount { get; set; }
}

public class McpPaymentMethodUsageResponse
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string Direction { get; set; } = null!;
    public string? SearchNote { get; set; }
    public List<McpPaymentMethodUsageItemResponse> Items { get; set; } = [];
}

public class McpPaymentMethodUsageItemResponse
{
    public Guid PaymentMethodId { get; set; }
    public string PaymentMethodName { get; set; } = null!;
    public string PaymentMethodType { get; set; } = null!;
    public decimal TotalOutgoing { get; set; }
    public decimal TotalIncoming { get; set; }
    public decimal NetFlow { get; set; }
    public int ExpenseCount { get; set; }
    public int IncomeCount { get; set; }
    public decimal UsagePercentage { get; set; }
}
