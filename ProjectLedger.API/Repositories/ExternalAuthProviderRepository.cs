using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public class ExternalAuthProviderRepository : Repository<ExternalAuthProvider>, IExternalAuthProviderRepository
{
    public ExternalAuthProviderRepository(AppDbContext context) : base(context) { }

    public async Task<ExternalAuthProvider?> GetByProviderAndProviderUserIdAsync(
        string provider, string providerUserId, CancellationToken ct = default)
        => await DbSet.FirstOrDefaultAsync(
            e => e.EapProvider == provider && e.EapProviderUserId == providerUserId, ct);

    public async Task<IEnumerable<ExternalAuthProvider>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await DbSet
            .Where(e => e.EapUserId == userId)
            .OrderBy(e => e.EapProvider)
            .ToListAsync(ct);
}
