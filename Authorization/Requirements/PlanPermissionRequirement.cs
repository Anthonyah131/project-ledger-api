using Microsoft.AspNetCore.Authorization;

namespace ProjectLedger.API.Authorization.Requirements;

/// <summary>
/// Authorization requirement that validates that the user's plan
/// has a specific permission enabled.
/// Used with policies of format "Plan:{PlanPermission}".
/// 
/// Example:
///   [Authorize(Policy = "Plan:CanExportData")]
/// </summary>
public class PlanPermissionRequirement : IAuthorizationRequirement
{
    /// <summary>Plan permission that is required.</summary>
    public PlanPermission Permission { get; }

    public PlanPermissionRequirement(PlanPermission permission)
    {
        Permission = permission;
    }
}
