using ProjectLedger.API.Models;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

/// <summary>
/// Workspace service. Group related projects together.
/// Only the owner can modify or delete the workspace.
/// </summary>
public class WorkspaceService : IWorkspaceService
{
    private readonly IWorkspaceRepository _workspaceRepo;

    public WorkspaceService(IWorkspaceRepository workspaceRepo)
    {
        _workspaceRepo = workspaceRepo;
    }

    public async Task<Workspace?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _workspaceRepo.GetByIdAsync(id, ct);

    public async Task<Workspace?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
        => await _workspaceRepo.GetByIdWithDetailsAsync(id, ct);

    public async Task<IEnumerable<Workspace>> GetByMemberUserIdAsync(Guid userId, CancellationToken ct = default)
        => await _workspaceRepo.GetByMemberUserIdAsync(userId, ct);

    public async Task<string?> GetMemberRoleAsync(Guid workspaceId, Guid userId, CancellationToken ct = default)
        => await _workspaceRepo.GetMemberRoleAsync(workspaceId, userId, ct);

    public async Task<int> CountProjectsAsync(Guid workspaceId, CancellationToken ct = default)
        => await _workspaceRepo.CountProjectsAsync(workspaceId, ct);

    public async Task<Workspace> CreateAsync(Workspace workspace, CancellationToken ct = default)
    {
        workspace.WksCreatedAt = DateTime.UtcNow;
        workspace.WksUpdatedAt = DateTime.UtcNow;

        // The creator is assigned as 'owner' in workspace_members
        workspace.Members.Add(new WorkspaceMember
        {
            WkmWorkspaceId = workspace.WksId,
            WkmUserId = workspace.WksOwnerUserId,
            WkmRole = WorkspaceRoles.Owner,
            WkmJoinedAt = DateTime.UtcNow,
            WkmCreatedAt = DateTime.UtcNow,
            WkmUpdatedAt = DateTime.UtcNow
        });

        await _workspaceRepo.AddAsync(workspace, ct);
        await _workspaceRepo.SaveChangesAsync(ct);

        return workspace;
    }

    public async Task UpdateAsync(Workspace workspace, CancellationToken ct = default)
    {
        workspace.WksUpdatedAt = DateTime.UtcNow;
        _workspaceRepo.Update(workspace);
        await _workspaceRepo.SaveChangesAsync(ct);
    }

    public async Task SoftDeleteAsync(Guid id, Guid deletedByUserId, CancellationToken ct = default)
    {
        var workspace = await _workspaceRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("WorkspaceNotFound");

        var hasProjects = await _workspaceRepo.HasActiveProjectsAsync(id, ct);
        if (hasProjects)
            throw new InvalidOperationException("WorkspaceCannotDeleteActiveProjects");

        workspace.WksIsDeleted = true;
        workspace.WksDeletedAt = DateTime.UtcNow;
        workspace.WksDeletedByUserId = deletedByUserId;
        workspace.WksUpdatedAt = DateTime.UtcNow;

        _workspaceRepo.Update(workspace);
        await _workspaceRepo.SaveChangesAsync(ct);
    }

    public async Task<Workspace?> GetGeneralWorkspaceForUserAsync(Guid userId, CancellationToken ct = default)
        => await _workspaceRepo.GetGeneralWorkspaceForUserAsync(userId, ct);
}
