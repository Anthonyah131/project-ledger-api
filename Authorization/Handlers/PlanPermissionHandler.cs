using Microsoft.AspNetCore.Authorization;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Authorization.Handlers;

/// <summary>
/// Authorization Handler that validates user plan permissions.
/// 
/// Flow:
/// 1. Extracts userId from JWT (claim "sub")
/// 2. Queries IPlanAuthorizationService.HasPermissionAsync
/// 3. If the user has the permission → context.Succeed()
/// 
/// Automatically registered in DI and activated when an endpoint
/// has [Authorize(Policy = "Plan:CanExportData")] (or any PlanPermission).
/// </summary>
public class PlanPermissionHandler : AuthorizationHandler<PlanPermissionRequirement>
{
    private readonly IPlanAuthorizationService _planAuth;
    private readonly ILogger<PlanPermissionHandler> _logger;

    public PlanPermissionHandler(
        IPlanAuthorizationService planAuth,
        ILogger<PlanPermissionHandler> logger)
    {
        _planAuth = planAuth;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PlanPermissionRequirement requirement)
    {
        var userId = context.User.GetUserId();
        if (userId is null)
        {
            _logger.LogWarning("PlanPermissionHandler: No userId in JWT claims.");
            return; // Requirement not met → 403
        }

        try
        {
            var hasPermission = await _planAuth.HasPermissionAsync(
                userId.Value, requirement.Permission);

            if (hasPermission)
            {
                context.Succeed(requirement);
            }
            else
            {
                _logger.LogInformation(
                    "PlanPermissionHandler: User {UserId} denied for permission {Permission}.",
                    userId.Value, requirement.Permission);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "PlanPermissionHandler: Error checking permission {Permission} for user {UserId}.",
                requirement.Permission, userId.Value);
            // Do not make Succeed → access denied
        }
    }
}
