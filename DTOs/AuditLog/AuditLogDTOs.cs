namespace ProjectLedger.API.DTOs.AuditLog;

// ── Responses ───────────────────────────────────────────────
// AuditLog es inmutable → NO tiene requests de creación/modificación.
// La creación se hace internamente desde los servicios.

/// <summary>Respuesta con los datos de un registro de auditoría.</summary>
public class AuditLogResponse
{
    public Guid Id { get; set; }
    public string EntityName { get; set; } = null!;
    public Guid EntityId { get; set; }
    public string ActionType { get; set; } = null!;
    public Guid PerformedByUserId { get; set; }
    public string? PerformedByUserName { get; set; }
    public DateTime PerformedAt { get; set; }
    public object? OldValues { get; set; }                      // JSON deserializado
    public object? NewValues { get; set; }                      // JSON deserializado
}
