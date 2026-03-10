namespace ProjectLedger.API.Models;

/// <summary>
/// Token OTP de un solo uso para el flujo de restablecimiento de contraseña.
/// El código nunca se almacena en texto plano; solo su hash SHA-256.
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
