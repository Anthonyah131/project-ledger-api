using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

/// <summary>
/// Implementación de IProjectAccessService.
/// Verifica en este orden: 1) Owner del proyecto → acceso total, 2) ProjectMember → según rol.
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
            throw new ForbiddenAccessException(
                $"User does not have '{minimumRole}' access to project '{projectId}'.");
    }

    /// <inheritdoc />
    public async Task<string?> GetUserRoleAsync(
        Guid userId, Guid projectId,
        CancellationToken ct = default)
    {
        // 1. Verificar que el proyecto existe y no está borrado
        var project = await _projectRepo.GetByIdAsync(projectId, ct);
        if (project == null) return null;

        // 2. Si es el owner → acceso automático como "owner"
        if (project.PrjOwnerUserId == userId)
            return ProjectRoles.Owner;

        // 3. Buscar membresía explícita
        var member = await _memberRepo.GetByProjectAndUserAsync(projectId, userId, ct);
        return member?.PrmRole;
    }
}
