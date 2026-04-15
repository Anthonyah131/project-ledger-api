using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

/// <summary>
/// Repository interface for AuditLog operations.
/// </summary>
public interface IAuditLogRepository : IRepository<AuditLog>
{
    /// <summary>Returns all audit log entries for a specific entity instance.</summary>
    Task<IEnumerable<AuditLog>> GetByEntityAsync(string entityName, Guid entityId, CancellationToken ct = default);

    /// <summary>Returns a paged list of audit log entries for a specific entity instance.</summary>
    Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> GetByEntityPagedAsync(string entityName, Guid entityId, int skip, int take, CancellationToken ct = default);

    /// <summary>Returns all audit log entries created by a specific user.</summary>
    Task<IEnumerable<AuditLog>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Returns a paged list of audit log entries created by a specific user.</summary>
    Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> GetByUserIdPagedAsync(Guid userId, int skip, int take, CancellationToken ct = default);

    /// <summary>
    /// Returns the number of audit log entries matching the given user, entity name, and action type
    /// within the specified UTC time range. Used to enforce rate-based plan limits.
    /// </summary>
    Task<int> CountByUserAndActionInRangeAsync(
        Guid userId,
        string entityName,
        string actionType,
        DateTime fromInclusiveUtc,
        DateTime toExclusiveUtc,
        CancellationToken ct = default);
}
