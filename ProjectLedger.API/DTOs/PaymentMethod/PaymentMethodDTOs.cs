namespace ProjectLedger.API.DTOs.PaymentMethod;

// ── Requests ────────────────────────────────────────────────

/// <summary>
/// Request para crear un método de pago.
/// NO incluye OwnerUserId (se toma del JWT).
/// </summary>
public class CreatePaymentMethodRequest
{
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;                   // 'bank', 'cash', 'card'
    public string Currency { get; set; } = null!;               // ISO 4217
    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }
    public string? Description { get; set; }
}

/// <summary>Request para actualizar un método de pago.</summary>
public class UpdatePaymentMethodRequest
{
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string? BankName { get; set; }
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
