
namespace ProjectLedger.API.Services;

/// <summary>
/// Service for verifying permissions and limits based on the user's plan.
/// 
/// Two modes of use:
/// 1. IMPERATIVE (in services/controllers):
///    <code>await _planAuth.ValidatePermissionAsync(userId, PlanPermission.CanExportData);</code>
///    Throws PlanDeniedException if the user doesn't have the permission.
/// 
/// 2. DECLARATIVE (in controllers with Authorization Policies):
///    <code>[Authorize(Policy = "Plan:CanExportData")]</code>
///    Handled automatically by the PlanPermissionHandler.
/// </summary>
public interface IPlanAuthorizationService
{
    // ── Boolean Permissions ──────────────────────────────────

    /// <summary>
    /// Verifies if the user's plan allows the specified action.
    /// Returns true/false without throwing an exception.
    /// </summary>
    Task<bool> HasPermissionAsync(
        Guid userId,
        PlanPermission permission,
        CancellationToken ct = default);

    /// <summary>
    /// Same as HasPermissionAsync but throws PlanDeniedException if permission is missing.
    /// Ideal for imperative validation in services.
    /// </summary>
    Task ValidatePermissionAsync(
        Guid userId,
        PlanPermission permission,
        CancellationToken ct = default);

    // ── Numeric Limits ───────────────────────────────────

    /// <summary>
    /// Verifies if the user can create more entities of the specified type
    /// based on their plan's limits. If the limit is null, it is considered unlimited.
    /// </summary>
    Task<bool> IsWithinLimitAsync(
        Guid userId,
        string limitName,
        int currentCount,
        CancellationToken ct = default);

    /// <summary>
    /// Same as IsWithinLimitAsync but throws PlanLimitExceededException if exceeded.
    /// </summary>
    Task ValidateLimitAsync(
        Guid userId,
        string limitName,
        int currentCount,
        CancellationToken ct = default);

    // ── Project Write Access Validation ─────────────────

    /// <summary>
    /// Validates that the plan of the project owner allows write operations.
    /// <list type="bullet">
    ///   <item>Always verifies <see cref="PlanPermission.CanEditProjects"/> for the owner.</item>
    ///   <item>If the acting user is NOT the owner (shared member), 
    ///         it also verifies <see cref="PlanPermission.CanShareProjects"/>.</item>
    /// </list>
    /// Throws <see cref="PlanDeniedException"/> if requirements are not met.
    /// Key scenario: if the owner downgraded to Free, shared members 
    /// can no longer create/edit/delete resources in that project.
    /// </summary>
    Task ValidateProjectWriteAccessAsync(
        Guid projectId,
        Guid actingUserId,
        CancellationToken ct = default);

    // ── Full Plan Capability Retrieval ───────────────────

    /// <summary>
    /// Retrieves a complete summary of permissions and limits for the user's plan.
    /// Useful for the frontend to show/hide available features.
    /// </summary>
    Task<PlanCapabilities> GetCapabilitiesAsync(
        Guid userId,
        CancellationToken ct = default);
}

/// <summary>
/// Summary of a user's plan capabilities.
/// Returned to the frontend to control feature visibility.
/// </summary>
public class PlanCapabilities
{
    public string PlanName { get; set; } = null!;
    public string PlanSlug { get; set; } = null!;

    // ── Permissions ────────────────────────────────────────────
    public Dictionary<string, bool> Permissions { get; set; } = new();

    // ── Limits (null = unlimited) ──────────────────────────
    public Dictionary<string, int?> Limits { get; set; } = new();
}
