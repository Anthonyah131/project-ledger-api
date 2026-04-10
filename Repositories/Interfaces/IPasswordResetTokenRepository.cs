using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public interface IPasswordResetTokenRepository : IRepository<PasswordResetToken>
{
    /// <summary>Finds an active config (unused, not expired) by code hash.</summary>
    Task<PasswordResetToken?> GetActiveByCodeHashAsync(string codeHash, CancellationToken ct = default);

    /// <summary>Invalidates (marks as used) all active tokens for the user.</summary>
    Task InvalidateAllByUserIdAsync(Guid userId, CancellationToken ct = default);
}
