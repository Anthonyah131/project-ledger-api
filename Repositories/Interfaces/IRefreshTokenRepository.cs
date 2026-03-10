using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public interface IRefreshTokenRepository : IRepository<RefreshToken>
{
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default);
    Task<IEnumerable<RefreshToken>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task RevokeAllByUserIdAsync(Guid userId, CancellationToken ct = default);
}
