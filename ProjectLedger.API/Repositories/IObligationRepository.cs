using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public interface IObligationRepository : IRepository<Obligation>
{
    Task<IEnumerable<Obligation>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
}
