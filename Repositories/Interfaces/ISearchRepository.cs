using ProjectLedger.API.DTOs.Search;

namespace ProjectLedger.API.Repositories;

public interface ISearchRepository
{
    Task<IReadOnlyList<ExpenseSearchResult>> SearchExpensesAsync(Guid userId, string query, int pageSize, CancellationToken ct = default);
    Task<IReadOnlyList<IncomeSearchResult>> SearchIncomesAsync(Guid userId, string query, int pageSize, CancellationToken ct = default);
}
