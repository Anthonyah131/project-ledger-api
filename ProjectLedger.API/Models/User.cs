namespace ProjectLedger.API.Models;

/// <summary>
/// Usuario del sistema. Nuevo usuario inicia desactivado (UsrIsActive = false).
/// Soporta autenticación local (password hash) y/o OAuth externo.
/// </summary>
public class User
{
    public Guid UsrId { get; set; }
    public string UsrEmail { get; set; } = null!;
    public string? UsrPasswordHash { get; set; }
    public string UsrFullName { get; set; } = null!;
    public Guid UsrPlanId { get; set; }
    public bool UsrIsActive { get; set; }
    public bool UsrIsAdmin { get; set; }
    public string? UsrAvatarUrl { get; set; }
    public DateTime? UsrLastLoginAt { get; set; }
    public DateTime UsrCreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UsrUpdatedAt { get; set; } = DateTime.UtcNow;

    // ── Soft delete ─────────────────────────────────────────
    public bool UsrIsDeleted { get; set; }
    public DateTime? UsrDeletedAt { get; set; }
    public Guid? UsrDeletedByUserId { get; set; }

    // ── Navigation properties ───────────────────────────────
    public Plan Plan { get; set; } = null!;
    public User? DeletedByUser { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<ExternalAuthProvider> ExternalAuthProviders { get; set; } = [];
    public ICollection<Project> OwnedProjects { get; set; } = [];
    public ICollection<ProjectMember> ProjectMemberships { get; set; } = [];
    public ICollection<PaymentMethod> PaymentMethods { get; set; } = [];
    public ICollection<Expense> CreatedExpenses { get; set; } = [];
    public ICollection<Obligation> CreatedObligations { get; set; } = [];
    public ICollection<AuditLog> AuditLogs { get; set; } = [];
}
