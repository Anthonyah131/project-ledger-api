using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

public interface IPlanService
{
    Task<Plan?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Plan?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<IEnumerable<Plan>> GetAllActiveAsync(CancellationToken ct = default);
}
