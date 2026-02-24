namespace ProjectLedger.API.Common.Enums;

/// <summary>
/// Enumeración de todos los permisos definidos por plan.
/// Cada valor corresponde a un flag booleano en el modelo <see cref="Models.Plan"/>.
/// </summary>
public enum PlanPermission
{
    CanCreateProjects,
    CanEditProjects,
    CanDeleteProjects,
    CanShareProjects,
    CanExportData,
    CanUseAdvancedReports,
    CanUseOcr,
    CanUseApi,
    CanUseMultiCurrency,
    CanSetBudgets
}
