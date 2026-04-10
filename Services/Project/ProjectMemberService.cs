using ProjectLedger.API.Models;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

/// <summary>
/// Project members service. Adds, updates role, and removes members.
/// Validates plan permissions (sharing) and team members limits.
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
        // Get the project to know who the owner is and validate their plan
        var project = await _projectRepo.GetByIdAsync(member.PrmProjectId, ct)
            ?? throw new KeyNotFoundException("ProjectNotFound");

        // Validate that the owner's plan allows sharing projects
        await _planAuth.ValidatePermissionAsync(
            project.PrjOwnerUserId, PlanPermission.CanShareProjects, ct);

        // Validate members limit per project
        var currentMembers = await _memberRepo.GetByProjectIdAsync(member.PrmProjectId, ct);
        await _planAuth.ValidateLimitAsync(
            project.PrjOwnerUserId, PlanLimits.MaxTeamMembersPerProject, currentMembers.Count(), ct);

        // Verify that it is not a duplicate
        var existing = await _memberRepo.GetByProjectAndUserAsync(
            member.PrmProjectId, member.PrmUserId, ct);
        if (existing is not null)
            throw new InvalidOperationException("MemberAlreadyExists");

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
            ?? throw new KeyNotFoundException("MemberNotFound");

        if (member.PrmIsDeleted)
            throw new KeyNotFoundException("MemberNotFound");

        // Cannot change the owner's role
        if (member.PrmRole == ProjectRoles.Owner)
            throw new InvalidOperationException("MemberCannotChangeOwnerRole");

        member.PrmRole = newRole;
        member.PrmUpdatedAt = DateTime.UtcNow;

        _memberRepo.Update(member);
        await _memberRepo.SaveChangesAsync(ct);
    }

    public async Task RemoveMemberAsync(Guid memberId, Guid deletedByUserId, CancellationToken ct = default)
    {
        var member = await _memberRepo.GetByIdAsync(memberId, ct)
            ?? throw new KeyNotFoundException("MemberNotFound");

        if (member.PrmIsDeleted)
            throw new KeyNotFoundException("MemberNotFound");

        // Cannot remove the owner
        if (member.PrmRole == ProjectRoles.Owner)
            throw new InvalidOperationException("MemberCannotRemoveOwner");

        member.PrmIsDeleted = true;
        member.PrmDeletedAt = DateTime.UtcNow;
        member.PrmDeletedByUserId = deletedByUserId;
        member.PrmUpdatedAt = DateTime.UtcNow;

        _memberRepo.Update(member);
        await _memberRepo.SaveChangesAsync(ct);
    }
}
