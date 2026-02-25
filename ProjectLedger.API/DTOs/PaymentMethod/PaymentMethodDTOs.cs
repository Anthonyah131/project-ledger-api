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

// ── Responses ───────────────────────────────────────────────

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
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
