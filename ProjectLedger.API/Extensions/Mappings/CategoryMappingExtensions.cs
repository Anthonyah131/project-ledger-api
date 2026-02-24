using ProjectLedger.API.Models;
using ProjectLedger.API.DTOs.Category;

namespace ProjectLedger.API.Extensions.Mappings;

public static class CategoryMappingExtensions
{
    // ── Entity → Response ───────────────────────────────────

    public static CategoryResponse ToResponse(this Category entity) => new()
    {
        Id = entity.CatId,
        ProjectId = entity.CatProjectId,
        Name = entity.CatName,
        Description = entity.CatDescription,
        IsDefault = entity.CatIsDefault,
        BudgetAmount = entity.CatBudgetAmount,
        CreatedAt = entity.CatCreatedAt,
        UpdatedAt = entity.CatUpdatedAt
    };

    // ── Request → Entity ────────────────────────────────────

    public static Category ToEntity(this CreateCategoryRequest request, Guid projectId) => new()
    {
        CatId = Guid.NewGuid(),
        CatProjectId = projectId,
        CatName = request.Name,
        CatDescription = request.Description,
        CatBudgetAmount = request.BudgetAmount,
        CatCreatedAt = DateTime.UtcNow,
        CatUpdatedAt = DateTime.UtcNow
    };

    // ── Apply update from DTO ──────────────────────────────

    public static void ApplyUpdate(this Category entity, UpdateCategoryRequest request)
    {
        entity.CatName = request.Name;
        entity.CatDescription = request.Description;
        entity.CatBudgetAmount = request.BudgetAmount;
        entity.CatUpdatedAt = DateTime.UtcNow;
    }

    // ── Collection helpers ──────────────────────────────────

    public static IEnumerable<CategoryResponse> ToResponse(this IEnumerable<Category> entities)
        => entities.Select(e => e.ToResponse());
}
