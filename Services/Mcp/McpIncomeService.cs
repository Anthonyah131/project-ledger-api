using ProjectLedger.API.DTOs.Mcp;

namespace ProjectLedger.API.Services;

/// <summary>
/// Partial implementation of McpService focusing on income tracking and project-level income data.
/// </summary>
public partial class McpService
{
    public async Task<McpIncomeByPeriodResponse> GetIncomeByPeriodAsync(
        Guid userId,
        McpIncomeByPeriodQuery query,
        CancellationToken ct = default)
    {
        EnsureValidDateRange(query.From, query.To);
        var granularity = NormalizeGranularity(query.Granularity);

        var (from, to) = ResolveRangeOrDefaults(query.From, query.To, granularity);

        var scope = await ResolveScopeAsync(userId, query.ProjectId, query.ProjectName, ct);
        var incomes = await LoadIncomesAsync(scope.SelectedProjects, from, to, ct);

        var points = incomes
            .GroupBy(i => GetPeriodStart(i.IncIncomeDate, granularity))
            .OrderBy(g => g.Key)
            .Select(g => new McpIncomePeriodPointResponse
            {
                PeriodStart = g.Key,
                PeriodLabel = BuildPeriodLabel(g.Key, granularity),
                TotalIncome = g.Sum(x => x.IncConvertedAmount),
                IncomeCount = g.Count()
            })
            .ToList();

        var total = points.Sum(p => p.TotalIncome);
        var count = points.Sum(p => p.IncomeCount);

        var response = new McpIncomeByPeriodResponse
        {
            ProjectId = query.ProjectId,
            From = from,
            To = to,
            Granularity = granularity,
            TotalIncome = total,
            IncomeCount = count,
            SearchNote = scope.SearchNote,
            Points = points
        };

        if (query.ComparePreviousPeriod == true && from.HasValue && to.HasValue)
        {
            var length = to.Value.DayNumber - from.Value.DayNumber + 1;
            var prevFrom = from.Value.AddDays(-length);
            var prevTo = from.Value.AddDays(-1);
            var previous = await LoadIncomesAsync(scope.SelectedProjects, prevFrom, prevTo, ct);
            var previousTotal = previous.Sum(i => i.IncConvertedAmount);

            response.PreviousPeriodTotal = previousTotal;
            response.DeltaAmount = total - previousTotal;
            response.DeltaPercentage = previousTotal == 0
                ? null
                : Math.Round((total - previousTotal) / previousTotal * 100m, 2);
        }

        return response;
    }

    public async Task<McpIncomeByProjectResponse> GetIncomeByProjectAsync(
        Guid userId,
        McpIncomeByProjectQuery query,
        CancellationToken ct = default)
    {
        EnsureValidDateRange(query.From, query.To);
        var top = query.Top ?? 10;

        var scope = await ResolveScopeAsync(userId, query.ProjectId, query.ProjectName, ct);
        var incomes = await LoadIncomesAsync(scope.SelectedProjects, query.From, query.To, ct);

        var grouped = scope.SelectedProjects
            .Select(project =>
            {
                var projectIncomes = incomes.Where(i => i.IncProjectId == project.PrjId).ToList();
                return new McpIncomeByProjectItemResponse
                {
                    ProjectId = project.PrjId,
                    ProjectName = project.PrjName,
                    CurrencyCode = project.PrjCurrencyCode,
                    TotalIncome = projectIncomes.Sum(i => i.IncConvertedAmount),
                    IncomeCount = projectIncomes.Count
                };
            })
            .OrderByDescending(i => i.TotalIncome)
            .Take(top)
            .ToList();

        return new McpIncomeByProjectResponse
        {
            From = query.From,
            To = query.To,
            TotalIncome = grouped.Sum(g => g.TotalIncome),
            Items = grouped
        };
    }
}
