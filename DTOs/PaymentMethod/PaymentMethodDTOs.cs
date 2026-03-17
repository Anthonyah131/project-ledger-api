using System.ComponentModel.DataAnnotations;

namespace ProjectLedger.API.DTOs.PaymentMethod;

// ── Requests ────────────────────────────────────────────────

/// <summary>
/// Request para crear un método de pago.
/// NO incluye OwnerUserId (se toma del JWT).
/// </summary>
public class CreatePaymentMethodRequest
{
    [Required]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 255 characters.")]
    public string Name { get; set; } = null!;

    [Required]
    [RegularExpression(@"^(bank|cash|card)$", ErrorMessage = "Type must be 'bank', 'cash', or 'card'.")]
    public string Type { get; set; } = null!;

    [Required]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "Currency must be a 3-character ISO 4217 code.")]
    [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "Currency must be uppercase ISO 4217 (e.g. USD, EUR, CRC).")]
    public string Currency { get; set; } = null!;

    [StringLength(255, ErrorMessage = "BankName cannot exceed 255 characters.")]
    public string? BankName { get; set; }

    [StringLength(100, ErrorMessage = "AccountNumber cannot exceed 100 characters.")]
    public string? AccountNumber { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// Partner dueño de esta cuenta. Opcional — si se provee debe pertenecer al usuario autenticado.
    /// </summary>
    public Guid? PartnerId { get; set; }
}

/// <summary>Request para actualizar un método de pago.</summary>
public class UpdatePaymentMethodRequest
{
    [Required]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 255 characters.")]
    public string Name { get; set; } = null!;

    [Required]
    [RegularExpression(@"^(bank|cash|card)$", ErrorMessage = "Type must be 'bank', 'cash', or 'card'.")]
    public string Type { get; set; } = null!;

    [StringLength(255, ErrorMessage = "BankName cannot exceed 255 characters.")]
    public string? BankName { get; set; }

    [StringLength(100, ErrorMessage = "AccountNumber cannot exceed 100 characters.")]
    public string? AccountNumber { get; set; }

    public string? Description { get; set; }
}

/// <summary>Request para enlazar un partner a un método de pago.</summary>
public class LinkPartnerToPaymentMethodRequest
{
    [Required]
    public Guid PartnerId { get; set; }
}

// ── Responses ───────────────────────────────────────────────

/// <summary>Partner dueño de un método de pago (embebido en PaymentMethodResponse).</summary>
public class PaymentMethodPartnerResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
}

/// <summary>Respuesta con los datos de un método de pago.</summary>
public class PaymentMethodResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string Currency { get; set; } = null!;
    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }
    public string? Description { get; set; }
    public Guid? PartnerId { get; set; }
    /// <summary>Info del partner dueño. Null si el método de pago no tiene partner asignado.</summary>
    public PaymentMethodPartnerResponse? Partner { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Proyecto relacionado a un método de pago.</summary>
public class PaymentMethodProjectResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;
    public string? Description { get; set; }
    public Guid OwnerUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Respuesta de proyectos relacionados a un método de pago.</summary>
public class PaymentMethodProjectsResponse
{
    public IReadOnlyList<PaymentMethodProjectResponse> Items { get; set; } = [];
    public int TotalCount { get; set; }
}

/// <summary>Resumen agregado de uso del método de pago.</summary>
public class PaymentMethodSummaryResponse
{
    public int RelatedExpensesCount { get; set; }
    public int RelatedIncomesCount { get; set; }
    public int RelatedProjectsCount { get; set; }
    public decimal TotalExpenseAmount { get; set; }
    public decimal TotalIncomeAmount { get; set; }
    public string Currency { get; set; } = null!;
}

/// <summary>Balance de una cuenta en un proyecto específico (en moneda de la cuenta).</summary>
public class PaymentMethodBalanceResponse
{
    public Guid PaymentMethodId { get; set; }
    public string PaymentMethodName { get; set; } = null!;
    public string Currency { get; set; } = null!;
    public Guid ProjectId { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal Balance { get; set; }
}
