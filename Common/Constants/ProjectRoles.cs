namespace ProjectLedger.API.Common.Constants;

/// <summary>
/// Project role constants. Privilege order: Owner > Editor > Viewer.
/// Used in ProjectMember.PrmRole and in authorization policies.
/// </summary>
public static class ProjectRoles
{
    public const string Owner  = "owner";
    public const string Editor = "editor";
    public const string Viewer = "viewer";

    /// <summary>
    /// Checks if the user's role has equal or higher privileges than the required role.
    /// </summary>
    public static bool HasMinimumRole(string userRole, string requiredRole)
    {
        return GetRoleLevel(userRole) >= GetRoleLevel(requiredRole);
    }

    private static int GetRoleLevel(string role) => role.ToLowerInvariant() switch
    {
        Owner  => 3,
        Editor => 2,
        Viewer => 1,
        _      => 0
    };
}
