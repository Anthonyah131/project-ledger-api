using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public interface IPartnerRepository : IRepository<Partner>
{
    Task<IEnumerable<Partner>> GetByOwnerUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<Partner?> GetByIdWithPaymentMethodsAsync(Guid id, CancellationToken ct = default);
    Task<(IEnumerable<Partner> Items, int TotalCount)> SearchByNameAsync(Guid userId, string? search, int skip, int take, CancellationToken ct = default);
    Task<(IEnumerable<PaymentMethod> Items, int TotalCount)> GetPaymentMethodsByPartnerIdPagedAsync(Guid partnerId, int skip, int take, CancellationToken ct = default);
    Task<(IEnumerable<Project> Items, int TotalCount)> GetProjectsByPartnerIdPagedAsync(Guid partnerId, int skip, int take, CancellationToken ct = default);
    Task<bool> HasActivePaymentMethodsInProjectsAsync(Guid partnerId, CancellationToken ct = default);
    Task<IEnumerable<PaymentMethod>> GetPaymentMethodsByPartnerIdAsync(Guid partnerId, CancellationToken ct = default);
    Task<IReadOnlyList<Project>> GetProjectsWithActivityAsync(Guid partnerId, CancellationToken ct = default);
}
