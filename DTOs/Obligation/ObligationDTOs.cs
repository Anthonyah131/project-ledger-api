using System.ComponentModel.DataAnnotations;

namespace ProjectLedger.API.DTOs.Obligation;

// ── Requests ────────────────────────────────────────────────

/// <summary>
/// Request to create an obligation/debt.
/// DOES NOT include ProjectId (comes from route) nor CreatedByUserId (comes from JWT).
/// </summary>
public class CreateObligationRequest
{
    [Required]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 255 characters.")]
    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    [Required]
    [Range(0.01, 999999999999.99, ErrorMessage = "Total amount must be between 0.01 and 999,999,999,999.99.")]
    public decimal TotalAmount { get; set; }

    [Required]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "Currency must be a 3-character ISO 4217 code.")]
    [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "Currency must be uppercase ISO 4217 (e.g. USD, EUR, CRC).")]
    public string Currency { get; set; } = null!;

    public DateOnly? DueDate { get; set; }
}

/// <summary>Request to update an obligation.</summary>
public class UpdateObligationRequest
{
    [Required]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 255 characters.")]
    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    [Required]
    [Range(0.01, 999999999999.99, ErrorMessage = "Total amount must be between 0.01 and 999,999,999,999.99.")]
    public decimal TotalAmount { get; set; }

    public DateOnly? DueDate { get; set; }
}

// ── Responses ───────────────────────────────────────────────

/// <summary>Response with the data of an obligation/debt.</summary>
public class ObligationResponse
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = null!;
    public DateOnly? DueDate { get; set; }

    // ── Calculated fields (app-level) ───────────────────────
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public string Status { get; set; } = null!;                 // open, partially_paid, paid, overdue

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
