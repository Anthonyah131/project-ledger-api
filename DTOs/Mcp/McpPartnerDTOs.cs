namespace ProjectLedger.API.DTOs.Mcp;

// ── GET /api/mcp/partners/balances ───────────────────────────────────────────

public class McpPartnerBalancesResponse
{
    public Guid? ProjectId { get; set; }
    public string? SearchNote { get; set; }

    /// <summary>Un elemento por cada proyecto que tenga al menos un partner con actividad.</summary>
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

    /// <summary>Cuánto le deben otros partners a éste (antes de liquidaciones).</summary>
    public decimal OthersOweHim { get; set; }

    /// <summary>Cuánto debe éste partner a otros (antes de liquidaciones).</summary>
    public decimal HeOwesOthers { get; set; }

    /// <summary>Total de liquidaciones recibidas por este partner.</summary>
    public decimal SettlementsReceived { get; set; }

    /// <summary>Total de liquidaciones pagadas por este partner.</summary>
    public decimal SettlementsPaid { get; set; }

    /// <summary>
    /// Balance neto en la moneda base del proyecto.
    /// Positivo = le deben dinero a este partner.
    /// Negativo = este partner debe dinero a otros.
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
