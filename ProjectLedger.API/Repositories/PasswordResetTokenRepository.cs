using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public class PasswordResetTokenRepository : Repository<PasswordResetToken>, IPasswordResetTokenRepository
{
    public PasswordResetTokenRepository(AppDbContext context) : base(context) { }

    public async Task<PasswordResetToken?> GetActiveByCodeHashAsync(string codeHash, CancellationToken ct = default)
        => await DbSet.FirstOrDefaultAsync(
            t => t.PrtCodeHash == codeHash
              && t.PrtUsedAt == null
              && t.PrtExpiresAt > DateTime.UtcNow,
            ct);

    public async Task InvalidateAllByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var tokens = await DbSet
            .Where(t => t.PrtUserId == userId && t.PrtUsedAt == null)
            .ToListAsync(ct);

        foreach (var token in tokens)
            token.PrtUsedAt = DateTime.UtcNow;
    }
}
