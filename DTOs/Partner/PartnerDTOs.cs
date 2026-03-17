using System.ComponentModel.DataAnnotations;

namespace ProjectLedger.API.DTOs.Partner;

// ── Requests ────────────────────────────────────────────────

/// <summary>Request para crear un partner.</summary>
public class CreatePartnerRequest
{
    [Required]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 255 characters.")]
    public string Name { get; set; } = null!;

    [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string? Email { get; set; }

    [StringLength(50, ErrorMessage = "Phone cannot exceed 50 characters.")]
    public string? Phone { get; set; }

    public string? Notes { get; set; }
}

/// <summary>Request para actualizar un partner.</summary>
public class UpdatePartnerRequest
{
    [Required]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 255 characters.")]
    public string Name { get; set; } = null!;

    [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string? Email { get; set; }

    [StringLength(50, ErrorMessage = "Phone cannot exceed 50 characters.")]
    public string? Phone { get; set; }

    public string? Notes { get; set; }
}

// ── Responses ───────────────────────────────────────────────

/// <summary>Respuesta con los datos de un partner.</summary>
public class PartnerResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Respuesta de un partner con sus métodos de pago y proyectos relacionados.</summary>
public class PartnerDetailResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public IReadOnlyList<PartnerPaymentMethodResponse> PaymentMethods { get; set; } = [];
    /// <summary>Proyectos donde al menos un método de pago del partner está vinculado.</summary>
    public IReadOnlyList<PartnerProjectResponse> Projects { get; set; } = [];
}

/// <summary>Proyecto relacionado al partner (a través de sus métodos de pago).</summary>
public class PartnerProjectResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;
    public string? Description { get; set; }
    public Guid? WorkspaceId { get; set; }
    public string? WorkspaceName { get; set; }
}

/// <summary>Método de pago simplificado dentro del contexto de un partner.</summary>
public class PartnerPaymentMethodResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string Currency { get; set; } = null!;
    public string? BankName { get; set; }
}
