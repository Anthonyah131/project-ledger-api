using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public interface IProjectRepository : IRepository<Project>
{
    Task<IEnumerable<Project>> GetByOwnerUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<IEnumerable<Project>> GetByMemberUserIdAsync(Guid userId, CancellationToken ct = default);
}
