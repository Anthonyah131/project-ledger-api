using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public interface IPlanRepository : IRepository<Plan>
{
    Task<Plan?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<IEnumerable<Plan>> GetActiveAsync(CancellationToken ct = default);
}
