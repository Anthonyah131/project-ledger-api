namespace ProjectLedger.API.Models;

/// <summary>
/// Conversión de un split a una moneda alternativa del proyecto.
/// Mutex: sce_expense_split_id XOR sce_income_split_id debe ser NOT NULL.
/// Permite que cada split muestre su monto en las monedas configuradas del proyecto.
/// </summary>
public class SplitCurrencyExchange
{
    public Guid SceId { get; set; }
    public Guid? SceExpenseSplitId { get; set; }           // FK → expense_splits  (mutex con SceIncomeSplitId)
    public Guid? SceIncomeSplitId { get; set; }            // FK → income_splits   (mutex con SceExpenseSplitId)
    public string SceCurrencyCode { get; set; } = null!;   // ISO 4217
    public decimal SceExchangeRate { get; set; }
    public decimal SceConvertedAmount { get; set; }        // Monto del split en esta moneda
    public DateTime SceCreatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation properties ───────────────────────────────
    public ExpenseSplit? ExpenseSplit { get; set; }
    public IncomeSplit? IncomeSplit { get; set; }
    public Currency Currency { get; set; } = null!;
}
