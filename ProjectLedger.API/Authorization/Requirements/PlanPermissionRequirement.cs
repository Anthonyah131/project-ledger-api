using Microsoft.AspNetCore.Authorization;

namespace ProjectLedger.API.Authorization.Requirements;

/// <summary>
/// Requisito de autorización que valida que el plan del usuario
/// tenga habilitado un permiso específico.
/// Se usa con policies de formato "Plan:{PlanPermission}".
/// 
/// Ejemplo:
///   [Authorize(Policy = "Plan:CanExportData")]
/// </summary>
public class PlanPermissionRequirement : IAuthorizationRequirement
{
    /// <summary>Permiso del plan que se requiere.</summary>
    public PlanPermission Permission { get; }

    public PlanPermissionRequirement(PlanPermission permission)
    {
        Permission = permission;
    }
}
