namespace ProjectLedger.API.Models;

/// <summary>
/// Catalog of enabled currencies (ISO 4217).
/// Natural PK: 3-character ISO code.
/// </summary>
public class Currency
{
    public string CurCode { get; set; } = null!;               // PK · ISO 4217 (e.g. "USD", "CRC")
    public string CurName { get; set; } = null!;               // Full name
    public string CurSymbol { get; set; } = null!;             // Display symbol
    public short CurDecimalPlaces { get; set; } = 2;           // Standard decimals
    public bool CurIsActive { get; set; } = true;              // Available currency?
    public DateTime CurCreatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation collections ──────────────────────────────
    public ICollection<Project> ProjectsWithCurrency { get; set; } = [];
    public ICollection<PaymentMethod> PaymentMethodsWithCurrency { get; set; } = [];
    public ICollection<Expense> ExpensesOriginalCurrency { get; set; } = [];
    public ICollection<Obligation> ObligationsWithCurrency { get; set; } = [];
    public ICollection<Income> IncomesOriginalCurrency { get; set; } = [];
    public ICollection<ProjectAlternativeCurrency> ProjectAlternativeCurrencies { get; set; } = [];
    public ICollection<TransactionCurrencyExchange> TransactionCurrencyExchanges { get; set; } = [];
    public ICollection<SplitCurrencyExchange> SplitCurrencyExchanges { get; set; } = [];
}
