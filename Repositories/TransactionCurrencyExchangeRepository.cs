using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

/// <summary>
/// Repository implementation for TransactionCurrencyExchange operations.
/// </summary>
public class TransactionCurrencyExchangeRepository : Repository<TransactionCurrencyExchange>, ITransactionCurrencyExchangeRepository
{
    public TransactionCurrencyExchangeRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<TransactionCurrencyExchange>> GetByEntityAsync(
        string entityType, Guid entityId, CancellationToken ct = default)
        => entityType == "expense"
            ? await DbSet.Where(e => e.TceExpenseId == entityId).ToListAsync(ct)
            : await DbSet.Where(e => e.TceIncomeId == entityId).ToListAsync(ct);

    public async Task<IEnumerable<TransactionCurrencyExchange>> GetByEntitiesAsync(
        string entityType, IEnumerable<Guid> entityIds, CancellationToken ct = default)
    {
        var ids = entityIds.ToList();
        if (ids.Count == 0) return [];

        return entityType == "expense"
            ? await DbSet.Where(e => e.TceExpenseId.HasValue && ids.Contains(e.TceExpenseId.Value)).ToListAsync(ct)
            : await DbSet.Where(e => e.TceIncomeId.HasValue && ids.Contains(e.TceIncomeId.Value)).ToListAsync(ct);
    }

    public async Task DeleteByEntityAsync(string entityType, Guid entityId, CancellationToken ct = default)
    {
        var exchanges = entityType == "expense"
            ? await DbSet.Where(e => e.TceExpenseId == entityId).ToListAsync(ct)
            : await DbSet.Where(e => e.TceIncomeId == entityId).ToListAsync(ct);

        DbSet.RemoveRange(exchanges);
    }
}
