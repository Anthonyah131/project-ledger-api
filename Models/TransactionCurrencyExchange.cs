namespace ProjectLedger.API.Models;

/// <summary>
/// Conversion of a transaction (expense or income) to an alternative currency.
/// Uses explicit FKs: tce_expense_id XOR tce_income_id must be NOT NULL.
/// </summary>
public class TransactionCurrencyExchange
{
    public Guid TceId { get; set; }
    public Guid? TceExpenseId { get; set; }                    // FK → expenses  (mutex con TceIncomeId)
    public Guid? TceIncomeId { get; set; }                     // FK → incomes   (mutex con TceExpenseId)
    public string TceCurrencyCode { get; set; } = null!;       // ISO 4217
    public decimal TceExchangeRate { get; set; }
    public decimal TceConvertedAmount { get; set; }
    public DateTime TceCreatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation properties ───────────────────────────────
    public Expense? Expense { get; set; }
    public Income? Income { get; set; }
    public Currency Currency { get; set; } = null!;
}
