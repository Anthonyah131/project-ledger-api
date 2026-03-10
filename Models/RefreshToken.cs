namespace ProjectLedger.API.Models;

/// <summary>
/// Refresh token para JWT authentication.
/// El token real nunca se almacena: solo su hash SHA-256.
/// </summary>
public class RefreshToken
{
    public Guid RtkId { get; set; }
    public Guid RtkUserId { get; set; }
    public string RtkTokenHash { get; set; } = null!;
    public DateTime RtkExpiresAt { get; set; }
    public DateTime? RtkRevokedAt { get; set; }
    public DateTime RtkCreatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation properties ───────────────────────────────
    public User User { get; set; } = null!;
}
