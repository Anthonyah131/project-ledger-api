using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public class PartnerRepository : Repository<Partner>, IPartnerRepository
{
    public PartnerRepository(AppDbContext context) : base(context) { }

    public override async Task<Partner?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await DbSet
            .FirstOrDefaultAsync(p => p.PtrId == id && !p.PtrIsDeleted, ct);

    public async Task<Partner?> GetByIdWithPaymentMethodsAsync(Guid id, CancellationToken ct = default)
        => await DbSet
            .Include(p => p.PaymentMethods)
                .ThenInclude(pm => pm.ProjectPaymentMethods)
                    .ThenInclude(ppm => ppm.Project)
                        .ThenInclude(proj => proj.Workspace)
            .FirstOrDefaultAsync(p => p.PtrId == id && !p.PtrIsDeleted, ct);

    public async Task<IEnumerable<Partner>> GetByOwnerUserIdAsync(Guid userId, CancellationToken ct = default)
        => await DbSet
            .Where(p => p.PtrOwnerUserId == userId && !p.PtrIsDeleted)
            .OrderBy(p => p.PtrName)
            .ToListAsync(ct);

    public async Task<IEnumerable<Partner>> SearchByNameAsync(
        Guid userId, string? search, int skip, int take, CancellationToken ct = default)
    {
        var query = DbSet
            .Where(p => p.PtrOwnerUserId == userId && !p.PtrIsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.PtrName.ToLower().Contains(search.ToLower()));

        return await query
            .OrderBy(p => p.PtrName)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<int> CountByOwnerUserIdAsync(Guid userId, CancellationToken ct = default)
        => await DbSet
            .CountAsync(p => p.PtrOwnerUserId == userId && !p.PtrIsDeleted, ct);

    public async Task<bool> HasActivePaymentMethodsInProjectsAsync(Guid partnerId, CancellationToken ct = default)
        => await Context.Set<ProjectPaymentMethod>()
            .AnyAsync(ppm =>
                ppm.PaymentMethod.PmtOwnerPartnerId == partnerId
                && !ppm.PaymentMethod.PmtIsDeleted, ct);
}
