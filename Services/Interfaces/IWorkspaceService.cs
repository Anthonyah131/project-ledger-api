using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

public interface IWorkspaceService
{
    /// <summary>
    /// Gets a workspace by ID.
    /// </summary>
    Task<Workspace?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Gets a workspace by ID including detailed projects and members.
    /// </summary>
    Task<Workspace?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Lists all workspaces where the user is a member.
    /// </summary>
    Task<IEnumerable<Workspace>> GetByMemberUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Gets the role of a user within a specific workspace.
    /// </summary>
    Task<string?> GetMemberRoleAsync(Guid workspaceId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Counts the number of active projects in a workspace.
    /// </summary>
    Task<int> CountProjectsAsync(Guid workspaceId, CancellationToken ct = default);

    /// <summary>
    /// Creates a new workspace.
    /// </summary>
    Task<Workspace> CreateAsync(Workspace workspace, CancellationToken ct = default);

    /// <summary>
    /// Updates a workspace's name or metadata.
    /// </summary>
    Task UpdateAsync(Workspace workspace, CancellationToken ct = default);

    /// <summary>
    /// Soft-deletes a workspace and its related project associations.
    /// </summary>
    Task SoftDeleteAsync(Guid id, Guid deletedByUserId, CancellationToken ct = default);
    /// <summary>Returns the "General" workspace owned by the user, or null if not found.</summary>
    Task<Workspace?> GetGeneralWorkspaceForUserAsync(Guid userId, CancellationToken ct = default);
}
