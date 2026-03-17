namespace ProjectLedger.API.Models;

/// <summary>
/// Partner asignado a un proyecto.
/// Los métodos de pago disponibles en el proyecto se derivan de los partners asignados.
/// </summary>
public class ProjectPartner
{
    public Guid PtpId { get; set; }
    public Guid PtpProjectId { get; set; }
    public Guid PtpPartnerId { get; set; }
    public Guid PtpAddedByUserId { get; set; }
    public DateTime PtpCreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime PtpUpdatedAt { get; set; } = DateTime.UtcNow;

    // ── Soft delete ─────────────────────────────────────────
    public bool PtpIsDeleted { get; set; }
    public DateTime? PtpDeletedAt { get; set; }
    public Guid? PtpDeletedByUserId { get; set; }

    // ── Navigation properties ───────────────────────────────
    public Project Project { get; set; } = null!;
    public Partner Partner { get; set; } = null!;
    public User AddedByUser { get; set; } = null!;
    public User? DeletedByUser { get; set; }
}
