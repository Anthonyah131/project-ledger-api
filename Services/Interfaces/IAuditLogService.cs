using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

public interface IAuditLogService
{
    /// <summary>
    /// Asynchronously logs an action performed on a specific entity.
    /// </summary>
    /// <param name="entityName">The name of the entity being logged (e.g., "Project").</param>
    /// <param name="entityId">The unique identifier of the entity.</param>
    /// <param name="actionType">The type of action performed (e.g., "Create", "Update", "Delete").</param>
    /// <param name="performedByUserId">The ID of the user who performed the action.</param>
    /// <param name="oldValues">The state of the entity before the action (optional).</param>
    /// <param name="newValues">The state of the entity after the action (optional).</param>
    /// <param name="ct">Cancellation token.</param>
    Task LogAsync(string entityName, Guid entityId, string actionType, Guid performedByUserId,
        object? oldValues = null, object? newValues = null, CancellationToken ct = default);
    /// <summary>
    /// Retrieves all audit log entries for a specific entity.
    /// </summary>
    Task<IEnumerable<AuditLog>> GetByEntityAsync(string entityName, Guid entityId, CancellationToken ct = default);
    /// <summary>
    /// Retrieves a paginated list of audit log entries for a specific entity.
    /// </summary>
    Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> GetByEntityPagedAsync(string entityName, Guid entityId, int skip, int take, CancellationToken ct = default);
    /// <summary>
    /// Retrieves all audit log entries of actions performed by a specific user.
    /// </summary>
    Task<IEnumerable<AuditLog>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    /// <summary>
    /// Retrieves a paginated list of audit log entries of actions performed by a specific user.
    /// </summary>
    Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> GetByUserIdPagedAsync(Guid userId, int skip, int take, CancellationToken ct = default);
    /// <summary>
    /// Counts the number of actions of a specific type performed by a user on an entity within a given time range.
    /// </summary>
    Task<int> CountByUserAndActionInRangeAsync(
        Guid userId,
        string entityName,
        string actionType,
        DateTime fromInclusiveUtc,
        DateTime toExclusiveUtc,
        CancellationToken ct = default);
}
