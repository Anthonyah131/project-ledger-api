using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);
    Task<IReadOnlyList<User>> GetAllUsersAsync(bool includeDeleted = false, CancellationToken ct = default);
}
