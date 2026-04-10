using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

/// <summary>
/// Repository interface for Project operations.
/// </summary>
public interface IProjectRepository : IRepository<Project>
{
    Task<IEnumerable<Project>> GetByOwnerUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<IEnumerable<Project>> GetByMemberUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<(IEnumerable<Project> Items, int TotalCount)> GetByUserIdPagedAsync(
        Guid userId, int skip, int take, string? sortBy = null, bool isDescending = true, CancellationToken ct = default);
    Task<(IEnumerable<Project> Items, int TotalCount)> GetByUserIdPagedExcludingAsync(
        Guid userId, IEnumerable<Guid> excludeProjectIds, int skip, int take, string? sortBy = null, bool isDescending = true, CancellationToken ct = default);
    Task<(IEnumerable<Project> Items, int TotalCount)> GetByWorkspaceIdPagedAsync(
        Guid workspaceId, Guid userId, int skip, int take, string? sortBy = null, bool isDescending = true, CancellationToken ct = default);
    Task<(IEnumerable<Project> Items, int TotalCount)> GetByWorkspaceIdPagedExcludingAsync(
        Guid workspaceId, Guid userId, IEnumerable<Guid> excludeProjectIds, int skip, int take, string? sortBy = null, bool isDescending = true, CancellationToken ct = default);
    Task<(IEnumerable<Project> Items, int TotalCount)> GetByUserIdPagedExcludingWithSearchAsync(
        Guid userId, IEnumerable<Guid> excludeProjectIds, string? search, int skip, int take, CancellationToken ct = default);
}
