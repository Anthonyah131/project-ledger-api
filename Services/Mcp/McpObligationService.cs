using ProjectLedger.API.DTOs.Mcp;

namespace ProjectLedger.API.Services;

public partial class McpService
{
    public async Task<McpPagedResponse<McpObligationItemResponse>> GetUpcomingObligationsAsync(
        Guid userId,
        McpUpcomingObligationsQuery query,
        CancellationToken ct = default)
    {
        var scope = await ResolveScopeAsync(userId, query.ProjectId, query.ProjectName, ct);
        var obligations = await LoadObligationsWithPaymentsAsync(scope.SelectedProjects, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dueWithinDays = query.DueWithinDays ?? 30;
        var end = today.AddDays(dueWithinDays);

        var items = obligations
            .Where(o => o.OblDueDate.HasValue && o.OblDueDate.Value >= today && o.OblDueDate.Value <= end)
            .Select(o =>
            {
                var paid = ComputePaidAmount(o);
                var remaining = Math.Max(0m, o.OblTotalAmount - paid);
                var status = ComputeObligationStatus(o, paid, today);
                return new
                {
                    Obligation = o,
                    Item = new McpObligationItemResponse
                    {
                        ObligationId = o.OblId,
                        ProjectId = o.OblProjectId,
                        ProjectName = o.Project.PrjName,
                        Title = o.OblTitle,
                        Description = o.OblDescription,
                        DueDate = o.OblDueDate,
                        TotalAmount = o.OblTotalAmount,
                        PaidAmount = paid,
                        RemainingAmount = remaining,
                        Currency = o.OblCurrency,
                        Status = status,
                        DaysUntilDue = o.OblDueDate.HasValue ? o.OblDueDate.Value.DayNumber - today.DayNumber : null,
                        DaysOverdue = o.OblDueDate.HasValue && o.OblDueDate.Value < today
                            ? today.DayNumber - o.OblDueDate.Value.DayNumber
                            : null
                    }
                };
            })
            .Where(x => x.Item.RemainingAmount > 0)
            .Where(x => !query.MinRemainingAmount.HasValue || x.Item.RemainingAmount >= query.MinRemainingAmount.Value)
            .Where(x => string.IsNullOrWhiteSpace(query.Search)
                || ContainsText(x.Obligation.OblTitle, query.Search)
                || ContainsText(x.Obligation.OblDescription, query.Search))
            .Select(x => x.Item)
            .OrderBy(i => i.DueDate)
            .ThenByDescending(i => i.RemainingAmount)
            .ToList();

        return ToMcpPagedResponse(items, query, scope.SearchNote);
    }

    public async Task<McpPagedResponse<McpObligationItemResponse>> GetUnpaidObligationsAsync(
        Guid userId,
        McpUnpaidObligationsQuery query,
        CancellationToken ct = default)
    {
        var scope = await ResolveScopeAsync(userId, query.ProjectId, query.ProjectName, ct);
        var obligations = await LoadObligationsWithPaymentsAsync(scope.SelectedProjects, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var items = obligations
            .Select(o =>
            {
                var paid = ComputePaidAmount(o);
                var remaining = Math.Max(0m, o.OblTotalAmount - paid);
                var status = ComputeObligationStatus(o, paid, today);
                return new
                {
                    Obligation = o,
                    Item = new McpObligationItemResponse
                    {
                        ObligationId = o.OblId,
                        ProjectId = o.OblProjectId,
                        ProjectName = o.Project.PrjName,
                        Title = o.OblTitle,
                        Description = o.OblDescription,
                        DueDate = o.OblDueDate,
                        TotalAmount = o.OblTotalAmount,
                        PaidAmount = paid,
                        RemainingAmount = remaining,
                        Currency = o.OblCurrency,
                        Status = status,
                        DaysUntilDue = o.OblDueDate.HasValue && o.OblDueDate.Value >= today
                            ? o.OblDueDate.Value.DayNumber - today.DayNumber
                            : null,
                        DaysOverdue = o.OblDueDate.HasValue && o.OblDueDate.Value < today
                            ? today.DayNumber - o.OblDueDate.Value.DayNumber
                            : null
                    }
                };
            })
            .Where(x => x.Item.RemainingAmount > 0)
            .Where(x => string.IsNullOrWhiteSpace(query.Status) || x.Item.Status == Normalize(query.Status))
            .Where(x => string.IsNullOrWhiteSpace(query.Search)
                || ContainsText(x.Obligation.OblTitle, query.Search)
                || ContainsText(x.Obligation.OblDescription, query.Search))
            .Select(x => x.Item)
            .OrderByDescending(i => i.DaysOverdue ?? 0)
            .ThenBy(i => i.DueDate)
            .ToList();

        return ToMcpPagedResponse(items, query, scope.SearchNote);
    }
}
