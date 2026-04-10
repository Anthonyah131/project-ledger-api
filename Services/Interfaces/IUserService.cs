using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

public interface IUserService
{
    /// <summary>
    /// Gets a user by ID.
    /// </summary>
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Gets a user by email address.
    /// </summary>
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Creates a new user in the system.
    /// </summary>
    Task<User> CreateAsync(User user, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing user's profile and metadata.
    /// </summary>
    Task UpdateAsync(User user, CancellationToken ct = default);

    /// <summary>
    /// Soft-deletes a user.
    /// </summary>
    Task SoftDeleteAsync(Guid id, Guid deletedByUserId, CancellationToken ct = default);

    // ── Admin operations ────────────────────────────────────

    /// <summary>
    /// Admin tool to list all users in the system.
    /// </summary>
    Task<IReadOnlyList<User>> GetAllAsync(bool includeDeleted = false, CancellationToken ct = default);

    /// <summary>
    /// Admin tool to retrieve a paginated and sorted list of users.
    /// </summary>
    Task<(IReadOnlyList<User> Items, int TotalCount)> GetAllPagedAsync(bool includeDeleted, int skip, int take, string? sortBy, bool descending, CancellationToken ct = default);

    /// <summary>
    /// Activates a user account.
    /// </summary>
    Task<bool> ActivateAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Deactivates a user account.
    /// </summary>
    Task<bool> DeactivateAsync(Guid userId, CancellationToken ct = default);
}
