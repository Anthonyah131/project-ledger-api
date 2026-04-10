using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

public interface ICategoryService
{
    /// <summary>
    /// Gets a category by ID.
    /// </summary>
    Task<Category?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Gets all categories belonging to a specific project.
    /// </summary>
    Task<IEnumerable<Category>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Creates a new category.
    /// </summary>
    Task<Category> CreateAsync(Category category, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing category's name or metadata.
    /// </summary>
    Task UpdateAsync(Category category, Guid performedByUserId, CancellationToken ct = default);

    /// <summary>
    /// Soft-deletes a category.
    /// </summary>
    Task SoftDeleteAsync(Guid id, Guid deletedByUserId, CancellationToken ct = default);
}
