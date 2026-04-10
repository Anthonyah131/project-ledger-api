using ProjectLedger.API.DTOs.Search;

namespace ProjectLedger.API.Services;

public interface ISearchService
{
    /// <summary>
    /// Performs a cross-entity search (Expenses, Incomes, Partners, Projects) for the given query.
    /// </summary>
    Task<GlobalSearchResponse> SearchAsync(Guid userId, string query, string types, int pageSize, CancellationToken ct = default);
}
