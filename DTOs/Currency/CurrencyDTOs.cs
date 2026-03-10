namespace ProjectLedger.API.DTOs.Currency;

// ── Responses ───────────────────────────────────────────────

/// <summary>Respuesta pública de una moneda del catálogo.</summary>
public class CurrencyResponse
{
    public string Code { get; set; } = null!;               // ISO 4217
    public string Name { get; set; } = null!;
    public string Symbol { get; set; } = null!;
    public short DecimalPlaces { get; set; }
    public bool IsActive { get; set; }
}
