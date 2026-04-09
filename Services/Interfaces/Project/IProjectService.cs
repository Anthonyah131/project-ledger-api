using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

public interface IProjectService
{
    Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Project>> GetByOwnerUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<IEnumerable<Project>> GetByMemberUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<(IEnumerable<Project> Items, int TotalCount)> GetByUserIdPagedAsync(
        Guid userId, int skip, int take, string? sortBy = null, bool isDescending = true, CancellationToken ct = default);
    Task<IEnumerable<ProjectMember>> GetPinnedMembershipsAsync(Guid userId, CancellationToken ct = default);
    Task<(IEnumerable<Project> Items, int TotalCount)> GetByUserIdPagedExcludingAsync(
        Guid userId, IEnumerable<Guid> excludeProjectIds, int skip, int take, string? sortBy = null, bool isDescending = true, CancellationToken ct = default);
    Task<DateTime> PinProjectAsync(Guid userId, Guid projectId, CancellationToken ct = default);
    Task UnpinProjectAsync(Guid userId, Guid projectId, CancellationToken ct = default);
    Task<(IEnumerable<Project> Items, int TotalCount)> GetByWorkspaceIdPagedAsync(
        Guid workspaceId, Guid userId, int skip, int take, string? sortBy = null, bool isDescending = true, CancellationToken ct = default);
    Task<(IEnumerable<Project> Items, int TotalCount)> GetByWorkspaceIdPagedExcludingAsync(
        Guid workspaceId, Guid userId, IEnumerable<Guid> excludeProjectIds, int skip, int take, string? sortBy = null, bool isDescending = true, CancellationToken ct = default);
    Task<Project> CreateAsync(Project project, CancellationToken ct = default);
    Task UpdateAsync(Project project, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid id, Guid deletedByUserId, CancellationToken ct = default);
    /// <summary>Asigna o desvincula un proyecto de un workspace sin modificar otros campos.</summary>
    Task SetWorkspaceAsync(Guid projectId, Guid? workspaceId, CancellationToken ct = default);
    /// <summary>Actualiza configuraciones del proyecto (ej. partners_enabled).</summary>
    Task UpdateSettingsAsync(Guid projectId, bool? partnersEnabled, CancellationToken ct = default);
}
