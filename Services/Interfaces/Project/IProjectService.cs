using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

public interface IProjectService
{
    /// <summary>
    /// Gets a project by ID.
    /// </summary>
    Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Lists all projects owned by a specific user.
    /// </summary>
    Task<IEnumerable<Project>> GetByOwnerUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Lists all projects where the user is a member (excluding ownership).
    /// </summary>
    Task<IEnumerable<Project>> GetByMemberUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a paginated list of projects accessible to the user.
    /// </summary>
    Task<(IEnumerable<Project> Items, int TotalCount)> GetByUserIdPagedAsync(
        Guid userId, int skip, int take, string? sortBy = null, bool isDescending = true, CancellationToken ct = default);

    /// <summary>
    /// Retrieves only the pinned projects (memberships) for a user.
    /// </summary>
    Task<IEnumerable<ProjectMember>> GetPinnedMembershipsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a paginated list of projects accessible to the user, excluding specific IDs.
    /// </summary>
    Task<(IEnumerable<Project> Items, int TotalCount)> GetByUserIdPagedExcludingAsync(
        Guid userId, IEnumerable<Guid> excludeProjectIds, int skip, int take, string? sortBy = null, bool isDescending = true, CancellationToken ct = default);

    /// <summary>
    /// Pins a project for the user, marking it as a favorite for quick access.
    /// </summary>
    Task<DateTime> PinProjectAsync(Guid userId, Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Unpins a project for the user.
    /// </summary>
    Task UnpinProjectAsync(Guid userId, Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a paginated list of projects within a specific workspace.
    /// </summary>
    Task<(IEnumerable<Project> Items, int TotalCount)> GetByWorkspaceIdPagedAsync(
        Guid workspaceId, Guid userId, int skip, int take, string? sortBy = null, bool isDescending = true, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a paginated list of projects within a workspace, excluding specific IDs.
    /// </summary>
    Task<(IEnumerable<Project> Items, int TotalCount)> GetByWorkspaceIdPagedExcludingAsync(
        Guid workspaceId, Guid userId, IEnumerable<Guid> excludeProjectIds, int skip, int take, string? sortBy = null, bool isDescending = true, CancellationToken ct = default);

    /// <summary>
    /// Creates a new project.
    /// </summary>
    Task<Project> CreateAsync(Project project, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing project's metadata.
    /// </summary>
    Task UpdateAsync(Project project, CancellationToken ct = default);

    /// <summary>
    /// Soft-deletes a project.
    /// </summary>
    Task SoftDeleteAsync(Guid id, Guid deletedByUserId, CancellationToken ct = default);

    /// <summary>Assigns or unlinks a project from a workspace without modifying other fields.</summary>
    Task SetWorkspaceAsync(Guid projectId, Guid? workspaceId, CancellationToken ct = default);

    /// <summary>Updates project settings (e.g., partners_enabled).</summary>
    Task UpdateSettingsAsync(Guid projectId, bool? partnersEnabled, CancellationToken ct = default);

    /// <summary>
    /// Lightweight lookup for command palette and selectors.
    /// Returns pinned projects filtered by search (page 1 only), total pinned count, and paginated unpinned items.
    /// </summary>
    Task<(IEnumerable<ProjectMember> PinnedFiltered, int PinnedTotalCount, IEnumerable<Project> Items, int TotalCount)>
        GetProjectsLookupAsync(Guid userId, string? search, int page, int skip, int take, CancellationToken ct = default);
}
