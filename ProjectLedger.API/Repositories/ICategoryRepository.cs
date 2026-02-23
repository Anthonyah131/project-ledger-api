using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public interface ICategoryRepository : IRepository<Category>
{
    Task<IEnumerable<Category>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task<Category?> GetDefaultByProjectIdAsync(Guid projectId, CancellationToken ct = default);
}
