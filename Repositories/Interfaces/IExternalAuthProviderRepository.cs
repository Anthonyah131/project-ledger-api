using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

/// <summary>
/// Repository interface for ExternalAuthProvider operations.
/// </summary>
public interface IExternalAuthProviderRepository : IRepository<ExternalAuthProvider>
{
    Task<ExternalAuthProvider?> GetByProviderAndProviderUserIdAsync(string provider, string providerUserId, CancellationToken ct = default);
    Task<IEnumerable<ExternalAuthProvider>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
}
