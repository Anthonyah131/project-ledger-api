using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

public interface IProjectMemberService
{
    Task<IEnumerable<ProjectMember>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task<ProjectMember> AddMemberAsync(ProjectMember member, CancellationToken ct = default);
    Task UpdateRoleAsync(Guid memberId, string newRole, CancellationToken ct = default);
    Task RemoveMemberAsync(Guid memberId, Guid deletedByUserId, CancellationToken ct = default);
}
