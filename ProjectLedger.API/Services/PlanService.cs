using ProjectLedger.API.Models;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

/// <summary>
/// Servicio de planes. Solo lectura (los planes se gestionan por seed/admin).
/// </summary>
public class PlanService : IPlanService
{
    private readonly IPlanRepository _planRepo;

    public PlanService(IPlanRepository planRepo)
    {
        _planRepo = planRepo;
    }

    public async Task<Plan?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _planRepo.GetByIdAsync(id, ct);

    public async Task<Plan?> GetBySlugAsync(string slug, CancellationToken ct = default)
        => await _planRepo.GetBySlugAsync(slug.ToLowerInvariant(), ct);

    public async Task<IEnumerable<Plan>> GetAllActiveAsync(CancellationToken ct = default)
        => await _planRepo.GetActiveAsync(ct);
}
