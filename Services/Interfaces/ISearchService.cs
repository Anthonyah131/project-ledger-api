using ProjectLedger.API.DTOs.Search;

namespace ProjectLedger.API.Services;

public interface ISearchService
{
    Task<GlobalSearchResponse> SearchAsync(Guid userId, string query, string types, int pageSize, CancellationToken ct = default);
}
