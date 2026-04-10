namespace ProjectLedger.API.DTOs.Mcp;

// ── GET /api/mcp/partners/balances ───────────────────────────────────────────

public class McpPartnerBalancesResponse
{
    public Guid? ProjectId { get; set; }
    public string? SearchNote { get; set; }

    /// <summary>One item for each project that has at least one partner with activity.</summary>
    public List<McpPartnerBalancesProjectResponse> Projects { get; set; } = [];
}

public class McpPartnerBalancesProjectResponse
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public string Currency { get; set; } = null!;
    public List<McpPartnerBalanceItemResponse> Partners { get; set; } = [];
}

public class McpPartnerBalanceItemResponse
{
    public Guid PartnerId { get; set; }
    public string PartnerName { get; set; } = null!;

    /// <summary>Amount owed by other partners to this one (before settlements).</summary>
    public decimal OthersOweHim { get; set; }

    /// <summary>Amount this partner owes to others (before settlements).</summary>
    public decimal HeOwesOthers { get; set; }

    /// <summary>Total settlements received by this partner.</summary>
    public decimal SettlementsReceived { get; set; }

    /// <summary>Total settlements paid by this partner.</summary>
    public decimal SettlementsPaid { get; set; }

    /// <summary>
    /// Net balance in the project's base currency.
    /// Positive = money is owed to this partner.
    /// Negative = this partner owes money to others.
    /// </summary>
    public decimal NetBalance { get; set; }
}

// ── GET /api/mcp/partners/settlements ───────────────────────────────────────

public class McpPartnerSettlementItemResponse
{
    public Guid SettlementId { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public string FromPartnerName { get; set; } = null!;
    public string ToPartnerName { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = null!;
    public decimal ConvertedAmount { get; set; }
    public DateOnly SettlementDate { get; set; }
    public string? Description { get; set; }
}
