namespace ProjectLedger.API.Models;

/// <summary>
/// User's financial contact.
/// Can represent the user themselves, a partner, family member, etc.
/// Owns payment methods and can be assigned to projects.
/// </summary>
public class Partner
{
    public Guid PtrId { get; set; }
    public Guid PtrOwnerUserId { get; set; }
    public string PtrName { get; set; } = null!;
    public string? PtrEmail { get; set; }
    public string? PtrPhone { get; set; }
    public string? PtrNotes { get; set; }
    public DateTime PtrCreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime PtrUpdatedAt { get; set; } = DateTime.UtcNow;

    // ── Soft delete ─────────────────────────────────────────
    public bool PtrIsDeleted { get; set; }
    public DateTime? PtrDeletedAt { get; set; }
    public Guid? PtrDeletedByUserId { get; set; }

    // ── Navigation properties ───────────────────────────────
    public User OwnerUser { get; set; } = null!;
    public User? DeletedByUser { get; set; }

    public ICollection<PaymentMethod> PaymentMethods { get; set; } = [];
}
