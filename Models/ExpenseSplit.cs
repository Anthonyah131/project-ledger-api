namespace ProjectLedger.API.Models;

/// <summary>
/// Division of the cost of an expense among project partners.
/// By default, a 100% split is created to the partner who owns the payment method.
/// </summary>
public class ExpenseSplit
{
    public Guid ExsId { get; set; }
    public Guid ExsExpenseId { get; set; }
    public Guid ExsPartnerId { get; set; }
    public string ExsSplitType { get; set; } = null!;      // 'percentage' | 'fixed'
    public decimal ExsSplitValue { get; set; }
    public decimal ExsResolvedAmount { get; set; }         // Always in the original currency of the expense
    public DateTime ExsCreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExsUpdatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation properties ───────────────────────────────
    public Expense Expense { get; set; } = null!;
    public Partner Partner { get; set; } = null!;
    public ICollection<SplitCurrencyExchange> CurrencyExchanges { get; set; } = [];
}
