namespace ProjectLedger.API.Models;

/// <summary>
/// Division of an income among project partners.
/// By default, a 100% split is created to the partner who owns the payment method.
/// </summary>
public class IncomeSplit
{
    public Guid InsId { get; set; }
    public Guid InsIncomeId { get; set; }
    public Guid InsPartnerId { get; set; }
    public string InsSplitType { get; set; } = null!;      // 'percentage' | 'fixed'
    public decimal InsSplitValue { get; set; }
    public decimal InsResolvedAmount { get; set; }         // Always in the original currency of the income
    public DateTime InsCreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime InsUpdatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation properties ───────────────────────────────
    public Income Income { get; set; } = null!;
    public Partner Partner { get; set; } = null!;
    public ICollection<SplitCurrencyExchange> CurrencyExchanges { get; set; } = [];
}
