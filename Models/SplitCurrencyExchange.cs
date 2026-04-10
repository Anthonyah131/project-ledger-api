namespace ProjectLedger.API.Models;

/// <summary>
/// Conversion of a split to an alternative project currency.
/// Mutex: sce_expense_split_id XOR sce_income_split_id must be NOT NULL.
/// Allows each split to show its amount in configured project currencies.
/// </summary>
public class SplitCurrencyExchange
{
    public Guid SceId { get; set; }
    public Guid? SceExpenseSplitId { get; set; }           // FK → expense_splits  (XOR mutex)
    public Guid? SceIncomeSplitId { get; set; }            // FK → income_splits   (XOR mutex)
    public Guid? SceSettlementId { get; set; }             // FK → partner_settlements (XOR mutex)
    public string SceCurrencyCode { get; set; } = null!;   // ISO 4217
    public decimal SceExchangeRate { get; set; }
    public decimal SceConvertedAmount { get; set; }        // Amount in this currency
    public DateTime SceCreatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation properties ───────────────────────────────
    public ExpenseSplit? ExpenseSplit { get; set; }
    public IncomeSplit? IncomeSplit { get; set; }
    public PartnerSettlement? Settlement { get; set; }
    public Currency Currency { get; set; } = null!;
}
