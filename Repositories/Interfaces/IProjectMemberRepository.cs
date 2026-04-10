using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

/// <summary>
/// Repository interface for ProjectMember operations.
/// </summary>
public interface IProjectMemberRepository : IRepository<ProjectMember>
{
    Task<IEnumerable<ProjectMember>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task<ProjectMember?> GetByProjectAndUserAsync(Guid projectId, Guid userId, CancellationToken ct = default);
    Task<IEnumerable<ProjectMember>> GetPinnedByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<IEnumerable<ProjectMember>> GetPinnedByUserIdWithSearchAsync(Guid userId, string? search, CancellationToken ct = default);
    Task<int> GetPinnedCountAsync(Guid userId, CancellationToken ct = default);
}
