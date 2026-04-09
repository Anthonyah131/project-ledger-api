using ProjectLedger.API.DTOs.Search;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

public class SearchService : ISearchService
{
    private readonly ISearchRepository _repo;

    public SearchService(ISearchRepository repo)
    {
        _repo = repo;
    }

    public async Task<GlobalSearchResponse> SearchAsync(
        Guid userId, string query, string types, int pageSize, CancellationToken ct = default)
    {
        var typeList = types.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var searchAll = typeList.Length == 0 || (typeList.Length == 1 && typeList[0].Equals("all", StringComparison.OrdinalIgnoreCase));
        var includeExpenses = searchAll || typeList.Any(t => t.Equals("expenses", StringComparison.OrdinalIgnoreCase));
        var includeIncomes = searchAll || typeList.Any(t => t.Equals("incomes", StringComparison.OrdinalIgnoreCase));

        var expensesTask = includeExpenses
            ? _repo.SearchExpensesAsync(userId, query, pageSize, ct)
            : Task.FromResult<IReadOnlyList<ExpenseSearchResult>>([]);

        var incomesTask = includeIncomes
            ? _repo.SearchIncomesAsync(userId, query, pageSize, ct)
            : Task.FromResult<IReadOnlyList<IncomeSearchResult>>([]);

        await Task.WhenAll(expensesTask, incomesTask);

        return new GlobalSearchResponse
        {
            Expenses = await expensesTask,
            Incomes = await incomesTask
        };
    }
}
