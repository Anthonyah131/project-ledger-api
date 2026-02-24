namespace ProjectLedger.API.DTOs.ProjectBudget;

// ── Requests ────────────────────────────────────────────────

/// <summary>
/// Request para crear/actualizar el presupuesto del proyecto.
/// NO incluye ProjectId (viene de la ruta).
/// Solo un presupuesto activo por proyecto.
/// </summary>
public class SetProjectBudgetRequest
{
    public decimal TotalBudget { get; set; }
    public decimal AlertPercentage { get; set; } = 80.00m;     // 1-100%
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

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
