using Microsoft.AspNetCore.Authorization;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Authorization.Handlers;

/// <summary>
/// Authorization Handler that validates project membership.
/// Extracts projectId from the route ({projectId}) and verifies
/// that the authenticated user is a member with the minimum required role.
/// 
/// Flow:
/// 1. Extracts projectId from the route
/// 2. Extracts userId from JWT (claim "sub")
/// 3. Verifies access via IProjectAccessService (ownership + membership)
/// 4. If it meets → context.Succeed()
/// </summary>
public class ProjectMemberHandler : AuthorizationHandler<ProjectMemberRequirement>
{
    private readonly IProjectAccessService _accessService;

    public ProjectMemberHandler(IProjectAccessService accessService)
    {
        _accessService = accessService;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ProjectMemberRequirement requirement)
    {
        // The Resource is HttpContext when using endpoint routing (.NET 6+)
        if (context.Resource is not HttpContext httpContext)
            return;

        // Extract projectId from the route
        var routeData = httpContext.GetRouteData();
        if (!routeData.Values.TryGetValue("projectId", out var projectIdValue) ||
            !Guid.TryParse(projectIdValue?.ToString(), out var projectId))
            return;     // Without projectId in route → requirement not met

        // Extract userId from JWT
        var userId = context.User.GetUserId();
        if (userId == null) return;

        // Verify access (ownership + membership + minimum role)
        if (await _accessService.HasAccessAsync(userId.Value, projectId, requirement.MinimumRole))
            context.Succeed(requirement);
    }
}
