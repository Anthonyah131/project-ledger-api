namespace ProjectLedger.API.Common.Constants;

/// <summary>
/// Constantes de roles de proyecto. Orden de privilegio: Owner > Editor > Viewer.
/// Usados en ProjectMember.PrmRole y en las policies de autorización.
/// </summary>
public static class ProjectRoles
{
    public const string Owner  = "owner";
    public const string Editor = "editor";
    public const string Viewer = "viewer";

    /// <summary>
    /// Verifica si el rol del usuario tiene privilegios iguales o superiores al requerido.
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
