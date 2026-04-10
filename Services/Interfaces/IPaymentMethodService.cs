using ProjectLedger.API.DTOs.PaymentMethod;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

public interface IPaymentMethodService
{
    /// <summary>
    /// Gets a payment method by ID.
    /// </summary>
    Task<PaymentMethod?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Gets all payment methods owned by a user.
    /// </summary>
    Task<IEnumerable<PaymentMethod>> GetByOwnerUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Creates a new payment method.
    /// </summary>
    Task<PaymentMethod> CreateAsync(PaymentMethod paymentMethod, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing payment method's metadata.
    /// </summary>
    Task UpdateAsync(PaymentMethod paymentMethod, CancellationToken ct = default);

    /// <summary>
    /// Soft-deletes a payment method.
    /// </summary>
    Task SoftDeleteAsync(Guid id, Guid deletedByUserId, CancellationToken ct = default);

    /// <summary>
    /// Calculates the balance of a specific payment method in the context of a project.
    /// </summary>
    Task<PaymentMethodBalanceResponse> GetProjectBalanceAsync(Guid pmtId, Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Links a financial partner to the payment method ownership.
    /// </summary>
    Task<PaymentMethod> LinkPartnerAsync(Guid pmtId, Guid partnerId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Unlinks the partner from the payment method.
    /// </summary>
    Task<PaymentMethod> UnlinkPartnerAsync(Guid pmtId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a paginated and searchable list of payment methods for lookup selectors.
    /// </summary>
    Task<(IEnumerable<PaymentMethod> Items, int TotalCount)>
        GetLookupAsync(Guid userId, string? search, int skip, int take, CancellationToken ct = default);
}
