using ProjectLedger.API.DTOs.Mcp;

namespace ProjectLedger.API.Services;

/// <summary>
/// Partial implementation of McpService focusing on project partner balances and settlement history.
/// </summary>
public partial class McpService
{
    public async Task<McpPartnerBalancesResponse> GetPartnerBalancesAsync(
        Guid userId,
        McpPartnerBalancesQuery query,
        CancellationToken ct = default)
    {
        var scope    = await ResolveScopeAsync(userId, query.ProjectId, query.ProjectName, ct);
        var projects = new List<McpPartnerBalancesProjectResponse>();

        foreach (var project in scope.SelectedProjects)
        {
            var balance = await _partnerBalanceService.GetBalancesAsync(
                project.PrjId, project.PrjCurrencyCode, ct);

            if (balance.Partners.Count == 0)
                continue;

            projects.Add(new McpPartnerBalancesProjectResponse
            {
                ProjectId   = project.PrjId,
                ProjectName = project.PrjName,
                Currency    = project.PrjCurrencyCode,
                Partners    = balance.Partners.Select(p => new McpPartnerBalanceItemResponse
                {
                    PartnerId           = p.PartnerId,
                    PartnerName         = p.PartnerName,
                    OthersOweHim        = p.OthersOweHim,
                    HeOwesOthers        = p.HeOwesOthers,
                    SettlementsReceived = p.SettlementsReceived,
                    SettlementsPaid     = p.SettlementsPaid,
                    NetBalance          = p.NetBalance
                }).ToList()
            });
        }

        return new McpPartnerBalancesResponse
        {
            ProjectId  = query.ProjectId,
            SearchNote = scope.SearchNote,
            Projects   = projects
        };
    }

    public async Task<McpPagedResponse<McpPartnerSettlementItemResponse>> GetPartnerSettlementsAsync(
        Guid userId,
        McpPartnerSettlementsQuery query,
        CancellationToken ct = default)
    {
        EnsureValidDateRange(query.From, query.To);

        var scope = await ResolveScopeAsync(userId, query.ProjectId, query.ProjectName, ct);
        var items = new List<McpPartnerSettlementItemResponse>();

        foreach (var project in scope.SelectedProjects)
        {
            var settlements = await _settlementRepo.GetByProjectIdAsync(project.PrjId, ct);

            var filtered = settlements
                .Where(s => !query.From.HasValue || s.PstSettlementDate >= query.From.Value)
                .Where(s => !query.To.HasValue   || s.PstSettlementDate <= query.To.Value)
                .Where(s => string.IsNullOrWhiteSpace(query.PartnerName)
                    || ContainsText(s.FromPartner.PtrName, query.PartnerName)
                    || ContainsText(s.ToPartner.PtrName,   query.PartnerName))
                .Select(s => new McpPartnerSettlementItemResponse
                {
                    SettlementId    = s.PstId,
                    ProjectId       = project.PrjId,
                    ProjectName     = project.PrjName,
                    FromPartnerName = s.FromPartner.PtrName,
                    ToPartnerName   = s.ToPartner.PtrName,
                    Amount          = s.PstAmount,
                    Currency        = s.PstCurrency,
                    ConvertedAmount = s.PstConvertedAmount,
                    SettlementDate  = s.PstSettlementDate,
                    Description     = s.PstDescription
                });

            items.AddRange(filtered);
        }

        var ordered = items
            .OrderByDescending(s => s.SettlementDate)
            .ThenBy(s => s.ProjectName);

        return ToMcpPagedResponse(ordered, query, scope.SearchNote);
    }
}
