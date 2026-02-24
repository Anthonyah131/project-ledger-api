using ProjectLedger.API.Models;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

/// <summary>
/// Servicio de proyectos. CRUD con soft delete.
/// Crea automáticamente un ProjectMember(owner) al crear el proyecto.
/// Valida permisos del plan antes de crear.
/// </summary>
public class ProjectService : IProjectService
{
    private readonly IProjectRepository _projectRepo;
    private readonly IProjectMemberRepository _memberRepo;
    private readonly ICategoryRepository _categoryRepo;
    private readonly IPlanAuthorizationService _planAuth;
    private readonly IAuditLogService _auditLog;

    public ProjectService(
        IProjectRepository projectRepo,
        IProjectMemberRepository memberRepo,
        ICategoryRepository categoryRepo,
        IPlanAuthorizationService planAuth,
        IAuditLogService auditLog)
    {
        _projectRepo = projectRepo;
        _memberRepo = memberRepo;
        _categoryRepo = categoryRepo;
        _planAuth = planAuth;
        _auditLog = auditLog;
    }

    public async Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _projectRepo.GetByIdAsync(id, ct);

    public async Task<IEnumerable<Project>> GetByOwnerUserIdAsync(Guid userId, CancellationToken ct = default)
        => await _projectRepo.GetByOwnerUserIdAsync(userId, ct);

    public async Task<IEnumerable<Project>> GetByMemberUserIdAsync(Guid userId, CancellationToken ct = default)
        => await _projectRepo.GetByMemberUserIdAsync(userId, ct);

    public async Task<Project> CreateAsync(Project project, CancellationToken ct = default)
    {
        // Validar permiso del plan
        await _planAuth.ValidatePermissionAsync(
            project.PrjOwnerUserId, PlanPermission.CanCreateProjects, ct);

        // Validar límite de proyectos
        var ownedProjects = await _projectRepo.GetByOwnerUserIdAsync(project.PrjOwnerUserId, ct);
        await _planAuth.ValidateLimitAsync(
            project.PrjOwnerUserId, PlanLimits.MaxProjects, ownedProjects.Count(), ct);

        project.PrjCreatedAt = DateTime.UtcNow;
        project.PrjUpdatedAt = DateTime.UtcNow;

        await _projectRepo.AddAsync(project, ct);

        // Crear membership "owner" automáticamente
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

        // Crear categoría por defecto "General"
        var defaultCategory = new Category
        {
            CatId = Guid.NewGuid(),
            CatProjectId = project.PrjId,
            CatName = "General",
            CatDescription = "Categoría por defecto del proyecto.",
            CatIsDefault = true,
            CatCreatedAt = DateTime.UtcNow,
            CatUpdatedAt = DateTime.UtcNow
        };
        await _categoryRepo.AddAsync(defaultCategory, ct);

        await _projectRepo.SaveChangesAsync(ct);

        _ = _auditLog.LogAsync("Project", project.PrjId, "create", project.PrjOwnerUserId,
            newValues: new { project.PrjId, project.PrjName, project.PrjCurrencyCode }, ct: ct);

        return project;
    }

    public async Task UpdateAsync(Project project, CancellationToken ct = default)
    {
        project.PrjUpdatedAt = DateTime.UtcNow;
        _projectRepo.Update(project);
        await _projectRepo.SaveChangesAsync(ct);

        _ = _auditLog.LogAsync("Project", project.PrjId, "update", project.PrjOwnerUserId,
            newValues: new { project.PrjName, project.PrjDescription }, ct: ct);
    }

    public async Task SoftDeleteAsync(Guid id, Guid deletedByUserId, CancellationToken ct = default)
    {
        var project = await _projectRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Project '{id}' not found.");

        project.PrjIsDeleted = true;
        project.PrjDeletedAt = DateTime.UtcNow;
        project.PrjDeletedByUserId = deletedByUserId;
        project.PrjUpdatedAt = DateTime.UtcNow;

        _projectRepo.Update(project);
        await _projectRepo.SaveChangesAsync(ct);

        _ = _auditLog.LogAsync("Project", id, "delete", deletedByUserId,
            oldValues: new { project.PrjName }, ct: ct);
    }
}
