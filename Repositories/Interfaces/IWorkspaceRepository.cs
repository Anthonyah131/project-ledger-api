using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

/// <summary>
/// Repository interface for Workspace operations.
/// </summary>
public interface IWorkspaceRepository : IRepository<Workspace>
{
    Task<Workspace?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Workspace>> GetByMemberUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<bool> HasActiveProjectsAsync(Guid workspaceId, CancellationToken ct = default);
    Task<int> CountProjectsAsync(Guid workspaceId, CancellationToken ct = default);
    Task<string?> GetMemberRoleAsync(Guid workspaceId, Guid userId, CancellationToken ct = default);
    /// <summary>Returns the first workspace named "General" owned by the user, or null if none exists.</summary>
    Task<Workspace?> GetGeneralWorkspaceForUserAsync(Guid userId, CancellationToken ct = default);
}
