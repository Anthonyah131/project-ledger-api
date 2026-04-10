using System.ComponentModel.DataAnnotations;

namespace ProjectLedger.API.DTOs.Partner;

// ── Requests ────────────────────────────────────────────────

/// <summary>Request to create a partner.</summary>
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

/// <summary>Request to update a partner.</summary>
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

/// <summary>Response with the data of a partner.</summary>
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

/// <summary>Response of a partner with their payment methods and related projects.</summary>
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
    /// <summary>Projects where at least one of the partner's payment methods is linked.</summary>
    public IReadOnlyList<PartnerProjectResponse> Projects { get; set; } = [];
}

/// <summary>Project related to the partner (through their payment methods).</summary>
public class PartnerProjectResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string CurrencyCode { get; set; } = null!;
    public string? Description { get; set; }
    public Guid? WorkspaceId { get; set; }
    public string? WorkspaceName { get; set; }
}

/// <summary>Simplified payment method within the context of a partner.</summary>
public class PartnerPaymentMethodResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string Currency { get; set; } = null!;
    public string? BankName { get; set; }
}
