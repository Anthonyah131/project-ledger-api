using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

public interface IProjectMemberService
{
    /// <summary>
    /// Gets all members (collaborators) assigned to a project.
    /// </summary>
    Task<IEnumerable<ProjectMember>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Adds a new user as a member of the project.
    /// </summary>
    Task<ProjectMember> AddMemberAsync(ProjectMember member, CancellationToken ct = default);

    /// <summary>
    /// Updates a member's role (e.g., Editor, Viewer).
    /// </summary>
    Task UpdateRoleAsync(Guid memberId, string newRole, CancellationToken ct = default);

    /// <summary>
    /// Removes a member from the project.
    /// </summary>
    Task RemoveMemberAsync(Guid memberId, Guid deletedByUserId, CancellationToken ct = default);
}
