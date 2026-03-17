using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

public interface IWorkspaceService
{
    Task<Workspace?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Workspace?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Workspace>> GetByMemberUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<string?> GetMemberRoleAsync(Guid workspaceId, Guid userId, CancellationToken ct = default);
    Task<int> CountProjectsAsync(Guid workspaceId, CancellationToken ct = default);
    Task<Workspace> CreateAsync(Workspace workspace, CancellationToken ct = default);
    Task UpdateAsync(Workspace workspace, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid id, Guid deletedByUserId, CancellationToken ct = default);
    /// <summary>Returns the "General" workspace owned by the user, or null if not found.</summary>
    Task<Workspace?> GetGeneralWorkspaceForUserAsync(Guid userId, CancellationToken ct = default);
}
