namespace ProjectLedger.API.DTOs.Expense;

// ── Requests ────────────────────────────────────────────────

/// <summary>
/// Request para crear un gasto.
/// NO incluye ProjectId (viene de la ruta) ni CreatedByUserId (viene del JWT).
/// Esto previene escalamiento de privilegios.
/// </summary>
public class CreateExpenseRequest
{
    public Guid CategoryId { get; set; }
    public Guid PaymentMethodId { get; set; }
    public Guid? ObligationId { get; set; }

    // ── Montos ──────────────────────────────────────────────
    public decimal OriginalAmount { get; set; }
    public string OriginalCurrency { get; set; } = null!;       // ISO 4217
    public decimal ExchangeRate { get; set; } = 1.000000m;

    // ── Datos descriptivos ──────────────────────────────────
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public DateOnly ExpenseDate { get; set; }
    public string? ReceiptNumber { get; set; }
    public string? Notes { get; set; }
    public bool IsTemplate { get; set; }

    // ── Moneda alternativa (opcional) ───────────────────────
    public string? AltCurrency { get; set; }
    public decimal? AltExchangeRate { get; set; }
}

/// <summary>Request para actualizar un gasto.</summary>
public class UpdateExpenseRequest
{
    public Guid CategoryId { get; set; }
    public Guid PaymentMethodId { get; set; }
    public decimal OriginalAmount { get; set; }
    public string OriginalCurrency { get; set; } = null!;
    public decimal ExchangeRate { get; set; } = 1.000000m;

    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public DateOnly ExpenseDate { get; set; }
    public string? ReceiptNumber { get; set; }
    public string? Notes { get; set; }

    public string? AltCurrency { get; set; }
    public decimal? AltExchangeRate { get; set; }
}

/// <summary>
/// Request para crear un gasto real a partir de una plantilla.
/// Permite sobreescribir monto, fecha y obligación;
/// el resto se toma de la plantilla.
/// </summary>
public class CreateFromTemplateRequest
{
    public decimal? OriginalAmount { get; set; }
    public DateOnly? ExpenseDate { get; set; }
    public Guid? ObligationId { get; set; }
    public string? Notes { get; set; }
}

// ── Responses ───────────────────────────────────────────────

/// <summary>Respuesta con los datos de un gasto.</summary>
public class ExpenseResponse
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public Guid PaymentMethodId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? ObligationId { get; set; }

    public decimal OriginalAmount { get; set; }
    public string OriginalCurrency { get; set; } = null!;
    public decimal ExchangeRate { get; set; }
    public decimal ConvertedAmount { get; set; }

    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public DateOnly ExpenseDate { get; set; }
    public string? ReceiptNumber { get; set; }
    public string? Notes { get; set; }
    public bool IsTemplate { get; set; }

    public string? AltCurrency { get; set; }
    public decimal? AltExchangeRate { get; set; }
    public decimal? AltAmount { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // ── Soft delete info (solo visible con includeDeleted=true) ──
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }
}
