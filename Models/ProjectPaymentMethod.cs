namespace ProjectLedger.API.Models;

/// <summary>
/// Linking of a payment method to a project.
/// Allows members of a shared project to use linked payment methods.
/// No soft delete: it is physically deleted when unlinked.
/// </summary>
public class ProjectPaymentMethod
{
    public Guid PpmId { get; set; }
    public Guid PpmProjectId { get; set; }
    public Guid PpmPaymentMethodId { get; set; }
    public Guid PpmAddedByUserId { get; set; }
    public DateTime PpmCreatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation properties ───────────────────────────────
    public Project Project { get; set; } = null!;
    public PaymentMethod PaymentMethod { get; set; } = null!;
    public User AddedByUser { get; set; } = null!;
}
