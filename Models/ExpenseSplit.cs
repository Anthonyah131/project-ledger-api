namespace ProjectLedger.API.Models;

/// <summary>
/// División del costo de un gasto entre partners del proyecto.
/// Por defecto se crea un split 100% al partner dueño del método de pago.
/// </summary>
public class ExpenseSplit
{
    public Guid ExsId { get; set; }
    public Guid ExsExpenseId { get; set; }
    public Guid ExsPartnerId { get; set; }
    public string ExsSplitType { get; set; } = null!;      // 'percentage' | 'fixed'
    public decimal ExsSplitValue { get; set; }
    public decimal ExsResolvedAmount { get; set; }         // Siempre en moneda original del gasto
    public DateTime ExsCreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExsUpdatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation properties ───────────────────────────────
    public Expense Expense { get; set; } = null!;
    public Partner Partner { get; set; } = null!;
    public ICollection<SplitCurrencyExchange> CurrencyExchanges { get; set; } = [];
}
