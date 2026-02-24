using ProjectLedger.API.Models;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

/// <summary>
/// Servicio de categorías. CRUD con soft delete.
/// Valida límite de categorías por proyecto según el plan del owner.
/// </summary>
public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _categoryRepo;
    private readonly IProjectRepository _projectRepo;
    private readonly IPlanAuthorizationService _planAuth;

    public CategoryService(
        ICategoryRepository categoryRepo,
        IProjectRepository projectRepo,
        IPlanAuthorizationService planAuth)
    {
        _categoryRepo = categoryRepo;
        _projectRepo = projectRepo;
        _planAuth = planAuth;
    }

    public async Task<Category?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var category = await _categoryRepo.GetByIdAsync(id, ct);
        return category is { CatIsDeleted: false } ? category : null;
    }

    public async Task<IEnumerable<Category>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => await _categoryRepo.GetByProjectIdAsync(projectId, ct);

    public async Task<Category> CreateAsync(Category category, CancellationToken ct = default)
    {
        // Validar límite de categorías por proyecto
        var project = await _projectRepo.GetByIdAsync(category.CatProjectId, ct)
            ?? throw new KeyNotFoundException($"Project '{category.CatProjectId}' not found.");

        var existingCategories = await _categoryRepo.GetByProjectIdAsync(category.CatProjectId, ct);
        await _planAuth.ValidateLimitAsync(
            project.PrjOwnerUserId, PlanLimits.MaxCategoriesPerProject, existingCategories.Count(), ct);

        category.CatCreatedAt = DateTime.UtcNow;
        category.CatUpdatedAt = DateTime.UtcNow;

        await _categoryRepo.AddAsync(category, ct);
        await _categoryRepo.SaveChangesAsync(ct);

        return category;
    }

    public async Task UpdateAsync(Category category, CancellationToken ct = default)
    {
        category.CatUpdatedAt = DateTime.UtcNow;
        _categoryRepo.Update(category);
        await _categoryRepo.SaveChangesAsync(ct);
    }

    public async Task SoftDeleteAsync(Guid id, Guid deletedByUserId, CancellationToken ct = default)
    {
        var category = await _categoryRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Category '{id}' not found.");

        if (category.CatIsDeleted)
            throw new KeyNotFoundException($"Category '{id}' not found.");

        category.CatIsDeleted = true;
        category.CatDeletedAt = DateTime.UtcNow;
        category.CatDeletedByUserId = deletedByUserId;
        category.CatUpdatedAt = DateTime.UtcNow;

        _categoryRepo.Update(category);
        await _categoryRepo.SaveChangesAsync(ct);
    }
}
