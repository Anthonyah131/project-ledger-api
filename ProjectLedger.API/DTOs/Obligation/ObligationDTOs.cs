namespace ProjectLedger.API.DTOs.Obligation;

// ── Requests ────────────────────────────────────────────────

/// <summary>
/// Request para crear una obligación/deuda.
/// NO incluye ProjectId (viene de la ruta) ni CreatedByUserId (viene del JWT).
/// </summary>
public class CreateObligationRequest
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = null!;               // ISO 4217
    public DateOnly? DueDate { get; set; }
}

/// <summary>Request para actualizar una obligación.</summary>
public class UpdateObligationRequest
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public decimal TotalAmount { get; set; }
    public DateOnly? DueDate { get; set; }
}

// ── Responses ───────────────────────────────────────────────

/// <summary>Respuesta con los datos de una obligación/deuda.</summary>
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

    // ── Campos calculados (app-level) ───────────────────────
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public string Status { get; set; } = null!;                 // open, partially_paid, paid, overdue

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
