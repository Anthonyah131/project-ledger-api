using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

public class ProjectPartnerRepository : Repository<ProjectPartner>, IProjectPartnerRepository
{
    public ProjectPartnerRepository(AppDbContext context) : base(context) { }

    public override async Task<ProjectPartner?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await DbSet
            .Include(p => p.Partner)
            .FirstOrDefaultAsync(p => p.PtpId == id && !p.PtpIsDeleted, ct);

    public async Task<IEnumerable<ProjectPartner>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => await DbSet
            .Include(p => p.Partner)
            .Where(p => p.PtpProjectId == projectId && !p.PtpIsDeleted)
            .OrderBy(p => p.Partner.PtrName)
            .ToListAsync(ct);

    public async Task<ProjectPartner?> GetActiveAsync(Guid projectId, Guid partnerId, CancellationToken ct = default)
        => await DbSet
            .FirstOrDefaultAsync(p =>
                p.PtpProjectId == projectId
                && p.PtpPartnerId == partnerId
                && !p.PtpIsDeleted, ct);

    public async Task<bool> HasPartnerPaymentMethodsLinkedToProjectAsync(Guid projectId, Guid partnerId, CancellationToken ct = default)
        => await Context.Set<ProjectPaymentMethod>()
            .AnyAsync(ppm =>
                ppm.PpmProjectId == projectId
                && ppm.PaymentMethod.PmtOwnerPartnerId == partnerId
                && !ppm.PaymentMethod.PmtIsDeleted, ct);

    public async Task<bool> HasPartnerSplitsInProjectAsync(Guid projectId, Guid partnerId, CancellationToken ct = default)
    {
        var hasExpenseSplits = await Context.Set<ExpenseSplit>()
            .AnyAsync(exs =>
                exs.ExsPartnerId == partnerId
                && exs.Expense.ExpProjectId == projectId
                && !exs.Expense.ExpIsDeleted, ct);

        if (hasExpenseSplits) return true;

        return await Context.Set<IncomeSplit>()
            .AnyAsync(ins =>
                ins.InsPartnerId == partnerId
                && ins.Income.IncProjectId == projectId
                && !ins.Income.IncIsDeleted, ct);
    }

    public async Task<bool> HasPartnerSettlementsInProjectAsync(Guid projectId, Guid partnerId, CancellationToken ct = default)
        => await Context.Set<PartnerSettlement>()
            .AnyAsync(pst =>
                pst.PstProjectId == projectId
                && (pst.PstFromPartnerId == partnerId || pst.PstToPartnerId == partnerId)
                && !pst.PstIsDeleted, ct);

    public async Task<IEnumerable<PaymentMethod>> GetLinkablePaymentMethodsAsync(Guid projectId, Guid userId, CancellationToken ct = default)
        => await Context.Set<PaymentMethod>()
            .Where(pm => !pm.PmtIsDeleted
                && pm.PmtOwnerUserId == userId
                // Must have a partner assigned to this project
                && pm.PmtOwnerPartnerId != null
                && DbSet.Any(ptp =>
                    ptp.PtpProjectId == projectId
                    && ptp.PtpPartnerId == pm.PmtOwnerPartnerId
                    && !ptp.PtpIsDeleted)
                // Must NOT already be linked to this project
                && !Context.Set<ProjectPaymentMethod>().Any(ppm =>
                    ppm.PpmProjectId == projectId
                    && ppm.PpmPaymentMethodId == pm.PmtId))
            .Include(pm => pm.OwnerPartner)
            .OrderBy(pm => pm.OwnerPartner!.PtrName)
            .ThenBy(pm => pm.PmtName)
            .ToListAsync(ct);

    public async Task<IEnumerable<PaymentMethod>> GetAvailablePaymentMethodsAsync(Guid projectId, Guid userId, CancellationToken ct = default)
        => await Context.Set<PaymentMethod>()
            .Where(pm => !pm.PmtIsDeleted
                && pm.PmtOwnerUserId == userId
                && (
                    // Methods without a partner — always available to the user
                    pm.PmtOwnerPartnerId == null
                    ||
                    // Methods with a partner that is assigned to this project
                    DbSet.Any(ptp =>
                        ptp.PtpProjectId == projectId
                        && ptp.PtpPartnerId == pm.PmtOwnerPartnerId
                        && !ptp.PtpIsDeleted)
                ))
            .Include(pm => pm.OwnerPartner)
            .OrderBy(pm => pm.OwnerPartner == null ? 0 : 1)
            .ThenBy(pm => pm.OwnerPartner!.PtrName)
            .ThenBy(pm => pm.PmtName)
            .ToListAsync(ct);
}
