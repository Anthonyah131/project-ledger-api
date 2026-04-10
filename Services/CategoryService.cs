using ProjectLedger.API.Models;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

/// <summary>
/// Category service. CRUD with soft delete.
/// Validates the project category limit based on the owner's plan.
/// </summary>
public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _categoryRepo;
    private readonly IProjectRepository _projectRepo;
    private readonly IPlanAuthorizationService _planAuth;
    private readonly IAuditLogService _auditLog;
    private readonly ITransactionReferenceGuardService _transactionReferenceGuard;

    public CategoryService(
        ICategoryRepository categoryRepo,
        IProjectRepository projectRepo,
        IPlanAuthorizationService planAuth,
        IAuditLogService auditLog,
        ITransactionReferenceGuardService transactionReferenceGuard)
    {
        _categoryRepo = categoryRepo;
        _projectRepo = projectRepo;
        _planAuth = planAuth;
        _auditLog = auditLog;
        _transactionReferenceGuard = transactionReferenceGuard;
    }

    /// <inheritdoc />
    public async Task<Category?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var category = await _categoryRepo.GetByIdAsync(id, ct);
        return category is { CatIsDeleted: false } ? category : null;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Category>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => await _categoryRepo.GetByProjectIdAsync(projectId, ct);

    /// <inheritdoc />
    public async Task<Category> CreateAsync(Category category, CancellationToken ct = default)
    {
        // Validate project category limit
        var project = await _projectRepo.GetByIdAsync(category.CatProjectId, ct)
            ?? throw new KeyNotFoundException("ProjectNotFound");

        var existingCategories = await _categoryRepo.GetByProjectIdAsync(category.CatProjectId, ct);
        await _planAuth.ValidateLimitAsync(
            project.PrjOwnerUserId, PlanLimits.MaxCategoriesPerProject, existingCategories.Count(), ct);

        category.CatCreatedAt = DateTime.UtcNow;
        category.CatUpdatedAt = DateTime.UtcNow;

        await _categoryRepo.AddAsync(category, ct);
        await _categoryRepo.SaveChangesAsync(ct);

        await _auditLog.LogAsync("Category", category.CatId, "create", project.PrjOwnerUserId,
            newValues: new { category.CatId, category.CatName, category.CatProjectId }, ct: ct);

        return category;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Category category, Guid performedByUserId, CancellationToken ct = default)
    {
        category.CatUpdatedAt = DateTime.UtcNow;
        _categoryRepo.Update(category);
        await _categoryRepo.SaveChangesAsync(ct);

        await _auditLog.LogAsync("Category", category.CatId, "update", performedByUserId,
            newValues: new { category.CatName, category.CatDescription }, ct: ct);
    }

    /// <inheritdoc />
    public async Task SoftDeleteAsync(Guid id, Guid deletedByUserId, CancellationToken ct = default)
    {
        var category = await _categoryRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("CategoryNotFound");

        if (category.CatIsDeleted)
            throw new KeyNotFoundException("CategoryNotFound");

        if (category.CatIsDefault)
            throw new InvalidOperationException("CategoryCannotDeleteDefault");

        await _transactionReferenceGuard.EnsureCategoryCanBeDeletedAsync(id, ct);

        category.CatIsDeleted = true;
        category.CatDeletedAt = DateTime.UtcNow;
        category.CatDeletedByUserId = deletedByUserId;
        category.CatUpdatedAt = DateTime.UtcNow;

        _categoryRepo.Update(category);
        await _categoryRepo.SaveChangesAsync(ct);

        await _auditLog.LogAsync("Category", id, "delete", deletedByUserId,
            oldValues: new { category.CatName }, ct: ct);
    }
}
