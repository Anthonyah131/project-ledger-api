using Microsoft.AspNetCore.Authorization;

namespace ProjectLedger.API.Authorization.Requirements;

/// <summary>
/// Requisito de autorización que valida que el usuario autenticado
/// sea miembro del proyecto con al menos el rol mínimo requerido.
/// Se usa con policies: "ProjectViewer", "ProjectEditor", "ProjectOwner".
/// </summary>
public class ProjectMemberRequirement : IAuthorizationRequirement
{
    /// <summary>Rol mínimo requerido (viewer, editor, owner).</summary>
    public string MinimumRole { get; }

    public ProjectMemberRequirement(string minimumRole)
    {
        MinimumRole = minimumRole;
    }
}
