namespace ProjectLedger.API.Models;

/// <summary>
/// Vínculo OAuth con proveedores externos (Google, Microsoft, GitHub, etc.).
/// Un usuario puede tener múltiples proveedores vinculados.
/// </summary>
public class ExternalAuthProvider
{
    public Guid EapId { get; set; }
    public Guid EapUserId { get; set; }
    public string EapProvider { get; set; } = null!;           // 'google', 'microsoft', 'github', etc.
    public string EapProviderUserId { get; set; } = null!;
    public string? EapProviderEmail { get; set; }
    public string? EapAccessTokenHash { get; set; }
    public string? EapRefreshTokenHash { get; set; }
    public DateTime? EapTokenExpiresAt { get; set; }
    public string? EapMetadata { get; set; }                   // JSONB
    public DateTime EapCreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime EapUpdatedAt { get; set; } = DateTime.UtcNow;

    // ── Soft delete ─────────────────────────────────────────
    public bool EapIsDeleted { get; set; }
    public DateTime? EapDeletedAt { get; set; }
    public Guid? EapDeletedByUserId { get; set; }

    // ── Navigation properties ───────────────────────────────
    public User User { get; set; } = null!;
    public User? DeletedByUser { get; set; }
}
