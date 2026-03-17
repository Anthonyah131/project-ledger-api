using ProjectLedger.API.Models;

namespace ProjectLedger.API.Services;

public interface IPartnerService
{
    Task<Partner?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Partner?> GetByIdWithPaymentMethodsAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<Partner>> GetByOwnerUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<(IEnumerable<Partner> Items, int TotalCount)> SearchAsync(Guid userId, string? search, int skip, int take, CancellationToken ct = default);
    Task<Partner> CreateAsync(Partner partner, CancellationToken ct = default);
    Task UpdateAsync(Partner partner, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid id, Guid deletedByUserId, CancellationToken ct = default);
    Task<IEnumerable<PaymentMethod>> GetPaymentMethodsAsync(Guid partnerId, CancellationToken ct = default);
}
