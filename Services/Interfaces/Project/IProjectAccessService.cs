
namespace ProjectLedger.API.Services;

/// <summary>
/// Service for validating project access.
/// Centralizes the verification logic for membership and roles.
/// Used by Authorization Handlers and by the service layer (imperative validation).
/// </summary>
public interface IProjectAccessService
{
    /// <summary>
    /// Verifies if the user has at least the specified role in the project.
    /// Considers both ownership and membership.
    /// </summary>
    Task<bool> HasAccessAsync(
        Guid userId,
        Guid projectId,
        string minimumRole = ProjectRoles.Viewer,
        CancellationToken ct = default);

    /// <summary>
    /// Same as HasAccessAsync but throws ForbiddenAccessException if access is denied.
    /// Used in the Application/Service layer for imperative validation.
    /// </summary>
    Task ValidateAccessAsync(
        Guid userId,
        Guid projectId,
        string minimumRole = ProjectRoles.Viewer,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves the effective role of the user in the project.
    /// Returns null if the user has no access.
    /// </summary>
    Task<string?> GetUserRoleAsync(
        Guid userId,
        Guid projectId,
        CancellationToken ct = default);
}
