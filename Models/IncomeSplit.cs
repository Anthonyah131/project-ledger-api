namespace ProjectLedger.API.Models;

/// <summary>
/// División de un ingreso entre partners del proyecto.
/// Por defecto se crea un split 100% al partner dueño del método de pago.
/// </summary>
public class IncomeSplit
{
    public Guid InsId { get; set; }
    public Guid InsIncomeId { get; set; }
    public Guid InsPartnerId { get; set; }
    public string InsSplitType { get; set; } = null!;      // 'percentage' | 'fixed'
    public decimal InsSplitValue { get; set; }
    public decimal InsResolvedAmount { get; set; }         // Siempre en moneda original del ingreso
    public DateTime InsCreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime InsUpdatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation properties ───────────────────────────────
    public Income Income { get; set; } = null!;
    public Partner Partner { get; set; } = null!;
    public ICollection<SplitCurrencyExchange> CurrencyExchanges { get; set; } = [];
}
