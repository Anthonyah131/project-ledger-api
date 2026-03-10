using Microsoft.AspNetCore.Authorization;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Authorization.Handlers;

/// <summary>
/// Authorization Handler que valida permisos del plan del usuario.
/// 
/// Flujo:
/// 1. Extrae userId del JWT (claim "sub")
/// 2. Consulta IPlanAuthorizationService.HasPermissionAsync
/// 3. Si tiene el permiso → context.Succeed()
/// 
/// Se registra automáticamente en DI y se activa cuando un endpoint
/// tiene [Authorize(Policy = "Plan:CanExportData")] (o cualquier PlanPermission).
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
            return; // Requisito no satisfecho → 403
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
            // No hacer Succeed → se niega el acceso
        }
    }
}
