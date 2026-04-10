namespace ProjectLedger.API.DTOs.AuditLog;

// ── Responses ───────────────────────────────────────────────
// AuditLog is immutable → It does NOT have creation/modification requests.
// Creation is handled internally by the services.

/// <summary>Response containing the data of an audit log record.</summary>
public class AuditLogResponse
{
    /// <summary>Unique identifier of the audit log.</summary>
    public Guid Id { get; set; }
    
    /// <summary>Name of the affected entity (e.g., User, Project, Expense).</summary>
    public string EntityName { get; set; } = null!;
    
    /// <summary>Unique identifier of the affected entity.</summary>
    public Guid EntityId { get; set; }
    
    /// <summary>Type of action performed (e.g., Create, Update, Delete).</summary>
    public string ActionType { get; set; } = null!;
    
    /// <summary>ID of the user who performed the action.</summary>
    public Guid PerformedByUserId { get; set; }
    
    /// <summary>Name of the user who performed the action.</summary>
    public string? PerformedByUserName { get; set; }
    
    /// <summary>Timestamp when the action was performed (UTC).</summary>
    public DateTime PerformedAt { get; set; }
    
    /// <summary>JSON object representing the entity state before the action (if applicable).</summary>
    public object? OldValues { get; set; }                      // Deserialized JSON
    
    /// <summary>JSON object representing the entity state after the action (if applicable).</summary>
    public object? NewValues { get; set; }                      // Deserialized JSON
}
