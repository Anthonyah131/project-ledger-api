using Microsoft.AspNetCore.Authorization;

namespace ProjectLedger.API.Authorization.Requirements;

/// <summary>
/// Authorization requirement that validates that the authenticated user
/// is a member of the project with at least the minimum required role.
/// Used with policies: "ProjectViewer", "ProjectEditor", "ProjectOwner".
/// </summary>
public class ProjectMemberRequirement : IAuthorizationRequirement
{
    /// <summary>Minimum required role (viewer, editor, owner).</summary>
    public string MinimumRole { get; }

    public ProjectMemberRequirement(string minimumRole)
    {
        MinimumRole = minimumRole;
    }
}
