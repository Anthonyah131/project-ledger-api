using ProjectLedger.API.Models;
using ProjectLedger.API.DTOs.Obligation;

namespace ProjectLedger.API.Extensions.Mappings;

public static class ObligationMappingExtensions
{
    // ── Entity → Response ───────────────────────────────────

    /// <summary>
    /// Convierte una Obligation a su response DTO.
    /// Los campos PaidAmount y Status se calculan a nivel de servicio
    /// en base a los Expenses vinculados.
    /// </summary>
    public static ObligationResponse ToResponse(
        this Obligation entity,
        decimal paidAmount = 0m)
    {
        var remaining = entity.OblTotalAmount - paidAmount;
        var status = paidAmount == 0m
            ? "open"
            : paidAmount >= entity.OblTotalAmount
                ? "paid"
                : "partially_paid";

        // Override to overdue if due date passed and not fully paid
        if (status != "paid"
            && entity.OblDueDate.HasValue
            && entity.OblDueDate.Value < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            status = "overdue";
        }

        return new ObligationResponse
        {
            Id = entity.OblId,
            ProjectId = entity.OblProjectId,
            CreatedByUserId = entity.OblCreatedByUserId,
            Title = entity.OblTitle,
            Description = entity.OblDescription,
            TotalAmount = entity.OblTotalAmount,
            Currency = entity.OblCurrency,
            DueDate = entity.OblDueDate,
            PaidAmount = paidAmount,
            RemainingAmount = remaining < 0 ? 0 : remaining,
            Status = status,
            CreatedAt = entity.OblCreatedAt,
            UpdatedAt = entity.OblUpdatedAt
        };
    }

    // ── Request → Entity ────────────────────────────────────

    public static Obligation ToEntity(
        this CreateObligationRequest request,
        Guid projectId,
        Guid userId) => new()
    {
        OblId = Guid.NewGuid(),
        OblProjectId = projectId,
        OblCreatedByUserId = userId,
        OblTitle = request.Title,
        OblDescription = request.Description,
        OblTotalAmount = request.TotalAmount,
        OblCurrency = request.Currency,
        OblDueDate = request.DueDate,
        OblCreatedAt = DateTime.UtcNow,
        OblUpdatedAt = DateTime.UtcNow
    };

    // ── Apply update from DTO ──────────────────────────────

    public static void ApplyUpdate(this Obligation entity, UpdateObligationRequest request)
    {
        entity.OblTitle = request.Title;
        entity.OblDescription = request.Description;
        entity.OblTotalAmount = request.TotalAmount;
        entity.OblDueDate = request.DueDate;
        entity.OblUpdatedAt = DateTime.UtcNow;
    }
}
