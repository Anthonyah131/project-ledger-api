using System.ComponentModel.DataAnnotations;

namespace ProjectLedger.API.DTOs.ProjectPartner;

// ── Requests ────────────────────────────────────────────────

/// <summary>Request to assign a partner to a project.</summary>
public class AddProjectPartnerRequest
{
    [Required]
    public Guid PartnerId { get; set; }
}

// ── Responses ───────────────────────────────────────────────

/// <summary>Partner assigned to a project.</summary>
public class ProjectPartnerResponse
{
    public Guid Id { get; set; }
    public Guid PartnerId { get; set; }
    public string PartnerName { get; set; } = null!;
    public string? PartnerEmail { get; set; }
    public DateTime AddedAt { get; set; }
}

/// <summary>Available payment methods in a project, grouped by partner.</summary>
public class AvailablePaymentMethodsResponse
{
    public Guid ProjectId { get; set; }
    /// <summary>User payment methods with no assigned partner — available in any project.</summary>
    public IReadOnlyList<ProjectPaymentMethodItem> UnpartneredPaymentMethods { get; set; } = [];
    /// <summary>Partners assigned to the project with their payment methods.</summary>
    public IReadOnlyList<PartnerWithPaymentMethods> Partners { get; set; } = [];
}

/// <summary>Partner with their available payment methods in the project context.</summary>
public class PartnerWithPaymentMethods
{
    public Guid PartnerId { get; set; }
    public string PartnerName { get; set; } = null!;
    public IReadOnlyList<ProjectPaymentMethodItem> PaymentMethods { get; set; } = [];
}

/// <summary>Payment method available in the project.</summary>
public class ProjectPaymentMethodItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string Currency { get; set; } = null!;
    public string? BankName { get; set; }
}

/// <summary>
/// Linkable payment method to the project: belongs to a partner assigned to the project
/// and is not yet linked to it.
/// </summary>
public class LinkablePaymentMethodResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string Currency { get; set; } = null!;
    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }
    public Guid PartnerId { get; set; }
    public string PartnerName { get; set; } = null!;
}
