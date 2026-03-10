using System.ComponentModel.DataAnnotations;

namespace ProjectLedger.API.DTOs.ProjectBudget;

// ── Requests ────────────────────────────────────────────────

/// <summary>
/// Request para crear/actualizar el presupuesto del proyecto.
/// NO incluye ProjectId (viene de la ruta).
/// Solo un presupuesto activo por proyecto.
/// </summary>
public class SetProjectBudgetRequest
{
    [Required]
    [Range(0.01, 99999999999999.99, ErrorMessage = "TotalBudget must be greater than 0.")]
    public decimal TotalBudget { get; set; }

    [Range(1.00, 100.00, ErrorMessage = "AlertPercentage must be between 1 and 100.")]
    public decimal AlertPercentage { get; set; } = 80.00m;
}

// ── Responses ───────────────────────────────────────────────

/// <summary>Respuesta con los datos del presupuesto del proyecto.</summary>
public class ProjectBudgetResponse
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public decimal TotalBudget { get; set; }
    public decimal AlertPercentage { get; set; }

    // ── Campos calculados (app-level) ───────────────────────
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
