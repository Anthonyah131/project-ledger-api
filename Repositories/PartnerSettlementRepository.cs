using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;
using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

/// <summary>
/// Repository implementation for PartnerSettlement operations.
/// </summary>
public class PartnerSettlementRepository : Repository<PartnerSettlement>, IPartnerSettlementRepository
{
    public PartnerSettlementRepository(AppDbContext context) : base(context) { }

    public override async Task<PartnerSettlement?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await DbSet
            .Include(s => s.FromPartner)
            .Include(s => s.ToPartner)
            .Include(s => s.CurrencyExchanges)
            .FirstOrDefaultAsync(s => s.PstId == id && !s.PstIsDeleted, ct);

    public async Task<(IReadOnlyList<PartnerSettlement> Items, int TotalCount)> GetPagedByProjectIdAsync(
        Guid projectId, int skip, int take, CancellationToken ct = default)
    {
        var query = DbSet
            .Include(s => s.FromPartner)
            .Include(s => s.ToPartner)
            .Include(s => s.CurrencyExchanges)
            .Where(s => s.PstProjectId == projectId && !s.PstIsDeleted)
            .OrderByDescending(s => s.PstSettlementDate)
            .ThenByDescending(s => s.PstCreatedAt);

        var total = await query.CountAsync(ct);
        var items = await query.Skip(skip).Take(take).ToListAsync(ct);

        return (items, total);
    }

    public async Task<IEnumerable<PartnerSettlement>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => await DbSet
            .Include(s => s.FromPartner)
            .Include(s => s.ToPartner)
            .Include(s => s.CurrencyExchanges)
            .Where(s => s.PstProjectId == projectId && !s.PstIsDeleted)
            .OrderByDescending(s => s.PstSettlementDate)
            .ToListAsync(ct);

    public async Task<PartnerSettlement?> GetActiveByIdAsync(Guid settlementId, Guid projectId, CancellationToken ct = default)
        => await DbSet
            .Include(s => s.FromPartner)
            .Include(s => s.ToPartner)
            .Include(s => s.CurrencyExchanges)
            .FirstOrDefaultAsync(s => s.PstId == settlementId && s.PstProjectId == projectId && !s.PstIsDeleted, ct);

    public async Task SaveCurrencyExchangesAsync(Guid settlementId, IReadOnlyList<CurrencyExchangeRequest> exchanges, CancellationToken ct = default)
    {
        foreach (var ce in exchanges)
        {
            Context.Set<SplitCurrencyExchange>().Add(new SplitCurrencyExchange
            {
                SceSettlementId = settlementId,
                SceCurrencyCode = ce.CurrencyCode,
                SceExchangeRate = ce.ExchangeRate,
                SceConvertedAmount = ce.ConvertedAmount
            });
        }
        await Context.SaveChangesAsync(ct);
    }

    public async Task ReplaceCurrencyExchangesAsync(Guid settlementId, IReadOnlyList<CurrencyExchangeRequest> exchanges, CancellationToken ct = default)
    {
        var existing = await Context.Set<SplitCurrencyExchange>()
            .Where(sce => sce.SceSettlementId == settlementId)
            .ToListAsync(ct);

        Context.Set<SplitCurrencyExchange>().RemoveRange(existing);

        foreach (var ce in exchanges)
        {
            Context.Set<SplitCurrencyExchange>().Add(new SplitCurrencyExchange
            {
                SceSettlementId = settlementId,
                SceCurrencyCode = ce.CurrencyCode,
                SceExchangeRate = ce.ExchangeRate,
                SceConvertedAmount = ce.ConvertedAmount
            });
        }
        await Context.SaveChangesAsync(ct);
    }
}
