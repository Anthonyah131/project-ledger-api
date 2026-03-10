using Microsoft.AspNetCore.Authorization;
using ProjectLedger.API.Services;

namespace ProjectLedger.API.Authorization.Handlers;

/// <summary>
/// Authorization Handler que valida membresía en un proyecto.
/// Extrae el projectId desde la ruta ({projectId}) y verifica
/// que el usuario autenticado sea miembro con el rol mínimo requerido.
/// 
/// Flujo:
/// 1. Extrae projectId de la ruta
/// 2. Extrae userId del JWT (claim "sub")
/// 3. Verifica acceso vía IProjectAccessService (ownership + membership)
/// 4. Si cumple → context.Succeed()
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
        // El Resource es HttpContext cuando se usa endpoint routing (.NET 6+)
        if (context.Resource is not HttpContext httpContext)
            return;

        // Extraer projectId de la ruta
        var routeData = httpContext.GetRouteData();
        if (!routeData.Values.TryGetValue("projectId", out var projectIdValue) ||
            !Guid.TryParse(projectIdValue?.ToString(), out var projectId))
            return;     // Sin projectId en ruta → requisito no satisfecho

        // Extraer userId del JWT
        var userId = context.User.GetUserId();
        if (userId == null) return;

        // Verificar acceso (ownership + membership + rol mínimo)
        if (await _accessService.HasAccessAsync(userId.Value, projectId, requirement.MinimumRole))
            context.Succeed(requirement);
    }
}
