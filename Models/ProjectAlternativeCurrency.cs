namespace ProjectLedger.API.Models;

/// <summary>
/// Alternative currency configured for a project (display).
/// Each project can have 0..N alternative currencies depending on the owner's plan.
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
