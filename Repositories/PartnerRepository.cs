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

    public async Task<(IEnumerable<Partner> Items, int TotalCount)> SearchByNameAsync(
        Guid userId, string? search, int skip, int take, CancellationToken ct = default)
    {
        var query = DbSet
            .Where(p => p.PtrOwnerUserId == userId && !p.PtrIsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.PtrName.ToLower().Contains(search.ToLower()));

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(p => p.PtrName)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<(IEnumerable<PaymentMethod> Items, int TotalCount)> GetPaymentMethodsByPartnerIdPagedAsync(
        Guid partnerId, int skip, int take, CancellationToken ct = default)
    {
        var query = Context.Set<PaymentMethod>()
            .Where(pm => pm.PmtOwnerPartnerId == partnerId && !pm.PmtIsDeleted);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(pm => pm.PmtName)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<(IEnumerable<Project> Items, int TotalCount)> GetProjectsByPartnerIdPagedAsync(
        Guid partnerId, int skip, int take, CancellationToken ct = default)
    {
        var query = Context.Set<Project>()
            .Include(p => p.Workspace)
            .Where(p => !p.PrjIsDeleted
                && p.ProjectPaymentMethods.Any(ppm =>
                    ppm.PaymentMethod.PmtOwnerPartnerId == partnerId
                    && !ppm.PaymentMethod.PmtIsDeleted));

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(p => p.PrjName)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<bool> HasActivePaymentMethodsAsync(Guid partnerId, CancellationToken ct = default)
        => await Context.Set<PaymentMethod>()
            .AnyAsync(pm => pm.PmtOwnerPartnerId == partnerId && !pm.PmtIsDeleted, ct);

    public async Task<bool> IsAssignedToAnyProjectAsync(Guid partnerId, CancellationToken ct = default)
        => await Context.Set<ProjectPartner>()
            .AnyAsync(pp => pp.PtpPartnerId == partnerId, ct);

    public async Task<bool> HasActivePaymentMethodsInProjectsAsync(Guid partnerId, CancellationToken ct = default)
        => await Context.Set<ProjectPaymentMethod>()
            .AnyAsync(ppm =>
                ppm.PaymentMethod.PmtOwnerPartnerId == partnerId
                && !ppm.PaymentMethod.PmtIsDeleted, ct);

    public async Task<IEnumerable<PaymentMethod>> GetPaymentMethodsByPartnerIdAsync(
        Guid partnerId, CancellationToken ct = default)
        => await Context.Set<PaymentMethod>()
            .Where(pm => pm.PmtOwnerPartnerId == partnerId && !pm.PmtIsDeleted)
            .OrderBy(pm => pm.PmtName)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Project>> GetProjectsWithActivityAsync(
        Guid partnerId, CancellationToken ct = default)
    {
        var projectIds = await Context.Set<ExpenseSplit>()
            .Where(es => es.ExsPartnerId == partnerId && !es.Expense.ExpIsDeleted && es.Expense.ExpIsActive)
            .Select(es => es.Expense.ExpProjectId)
            .Union(Context.Set<IncomeSplit>()
                .Where(ins => ins.InsPartnerId == partnerId && !ins.Income.IncIsDeleted && ins.Income.IncIsActive)
                .Select(ins => ins.Income.IncProjectId))
            .Union(Context.Set<PartnerSettlement>()
                .Where(ps => (ps.PstFromPartnerId == partnerId || ps.PstToPartnerId == partnerId) && !ps.PstIsDeleted)
                .Select(ps => ps.PstProjectId))
            .Distinct()
            .ToListAsync(ct);

        return await Context.Set<Project>()
            .Where(p => projectIds.Contains(p.PrjId) && !p.PrjIsDeleted)
            .OrderBy(p => p.PrjName)
            .ToListAsync(ct);
    }
}
