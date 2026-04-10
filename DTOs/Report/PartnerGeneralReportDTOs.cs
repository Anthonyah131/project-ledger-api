using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.DTOs.Partner;

namespace ProjectLedger.API.DTOs.Report;

// ── Partner General Report ───────────────────────────────────

/// <summary>
/// General partner report: consolidated activity by project and payment method.
/// Amounts per project are in each project's base currency.
/// Amounts per payment method are in the payment method's currency.
/// </summary>
public class PartnerGeneralReportResponse
{
    public Guid PartnerId { get; set; }
    public string PartnerName { get; set; } = null!;
    public string? PartnerEmail { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public DateTime GeneratedAt { get; set; }

    public List<PartnerProjectSummary> Projects { get; set; } = [];
    public List<PartnerPaymentMethodSummary> PaymentMethods { get; set; } = [];
}

/// <summary>
/// Partner activity in a specific project.
/// All amounts are in the project's base currency.
/// Alternative currencies are shown per transaction only for reference.
/// </summary>
public class PartnerProjectSummary
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;

    // Balances in project's base currency (same logic as /partners/balance)
    public decimal PaidPhysically { get; set; }
    public decimal OthersOweHim { get; set; }
    public decimal HeOwesOthers { get; set; }
    public decimal SettlementsReceived { get; set; }
    public decimal SettlementsPaid { get; set; }
    public decimal NetBalance { get; set; }

    /// <summary>Same values expressed in the project's alternative currencies.</summary>
    public List<PartnerCurrencyTotal> CurrencyTotals { get; set; } = [];

    public List<PartnerProjectTransaction> Transactions { get; set; } = [];
    public List<PartnerProjectSettlement> Settlements { get; set; } = [];
}

/// <summary>
/// A transaction (expense or income) where the partner has a split.
/// SplitAmount is in the project's base currency.
/// CurrencyExchanges are the split's alternative amounts — read-only.
/// </summary>
public class PartnerProjectTransaction
{
    public Guid TransactionId { get; set; }
    public string Type { get; set; } = null!;           // "expense" | "income"
    public string Title { get; set; } = null!;
    public DateOnly Date { get; set; }
    public string? Category { get; set; }
    public string? PaymentMethodName { get; set; }
    public string? PayingPartnerName { get; set; }
    public decimal SplitAmount { get; set; }             // in project's base currency
    public string SplitType { get; set; } = null!;
    public decimal SplitValue { get; set; }
    public List<SplitCurrencyExchangeItem> CurrencyExchanges { get; set; } = [];
}

public class PartnerProjectSettlement
{
    public Guid SettlementId { get; set; }
    public DateOnly Date { get; set; }
    public string Direction { get; set; } = null!;       // "paid_to" | "received_from"
    public string OtherPartnerName { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = null!;
    public decimal ConvertedAmount { get; set; }         // in project's base currency
    public List<CurrencyExchangeResponse> CurrencyExchanges { get; set; } = [];
}

/// <summary>
/// Partner activity through a payment method.
/// All amounts are in the payment method's currency.
/// </summary>
public class PartnerPaymentMethodSummary
{
    public Guid PaymentMethodId { get; set; }
    public string PaymentMethodName { get; set; } = null!;
    public string Currency { get; set; } = null!;
    public string? BankName { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal TotalIncomes { get; set; }
    public decimal NetFlow { get; set; }                 // TotalIncomes - TotalExpenses
    public int TransactionCount { get; set; }
}
