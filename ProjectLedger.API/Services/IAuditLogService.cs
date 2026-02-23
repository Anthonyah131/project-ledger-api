using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

public interface IAuditLogService
{
    Task LogAsync(string entityName, Guid entityId, string actionType, Guid performedByUserId,
        object? oldValues = null, object? newValues = null, CancellationToken ct = default);
    Task<IEnumerable<AuditLog>> GetByEntityAsync(string entityName, Guid entityId, CancellationToken ct = default);
    Task<IEnumerable<AuditLog>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
}
