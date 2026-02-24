using ProjectLedger.API.Models;
using ProjectLedger.API.DTOs.ProjectBudget;

namespace ProjectLedger.API.Extensions.Mappings;

public static class ProjectBudgetMappingExtensions
{
    // ── Entity → Response ───────────────────────────────────

    /// <summary>
    /// Convierte un ProjectBudget a su response DTO.
    /// SpentAmount se calcula a nivel de servicio (suma de gastos del proyecto).
    /// </summary>
    public static ProjectBudgetResponse ToResponse(
        this ProjectBudget entity,
        decimal spentAmount = 0m)
    {
        var remaining = entity.PjbTotalBudget - spentAmount;
        var spentPct = entity.PjbTotalBudget > 0
            ? Math.Round(spentAmount / entity.PjbTotalBudget * 100, 2)
            : 0m;

        return new ProjectBudgetResponse
        {
            Id = entity.PjbId,
            ProjectId = entity.PjbProjectId,
            TotalBudget = entity.PjbTotalBudget,
            AlertPercentage = entity.PjbAlertPercentage,
            SpentAmount = spentAmount,
            RemainingAmount = remaining < 0 ? 0 : remaining,
            SpentPercentage = spentPct,
            IsAlertTriggered = spentPct >= entity.PjbAlertPercentage,
            CreatedAt = entity.PjbCreatedAt,
            UpdatedAt = entity.PjbUpdatedAt
        };
    }

    // ── Request → Entity ────────────────────────────────────

    public static ProjectBudget ToEntity(this SetProjectBudgetRequest request, Guid projectId) => new()
    {
        PjbId = Guid.NewGuid(),
        PjbProjectId = projectId,
        PjbTotalBudget = request.TotalBudget,
        PjbAlertPercentage = request.AlertPercentage,
        PjbCreatedAt = DateTime.UtcNow,
        PjbUpdatedAt = DateTime.UtcNow
    };

    // ── Apply update from DTO ──────────────────────────────

    public static void ApplyUpdate(this ProjectBudget entity, SetProjectBudgetRequest request)
    {
        entity.PjbTotalBudget = request.TotalBudget;
        entity.PjbAlertPercentage = request.AlertPercentage;
        entity.PjbUpdatedAt = DateTime.UtcNow;
    }
}
