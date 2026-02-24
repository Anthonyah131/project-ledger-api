namespace ProjectLedger.API.Common.Constants;

/// <summary>
/// Constantes de nombres de límites del plan.
/// Deben coincidir exactamente con las keys del JSONB PlnLimits.
/// Usar estas constantes tanto en PlanAuthorizationService como en los servicios
/// que validan límites, para evitar magic strings.
/// </summary>
public static class PlanLimits
{
    public const string MaxProjects              = "max_projects";
    public const string MaxExpensesPerMonth      = "max_expenses_per_month";
    public const string MaxCategoriesPerProject  = "max_categories_per_project";
    public const string MaxPaymentMethods        = "max_payment_methods";
    public const string MaxTeamMembersPerProject = "max_team_members_per_project";
}
