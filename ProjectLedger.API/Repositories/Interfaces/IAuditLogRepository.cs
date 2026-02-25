using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public interface IAuditLogRepository : IRepository<AuditLog>
{
    Task<IEnumerable<AuditLog>> GetByEntityAsync(string entityName, Guid entityId, CancellationToken ct = default);
    Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> GetByEntityPagedAsync(string entityName, Guid entityId, int skip, int take, CancellationToken ct = default);
    Task<IEnumerable<AuditLog>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> GetByUserIdPagedAsync(Guid userId, int skip, int take, CancellationToken ct = default);
}
