namespace ProjectLedger.API.Models;

/// <summary>
/// Catálogo de monedas habilitadas (ISO 4217).
/// PK natural: código ISO de 3 caracteres.
/// </summary>
public class Currency
{
    public string CurCode { get; set; } = null!;               // PK · ISO 4217 (ej: "USD", "CRC")
    public string CurName { get; set; } = null!;               // Nombre completo
    public string CurSymbol { get; set; } = null!;             // Símbolo de visualización
    public short CurDecimalPlaces { get; set; } = 2;           // Decimales estándar
    public bool CurIsActive { get; set; } = true;              // ¿Moneda disponible?
    public DateTime CurCreatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation collections ──────────────────────────────
    public ICollection<Project> ProjectsWithCurrency { get; set; } = [];
    public ICollection<PaymentMethod> PaymentMethodsWithCurrency { get; set; } = [];
    public ICollection<Expense> ExpensesOriginalCurrency { get; set; } = [];
    public ICollection<Expense> ExpensesAltCurrency { get; set; } = [];
    public ICollection<Obligation> ObligationsWithCurrency { get; set; } = [];
}
