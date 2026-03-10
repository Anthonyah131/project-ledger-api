using System.ComponentModel.DataAnnotations;

namespace ProjectLedger.API.DTOs.Category;

// ── Requests ────────────────────────────────────────────────

/// <summary>
/// Request para crear una categoría.
/// NO incluye ProjectId (viene de la ruta) para prevenir escalamiento de privilegios.
/// </summary>
public class CreateCategoryRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 100 characters.")]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    [Range(0.01, 99999999999999.99, ErrorMessage = "BudgetAmount must be greater than 0.")]
    public decimal? BudgetAmount { get; set; }
}

/// <summary>Request para actualizar una categoría.</summary>
public class UpdateCategoryRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 100 characters.")]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    [Range(0.01, 99999999999999.99, ErrorMessage = "BudgetAmount must be greater than 0.")]
    public decimal? BudgetAmount { get; set; }
}

// ── Responses ───────────────────────────────────────────────

/// <summary>Respuesta con los datos de una categoría.</summary>
public class CategoryResponse
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public decimal? BudgetAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
