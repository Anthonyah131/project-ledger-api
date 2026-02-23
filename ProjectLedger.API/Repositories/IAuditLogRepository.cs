using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public interface IAuditLogRepository : IRepository<AuditLog>
{
    Task<IEnumerable<AuditLog>> GetByEntityAsync(string entityName, Guid entityId, CancellationToken ct = default);
    Task<IEnumerable<AuditLog>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
}
