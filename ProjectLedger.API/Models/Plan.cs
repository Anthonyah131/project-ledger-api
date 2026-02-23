namespace ProjectLedger.API.Models;

/// <summary>
/// Plan de suscripción del sistema (free, pro, enterprise, etc.).
/// Define permisos y límites para los usuarios asignados.
/// </summary>
public class Plan
{
    public Guid PlnId { get; set; }
    public string PlnName { get; set; } = null!;
    public string PlnSlug { get; set; } = null!;
    public string? PlnDescription { get; set; }
    public bool PlnIsActive { get; set; } = true;
    public int PlnDisplayOrder { get; set; }

    // ── Permisos (Features) ─────────────────────────────────
    public bool PlnCanCreateProjects { get; set; } = true;
    public bool PlnCanEditProjects { get; set; } = true;
    public bool PlnCanDeleteProjects { get; set; } = true;
    public bool PlnCanShareProjects { get; set; } = true;
    public bool PlnCanExportData { get; set; }
    public bool PlnCanUseAdvancedReports { get; set; }
    public bool PlnCanUseOcr { get; set; }
    public bool PlnCanUseApi { get; set; }
    public bool PlnCanUseMultiCurrency { get; set; } = true;
    public bool PlnCanSetBudgets { get; set; } = true;

    // ── Límites numéricos (JSONB) ───────────────────────────
    public string? PlnLimits { get; set; }  // Almacenado como JSONB en DB

    public DateTime PlnCreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime PlnUpdatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation collections ──────────────────────────────
    public ICollection<User> Users { get; set; } = [];
}
