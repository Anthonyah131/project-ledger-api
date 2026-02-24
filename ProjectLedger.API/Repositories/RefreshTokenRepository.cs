using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public class RefreshTokenRepository : Repository<RefreshToken>, IRefreshTokenRepository
{
    public RefreshTokenRepository(AppDbContext context) : base(context) { }

    public async Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default)
        => await DbSet.FirstOrDefaultAsync(
            t => t.RtkTokenHash == tokenHash && t.RtkRevokedAt == null, ct);

    public async Task<IEnumerable<RefreshToken>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await DbSet
            .Where(t => t.RtkUserId == userId && t.RtkRevokedAt == null)
            .ToListAsync(ct);

    public async Task RevokeAllByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var tokens = await DbSet
            .Where(t => t.RtkUserId == userId && t.RtkRevokedAt == null)
            .ToListAsync(ct);

        foreach (var token in tokens)
            token.RtkRevokedAt = DateTime.UtcNow;
    }
}
