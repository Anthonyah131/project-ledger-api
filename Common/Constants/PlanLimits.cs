namespace ProjectLedger.API.Common.Constants;

/// <summary>
/// Plan limit name constants.
/// Must match exactly the keys in the PlnLimits JSONB.
/// Use these constants in both PlanAuthorizationService and the services
/// que validan límites, para evitar magic strings.
/// </summary>
public static class PlanLimits
{
    public const string MaxProjects              = "max_projects";
    public const string MaxExpensesPerMonth      = "max_expenses_per_month";
    public const string MaxCategoriesPerProject  = "max_categories_per_project";
    public const string MaxPaymentMethods        = "max_payment_methods";
    public const string MaxTeamMembersPerProject = "max_team_members_per_project";
    public const string MaxAlternativeCurrenciesPerProject = "max_alternative_currencies_per_project";
    public const string MaxIncomesPerMonth       = "max_incomes_per_month";
    public const string MaxDocumentReadsPerMonth = "max_document_reads_per_month";
}
