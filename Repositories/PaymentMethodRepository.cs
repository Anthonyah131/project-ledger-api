using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public class PaymentMethodRepository : Repository<PaymentMethod>, IPaymentMethodRepository
{
    public PaymentMethodRepository(AppDbContext context) : base(context) { }

    public override async Task<PaymentMethod?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await DbSet
            .Include(pm => pm.OwnerPartner)
            .FirstOrDefaultAsync(pm => pm.PmtId == id && !pm.PmtIsDeleted, ct);

    public async Task<IEnumerable<PaymentMethod>> GetByOwnerUserIdAsync(Guid userId, CancellationToken ct = default)
        => await DbSet
            .Include(pm => pm.OwnerPartner)
            .Where(pm => pm.PmtOwnerUserId == userId && !pm.PmtIsDeleted)
            .OrderBy(pm => pm.PmtName)
            .ToListAsync(ct);

    public async Task<(IEnumerable<PaymentMethod> Items, int TotalCount)> GetByOwnerUserIdPagedWithSearchAsync(
        Guid userId, string? search, int skip, int take, CancellationToken ct = default)
    {
        var query = DbSet
            .Where(pm => pm.PmtOwnerUserId == userId && !pm.PmtIsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(pm => pm.PmtName.ToLower().Contains(search.ToLower()));

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(pm => pm.PmtName).Skip(skip).Take(take).ToListAsync(ct);
        return (items, total);
    }

    public async Task<bool> IsLinkedToAnyProjectAsync(Guid pmtId, CancellationToken ct = default)
        => await Context.Set<ProjectPaymentMethod>()
            .AnyAsync(ppm => ppm.PpmPaymentMethodId == pmtId, ct);

    public async Task<(decimal TotalIncome, decimal TotalExpenses)> GetProjectBalanceAsync(
        Guid pmtId, Guid projectId, CancellationToken ct = default)
    {
        var totalIncome = await Context.Set<Income>()
            .Where(i => i.IncPaymentMethodId == pmtId
                     && i.IncProjectId == projectId
                     && !i.IncIsDeleted
                     && i.IncIsActive)
            .SumAsync(i => (decimal?)i.IncOriginalAmount, ct) ?? 0m;

        var totalExpenses = await Context.Set<Expense>()
            .Where(e => e.ExpPaymentMethodId == pmtId
                     && e.ExpProjectId == projectId
                     && !e.ExpIsDeleted
                     && e.ExpIsActive)
            .SumAsync(e => (decimal?)e.ExpOriginalAmount, ct) ?? 0m;

        return (totalIncome, totalExpenses);
    }
}
