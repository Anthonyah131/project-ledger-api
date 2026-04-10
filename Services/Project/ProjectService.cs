using ProjectLedger.API.Models;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

/// <summary>
/// Projects service. CRUD with soft delete.
/// Automatically creates a ProjectMember (owner) when creating a project.
/// Validates plan permissions before creation.
/// </summary>
public class ProjectService : IProjectService
{
    private readonly IProjectRepository _projectRepo;
    private readonly IProjectMemberRepository _memberRepo;
    private readonly ICategoryRepository _categoryRepo;
    private readonly IProjectPartnerRepository _projectPartnerRepo;
    private readonly IExpenseSplitRepository _expenseSplitRepo;
    private readonly IIncomeSplitRepository _incomeSplitRepo;
    private readonly IPlanAuthorizationService _planAuth;
    private readonly IAuditLogService _auditLog;

    public ProjectService(
        IProjectRepository projectRepo,
        IProjectMemberRepository memberRepo,
        ICategoryRepository categoryRepo,
        IProjectPartnerRepository projectPartnerRepo,
        IExpenseSplitRepository expenseSplitRepo,
        IIncomeSplitRepository incomeSplitRepo,
        IPlanAuthorizationService planAuth,
        IAuditLogService auditLog)
    {
        _projectRepo = projectRepo;
        _memberRepo = memberRepo;
        _categoryRepo = categoryRepo;
        _projectPartnerRepo = projectPartnerRepo;
        _expenseSplitRepo = expenseSplitRepo;
        _incomeSplitRepo = incomeSplitRepo;
        _planAuth = planAuth;
        _auditLog = auditLog;
    }

    public async Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _projectRepo.GetByIdAsync(id, ct);

    public async Task<IEnumerable<Project>> GetByOwnerUserIdAsync(Guid userId, CancellationToken ct = default)
        => await _projectRepo.GetByOwnerUserIdAsync(userId, ct);

    public async Task<IEnumerable<Project>> GetByMemberUserIdAsync(Guid userId, CancellationToken ct = default)
        => await _projectRepo.GetByMemberUserIdAsync(userId, ct);

    public async Task<(IEnumerable<Project> Items, int TotalCount)> GetByUserIdPagedAsync(
        Guid userId, int skip, int take, string? sortBy = null, bool isDescending = true, CancellationToken ct = default)
        => await _projectRepo.GetByUserIdPagedAsync(userId, skip, take, sortBy, isDescending, ct);

    public async Task<(IEnumerable<Project> Items, int TotalCount)> GetByWorkspaceIdPagedAsync(
        Guid workspaceId, Guid userId, int skip, int take, string? sortBy = null, bool isDescending = true, CancellationToken ct = default)
        => await _projectRepo.GetByWorkspaceIdPagedAsync(workspaceId, userId, skip, take, sortBy, isDescending, ct);

    public async Task<IEnumerable<ProjectMember>> GetPinnedMembershipsAsync(Guid userId, CancellationToken ct = default)
        => await _memberRepo.GetPinnedByUserIdAsync(userId, ct);

    public async Task<(IEnumerable<Project> Items, int TotalCount)> GetByUserIdPagedExcludingAsync(
        Guid userId, IEnumerable<Guid> excludeProjectIds, int skip, int take, string? sortBy = null, bool isDescending = true, CancellationToken ct = default)
        => await _projectRepo.GetByUserIdPagedExcludingAsync(userId, excludeProjectIds, skip, take, sortBy, isDescending, ct);

    public async Task<(IEnumerable<Project> Items, int TotalCount)> GetByWorkspaceIdPagedExcludingAsync(
        Guid workspaceId, Guid userId, IEnumerable<Guid> excludeProjectIds, int skip, int take, string? sortBy = null, bool isDescending = true, CancellationToken ct = default)
        => await _projectRepo.GetByWorkspaceIdPagedExcludingAsync(workspaceId, userId, excludeProjectIds, skip, take, sortBy, isDescending, ct);

    public async Task<DateTime> PinProjectAsync(Guid userId, Guid projectId, CancellationToken ct = default)
    {
        var member = await _memberRepo.GetByProjectAndUserAsync(projectId, userId, ct)
            ?? throw new KeyNotFoundException("ProjectNotFound");

        if (member.PrmIsPinned)
            return member.PrmPinnedAt!.Value;

        var pinnedCount = await _memberRepo.GetPinnedCountAsync(userId, ct);
        if (pinnedCount >= 6)
            throw new InvalidOperationException("PINNED_LIMIT_EXCEEDED");

        var now = DateTime.UtcNow;
        member.PrmIsPinned = true;
        member.PrmPinnedAt = now;
        member.PrmUpdatedAt = now;

        _memberRepo.Update(member);
        await _memberRepo.SaveChangesAsync(ct);

        return now;
    }

    public async Task UnpinProjectAsync(Guid userId, Guid projectId, CancellationToken ct = default)
    {
        var member = await _memberRepo.GetByProjectAndUserAsync(projectId, userId, ct);
        if (member is null || !member.PrmIsPinned)
            return;

        member.PrmIsPinned = false;
        member.PrmPinnedAt = null;
        member.PrmUpdatedAt = DateTime.UtcNow;

        _memberRepo.Update(member);
        await _memberRepo.SaveChangesAsync(ct);
    }

    public async Task<Project> CreateAsync(Project project, CancellationToken ct = default)
    {
        // Validate plan permission
        await _planAuth.ValidatePermissionAsync(
            project.PrjOwnerUserId, PlanPermission.CanCreateProjects, ct);

        // Validate projects limit
        var ownedProjects = await _projectRepo.GetByOwnerUserIdAsync(project.PrjOwnerUserId, ct);
        await _planAuth.ValidateLimitAsync(
            project.PrjOwnerUserId, PlanLimits.MaxProjects, ownedProjects.Count(), ct);

        project.PrjCreatedAt = DateTime.UtcNow;
        project.PrjUpdatedAt = DateTime.UtcNow;

        await _projectRepo.AddAsync(project, ct);

        // Automatically create "owner" membership
        var ownerMember = new ProjectMember
        {
            PrmId = Guid.NewGuid(),
            PrmProjectId = project.PrjId,
            PrmUserId = project.PrjOwnerUserId,
            PrmRole = ProjectRoles.Owner,
            PrmJoinedAt = DateTime.UtcNow,
            PrmCreatedAt = DateTime.UtcNow,
            PrmUpdatedAt = DateTime.UtcNow
        };

        await _memberRepo.AddAsync(ownerMember, ct);

        // Create default "General" category
        var defaultCategory = new Category
        {
            CatId = Guid.NewGuid(),
            CatProjectId = project.PrjId,
            CatName = "General",
            CatDescription = "Default project category.",
            CatIsDefault = true,
            CatCreatedAt = DateTime.UtcNow,
            CatUpdatedAt = DateTime.UtcNow
        };
        await _categoryRepo.AddAsync(defaultCategory, ct);

        await _projectRepo.SaveChangesAsync(ct);

        await _auditLog.LogAsync("Project", project.PrjId, "create", project.PrjOwnerUserId,
            newValues: new { project.PrjId, project.PrjName, project.PrjCurrencyCode }, ct: ct);

        return project;
    }

    public async Task UpdateAsync(Project project, CancellationToken ct = default)
    {
        // Validate that the owner's plan allows editing projects
        await _planAuth.ValidatePermissionAsync(
            project.PrjOwnerUserId, PlanPermission.CanEditProjects, ct);

        project.PrjUpdatedAt = DateTime.UtcNow;
        // Do NOT call _projectRepo.Update(project) — that marks all properties as Modified
        // and would overwrite concurrent setting changes (e.g. PrjPartnersEnabled).
        // The entity is already tracked (loaded from GetByIdAsync in the same scope),
        // so SaveChanges will only persist the actually-changed properties.
        await _projectRepo.SaveChangesAsync(ct);

        await _auditLog.LogAsync("Project", project.PrjId, "update", project.PrjOwnerUserId,
            newValues: new { project.PrjName, project.PrjDescription }, ct: ct);
    }

    public async Task SetWorkspaceAsync(Guid projectId, Guid? workspaceId, CancellationToken ct = default)
    {
        var project = await _projectRepo.GetByIdAsync(projectId, ct)
            ?? throw new KeyNotFoundException("ProjectNotFound");

        project.PrjWorkspaceId = workspaceId;
        project.PrjUpdatedAt = DateTime.UtcNow;

        _projectRepo.Update(project);
        await _projectRepo.SaveChangesAsync(ct);
    }

    public async Task SoftDeleteAsync(Guid id, Guid deletedByUserId, CancellationToken ct = default)
    {
        var project = await _projectRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("ProjectNotFound");

        // Validate that the owner's plan allows deleting projects
        await _planAuth.ValidatePermissionAsync(
            project.PrjOwnerUserId, PlanPermission.CanDeleteProjects, ct);

        project.PrjIsDeleted = true;
        project.PrjDeletedAt = DateTime.UtcNow;
        project.PrjDeletedByUserId = deletedByUserId;
        project.PrjUpdatedAt = DateTime.UtcNow;

        _projectRepo.Update(project);
        await _projectRepo.SaveChangesAsync(ct);

        await _auditLog.LogAsync("Project", id, "delete", deletedByUserId,
            oldValues: new { project.PrjName }, ct: ct);
    }

    public async Task<(IEnumerable<ProjectMember> PinnedFiltered, int PinnedTotalCount, IEnumerable<Project> Items, int TotalCount)>
        GetProjectsLookupAsync(Guid userId, string? search, int page, int skip, int take, CancellationToken ct = default)
    {
        // Single query: all pinned items (max 6). IDs to exclude from items[]
        var allPinned = await _memberRepo.GetPinnedByUserIdAsync(userId, ct);
        var pinnedList = allPinned.ToList();
        var pinnedIds = pinnedList.Select(m => m.PrmProjectId).ToList();

        // Filter in-memory (max 6, always cheap). Only on page 1
        IEnumerable<ProjectMember> pinnedFiltered = [];
        if (page == 1)
        {
            pinnedFiltered = string.IsNullOrWhiteSpace(search)
                ? pinnedList
                : pinnedList.Where(m => m.Project.PrjName.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var (items, totalCount) = await _projectRepo.GetByUserIdPagedExcludingWithSearchAsync(
            userId, pinnedIds, search, skip, take, ct);

        return (pinnedFiltered, pinnedList.Count, items, totalCount);
    }

    public async Task UpdateSettingsAsync(Guid projectId, bool? partnersEnabled, CancellationToken ct = default)
    {
        var project = await _projectRepo.GetByIdAsync(projectId, ct)
            ?? throw new KeyNotFoundException("ProjectNotFound");

        if (partnersEnabled.HasValue)
        {
            if (partnersEnabled.Value)
            {
                await _planAuth.ValidatePermissionAsync(project.PrjOwnerUserId, PlanPermission.CanUsePartners, ct);

                var partners = await _projectPartnerRepo.GetByProjectIdAsync(projectId, ct);
                if (partners.Count() < 2)
                    throw new InvalidOperationException("ProjectPartnersEnabledRequiresMinPartners");
            }
            else
            {
                // Cannot be disabled if there are already movements with registered splits
                var hasExpenseSplits = await _expenseSplitRepo.ExistsForProjectAsync(projectId, ct);
                var hasIncomeSplits = await _incomeSplitRepo.ExistsForProjectAsync(projectId, ct);
                if (hasExpenseSplits || hasIncomeSplits)
                    throw new InvalidOperationException("ProjectPartnersDisabledHasSplits");
            }

            project.PrjPartnersEnabled = partnersEnabled.Value;
        }

        project.PrjUpdatedAt = DateTime.UtcNow;
        _projectRepo.Update(project);
        await _projectRepo.SaveChangesAsync(ct);
    }
}
