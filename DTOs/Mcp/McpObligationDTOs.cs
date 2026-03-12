namespace ProjectLedger.API.DTOs.Mcp;

public class McpObligationItemResponse
{
    public Guid ObligationId { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public string Title { get; set; } = null!;
    public DateOnly? DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public string Currency { get; set; } = null!;
    public string Status { get; set; } = null!;
    public int? DaysUntilDue { get; set; }
    public int? DaysOverdue { get; set; }
}
