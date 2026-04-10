using System.ComponentModel.DataAnnotations;

namespace ProjectLedger.API.DTOs.ProjectBudget;

// ── Requests ────────────────────────────────────────────────

/// <summary>
/// Request to create/update the project budget.
/// DOES NOT include ProjectId (comes from route).
/// Only one active budget per project.
/// </summary>
public class SetProjectBudgetRequest
{
    [Required]
    [Range(0.01, 999999999999.99, ErrorMessage = "Total budget must be between 0.01 and 999,999,999,999.99.")]
    public decimal TotalBudget { get; set; }

    [Range(1.00, 100.00, ErrorMessage = "Alert percentage must be between 1 and 100.")]
    public decimal AlertPercentage { get; set; } = 80.00m;
}

// ── Responses ───────────────────────────────────────────────

/// <summary>Response with project budget data.</summary>
public class ProjectBudgetResponse
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public decimal TotalBudget { get; set; }
    public decimal AlertPercentage { get; set; }

    // ── Calculated fields (app-level) ───────────────────────
    public decimal SpentAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public decimal SpentPercentage { get; set; }
    public bool IsAlertTriggered { get; set; }

    /// <summary>
    /// Multi-level alert: normal (&lt;70%), warning (70-89%), critical (90-99%), exceeded (≥100%).
    /// </summary>
    public string AlertLevel { get; set; } = "normal";

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
