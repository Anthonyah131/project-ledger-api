using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

public interface IUserService
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User> CreateAsync(User user, CancellationToken ct = default);
    Task UpdateAsync(User user, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid id, Guid deletedByUserId, CancellationToken ct = default);

    // ── Admin operations ────────────────────────────────────
    Task<IReadOnlyList<User>> GetAllAsync(bool includeDeleted = false, CancellationToken ct = default);
    Task<bool> ActivateAsync(Guid userId, CancellationToken ct = default);
    Task<bool> DeactivateAsync(Guid userId, CancellationToken ct = default);
}
