using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

public interface IPartnerService
{
    /// <summary>
    /// Gets a partner by ID.
    /// </summary>
    Task<Partner?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Gets a partner by ID including their linked payment methods.
    /// </summary>
    Task<Partner?> GetByIdWithPaymentMethodsAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// List all partners owned by a specific user.
    /// </summary>
    Task<IEnumerable<Partner>> GetByOwnerUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Searches for partners by name within the user's scope.
    /// </summary>
    Task<(IEnumerable<Partner> Items, int TotalCount)> SearchAsync(Guid userId, string? search, int skip, int take, CancellationToken ct = default);

    /// <summary>
    /// Creates a new partner.
    /// </summary>
    Task<Partner> CreateAsync(Partner partner, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing partner's metadata.
    /// </summary>
    Task UpdateAsync(Partner partner, CancellationToken ct = default);

    /// <summary>
    /// Soft-deletes a partner.
    /// </summary>
    Task SoftDeleteAsync(Guid id, Guid deletedByUserId, CancellationToken ct = default);

    /// <summary>
    /// Gets a paginated list of payment methods owned by the partner.
    /// </summary>
    Task<(IEnumerable<PaymentMethod> Items, int TotalCount)> GetPaymentMethodsPagedAsync(Guid partnerId, int skip, int take, CancellationToken ct = default);

    /// <summary>
    /// Gets a paginated list of projects where the partner is active.
    /// </summary>
    Task<(IEnumerable<Project> Items, int TotalCount)> GetProjectsPagedAsync(Guid partnerId, int skip, int take, CancellationToken ct = default);
}
