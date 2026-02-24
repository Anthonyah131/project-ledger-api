namespace ProjectLedger.API.DTOs.Plan;

// ── Responses ───────────────────────────────────────────────

/// <summary>Respuesta pública de un plan (listado).</summary>
public class PlanResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public PlanPermissionsDto Permissions { get; set; } = null!;
    public PlanLimitsDto? Limits { get; set; }
}

/// <summary>Permisos del plan (flags booleanos).</summary>
public class PlanPermissionsDto
{
    public bool CanCreateProjects { get; set; }
    public bool CanEditProjects { get; set; }
    public bool CanDeleteProjects { get; set; }
    public bool CanShareProjects { get; set; }
    public bool CanExportData { get; set; }
    public bool CanUseAdvancedReports { get; set; }
    public bool CanUseOcr { get; set; }
    public bool CanUseApi { get; set; }
    public bool CanUseMultiCurrency { get; set; }
    public bool CanSetBudgets { get; set; }
}

/// <summary>Límites numéricos del plan (deserializados del JSONB).</summary>
public class PlanLimitsDto
{
    public int? MaxProjects { get; set; }
    public int? MaxExpensesPerMonth { get; set; }
    public int? MaxCategoriesPerProject { get; set; }
    public int? MaxPaymentMethods { get; set; }
    public int? MaxTeamMembersPerProject { get; set; }
}
