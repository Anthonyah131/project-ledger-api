using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

/// <summary>
/// Implementation of IProjectAccessService.
/// Checks in this order: 1) Project Owner → full access, 2) ProjectMember → according to role.
/// </summary>
public class ProjectAccessService : IProjectAccessService
{
    private readonly IProjectRepository _projectRepo;
    private readonly IProjectMemberRepository _memberRepo;

    public ProjectAccessService(
        IProjectRepository projectRepo,
        IProjectMemberRepository memberRepo)
    {
        _projectRepo = projectRepo;
        _memberRepo = memberRepo;
    }

    /// <inheritdoc />
    public async Task<bool> HasAccessAsync(
        Guid userId, Guid projectId,
        string minimumRole = ProjectRoles.Viewer,
        CancellationToken ct = default)
    {
        var role = await GetUserRoleAsync(userId, projectId, ct);
        if (role == null) return false;

        return ProjectRoles.HasMinimumRole(role, minimumRole);
    }

    /// <inheritdoc />
    public async Task ValidateAccessAsync(
        Guid userId, Guid projectId,
        string minimumRole = ProjectRoles.Viewer,
        CancellationToken ct = default)
    {
        if (!await HasAccessAsync(userId, projectId, minimumRole, ct))
            throw new ForbiddenAccessException("ProjectAccessDenied");
    }

    /// <inheritdoc />
    public async Task<string?> GetUserRoleAsync(
        Guid userId, Guid projectId,
        CancellationToken ct = default)
    {
        // 1. Verify project exists and is not deleted
        var project = await _projectRepo.GetByIdAsync(projectId, ct);
        if (project == null) return null;

        // 2. If user is owner → automatic access as "owner"
        if (project.PrjOwnerUserId == userId)
            return ProjectRoles.Owner;

        // 3. Look for explicit membership
        var member = await _memberRepo.GetByProjectAndUserAsync(projectId, userId, ct);
        return member?.PrmRole;
    }
}
