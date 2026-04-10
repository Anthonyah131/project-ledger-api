namespace ProjectLedger.API.Common.Enums;

/// <summary>
/// Enumeration of all permissions defined by plan.
/// Each value corresponds to a boolean flag in the <see cref="Models.Plan"/> model.
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
    CanSetBudgets,
    CanUsePartners
}
