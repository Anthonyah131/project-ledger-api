using ProjectLedger.API.Models;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

/// <summary>
/// Servicio de miembros de proyecto. Agrega, actualiza rol y remueve miembros.
/// Valida permisos del plan (sharing) y límites de team members.
/// </summary>
public class ProjectMemberService : IProjectMemberService
{
    private readonly IProjectMemberRepository _memberRepo;
    private readonly IProjectRepository _projectRepo;
    private readonly IPlanAuthorizationService _planAuth;

    public ProjectMemberService(
        IProjectMemberRepository memberRepo,
        IProjectRepository projectRepo,
        IPlanAuthorizationService planAuth)
    {
        _memberRepo = memberRepo;
        _projectRepo = projectRepo;
        _planAuth = planAuth;
    }

    public async Task<IEnumerable<ProjectMember>> GetByProjectIdAsync(
        Guid projectId, CancellationToken ct = default)
        => await _memberRepo.GetByProjectIdAsync(projectId, ct);

    public async Task<ProjectMember> AddMemberAsync(ProjectMember member, CancellationToken ct = default)
    {
        // Obtener el proyecto para saber quién es el owner y validar su plan
        var project = await _projectRepo.GetByIdAsync(member.PrmProjectId, ct)
            ?? throw new KeyNotFoundException($"Project '{member.PrmProjectId}' not found.");

        // Validar que el plan del owner permite compartir proyectos
        await _planAuth.ValidatePermissionAsync(
            project.PrjOwnerUserId, PlanPermission.CanShareProjects, ct);

        // Validar límite de miembros por proyecto
        var currentMembers = await _memberRepo.GetByProjectIdAsync(member.PrmProjectId, ct);
        await _planAuth.ValidateLimitAsync(
            project.PrjOwnerUserId, PlanLimits.MaxTeamMembersPerProject, currentMembers.Count(), ct);

        // Verificar que no sea un duplicado
        var existing = await _memberRepo.GetByProjectAndUserAsync(
            member.PrmProjectId, member.PrmUserId, ct);
        if (existing is not null)
            throw new InvalidOperationException("User is already a member of this project.");

        member.PrmJoinedAt = DateTime.UtcNow;
        member.PrmCreatedAt = DateTime.UtcNow;
        member.PrmUpdatedAt = DateTime.UtcNow;

        await _memberRepo.AddAsync(member, ct);
        await _memberRepo.SaveChangesAsync(ct);

        return member;
    }

    public async Task UpdateRoleAsync(Guid memberId, string newRole, CancellationToken ct = default)
    {
        var member = await _memberRepo.GetByIdAsync(memberId, ct)
            ?? throw new KeyNotFoundException($"Project member '{memberId}' not found.");

        if (member.PrmIsDeleted)
            throw new KeyNotFoundException($"Project member '{memberId}' not found.");

        // No se puede cambiar el rol del owner
        if (member.PrmRole == ProjectRoles.Owner)
            throw new InvalidOperationException("Cannot change the role of the project owner.");

        member.PrmRole = newRole;
        member.PrmUpdatedAt = DateTime.UtcNow;

        _memberRepo.Update(member);
        await _memberRepo.SaveChangesAsync(ct);
    }

    public async Task RemoveMemberAsync(Guid memberId, Guid deletedByUserId, CancellationToken ct = default)
    {
        var member = await _memberRepo.GetByIdAsync(memberId, ct)
            ?? throw new KeyNotFoundException($"Project member '{memberId}' not found.");

        if (member.PrmIsDeleted)
            throw new KeyNotFoundException($"Project member '{memberId}' not found.");

        // No se puede remover al owner
        if (member.PrmRole == ProjectRoles.Owner)
            throw new InvalidOperationException("Cannot remove the project owner from the project.");

        member.PrmIsDeleted = true;
        member.PrmDeletedAt = DateTime.UtcNow;
        member.PrmDeletedByUserId = deletedByUserId;
        member.PrmUpdatedAt = DateTime.UtcNow;

        _memberRepo.Update(member);
        await _memberRepo.SaveChangesAsync(ct);
    }
}
