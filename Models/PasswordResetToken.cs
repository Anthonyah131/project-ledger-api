namespace ProjectLedger.API.Models;

/// <summary>
/// One-time OTP token for the password reset flow.
/// The code is never stored in plain text; only its SHA-256 hash.
/// </summary>
public class PasswordResetToken
{
    public Guid PrtId { get; set; }
    public Guid PrtUserId { get; set; }
    public string PrtCodeHash { get; set; } = null!;
    public DateTime PrtExpiresAt { get; set; }
    public DateTime? PrtUsedAt { get; set; }
    public DateTime PrtCreatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation properties ───────────────────────────────
    public User User { get; set; } = null!;
}
