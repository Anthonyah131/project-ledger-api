namespace ProjectLedger.API.Models;

/// <summary>
/// Registro de auditoría inmutable.
/// Almacena snapshots JSONB del estado anterior y nuevo de cada entidad.
/// </summary>
public class AuditLog
{
    public Guid AudId { get; set; }
    public string AudEntityName { get; set; } = null!;         // ej: "expenses", "obligations"
    public Guid AudEntityId { get; set; }
    public string AudActionType { get; set; } = null!;         // 'create', 'update', 'delete', 'status_change', 'associate'
    public Guid AudPerformedByUserId { get; set; }
    public DateTime AudPerformedAt { get; set; } = DateTime.UtcNow;
    public string? AudOldValues { get; set; }                  // JSONB
    public string? AudNewValues { get; set; }                  // JSONB

    // ── Navigation properties ───────────────────────────────
    public User PerformedByUser { get; set; } = null!;
}
