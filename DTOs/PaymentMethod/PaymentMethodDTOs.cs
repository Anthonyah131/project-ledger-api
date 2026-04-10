using System.ComponentModel.DataAnnotations;

namespace ProjectLedger.API.DTOs.PaymentMethod;

// ── Requests ────────────────────────────────────────────────

/// <summary>
/// Request to create a payment method.
/// DOES NOT include OwnerUserId (taken from JWT).
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

    [StringLength(255, ErrorMessage = "Bank name cannot exceed 255 characters.")]
    public string? BankName { get; set; }

    [StringLength(100, ErrorMessage = "Account number cannot exceed 100 characters.")]
    public string? AccountNumber { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// Partner owning this account. Optional — if provided it must belong to the authenticated user.
    /// </summary>
    public Guid? PartnerId { get; set; }
}

/// <summary>Request to update a payment method.</summary>
public class UpdatePaymentMethodRequest
{
    [Required]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 255 characters.")]
    public string Name { get; set; } = null!;

    [Required]
    [RegularExpression(@"^(bank|cash|card)$", ErrorMessage = "Type must be 'bank', 'cash', or 'card'.")]
    public string Type { get; set; } = null!;

    [StringLength(255, ErrorMessage = "Bank name cannot exceed 255 characters.")]
    public string? BankName { get; set; }

    [StringLength(100, ErrorMessage = "Account number cannot exceed 100 characters.")]
    public string? AccountNumber { get; set; }

    public string? Description { get; set; }
}

/// <summary>Request to link a partner to a payment method.</summary>
public class LinkPartnerToPaymentMethodRequest
{
    [Required]
    public Guid PartnerId { get; set; }
}

// ── Responses ───────────────────────────────────────────────

/// <summary>Partner owning a payment method (embedded in PaymentMethodResponse).</summary>
public class PaymentMethodPartnerResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
}

/// <summary>Response with the data of a payment method.</summary>
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
    /// <summary>Owning partner's info. Null if the payment method does not have an assigned partner.</summary>
    public PaymentMethodPartnerResponse? Partner { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Project related to a payment method.</summary>
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

/// <summary>Response of projects related to a payment method.</summary>
public class PaymentMethodProjectsResponse
{
    public IReadOnlyList<PaymentMethodProjectResponse> Items { get; set; } = [];
    public int TotalCount { get; set; }
}

/// <summary>Aggregated usage summary of the payment method.</summary>
public class PaymentMethodSummaryResponse
{
    public int RelatedExpensesCount { get; set; }
    public int RelatedIncomesCount { get; set; }
    public int RelatedProjectsCount { get; set; }
    public decimal TotalExpenseAmount { get; set; }
    public decimal TotalIncomeAmount { get; set; }
    public string Currency { get; set; } = null!;
}

// ── Payment Method Lookup ────────────────────────────────────

/// <summary>Minimal payment method item for selectors and command palette.</summary>
public class PaymentMethodLookupItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string Currency { get; set; } = null!;
}

/// <summary>Paginated response from the payment methods lookup.</summary>
public class PaymentMethodLookupResponse
{
    public IReadOnlyList<PaymentMethodLookupItem> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

/// <summary>Account balance in a specific project (in the account's currency).</summary>
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
