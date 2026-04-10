using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

public interface IPlanService
{
    /// <summary>
    /// Gets a plan by ID.
    /// </summary>
    Task<Plan?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Gets a plan by its unique slug (e.g., "free", "premium").
    /// </summary>
    Task<Plan?> GetBySlugAsync(string slug, CancellationToken ct = default);

    /// <summary>
    /// Returns all available active plans.
    /// </summary>
    Task<IEnumerable<Plan>> GetAllActiveAsync(CancellationToken ct = default);
}
