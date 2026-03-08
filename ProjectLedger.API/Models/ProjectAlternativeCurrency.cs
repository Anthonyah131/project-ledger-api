namespace ProjectLedger.API.Models;

/// <summary>
/// Moneda alternativa configurada para un proyecto (visualización).
/// Cada proyecto puede tener 0..N monedas alternativas según el plan del owner.
/// </summary>
public class ProjectAlternativeCurrency
{
    public Guid PacId { get; set; }
    public Guid PacProjectId { get; set; }
    public string PacCurrencyCode { get; set; } = null!;       // ISO 4217
    public DateTime PacCreatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation properties ───────────────────────────────
    public Project Project { get; set; } = null!;
    public Currency Currency { get; set; } = null!;
}
