using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.DTOs.Partner;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

public class PartnerBalanceService : IPartnerBalanceService
{
    private readonly IPartnerBalanceRepository _balanceRepo;

    public PartnerBalanceService(IPartnerBalanceRepository balanceRepo)
    {
        _balanceRepo = balanceRepo;
    }

    public async Task<PartnerBalanceResponse> GetBalancesAsync(Guid projectId, string projectCurrency, CancellationToken ct = default)
    {
        var summary = await _balanceRepo.GetBalancesAsync(projectId, ct);

        var items = summary.Individuals.Select(b =>
        {
            var othersOweHim = Math.Round(b.OthersOweHimExpenses + b.OthersOweHimIncomes, 2);
            var heOwesOthers = Math.Round(b.HeOwesOthersExpenses + b.HeOwesOthersIncomes, 2);

            // Balance = (othersOweHim - heOwesOthers) + (settlementsPaid - settlementsReceived)
            // settlementsPaid: A paid another partner → reduces A's debt → adds to A's balance
            // settlementsReceived: A received from another partner → reduces what is owed to A → subtracts from A's balance
            var netBalance = othersOweHim - heOwesOthers + b.SettlementsPaid - b.SettlementsReceived;

            var currencyTotals = b.CurrencyTotals
                .Select(ct => new PartnerCurrencyTotal(
                    CurrencyCode: ct.CurrencyCode,
                    OthersOweHim: ct.OthersOweHim,
                    HeOwesOthers: ct.HeOwesOthers,
                    SettlementsPaid: ct.SettlementsPaid,
                    SettlementsReceived: ct.SettlementsReceived,
                    NetBalance: Math.Round(ct.OthersOweHim - ct.HeOwesOthers + ct.SettlementsPaid - ct.SettlementsReceived, 2)))
                .ToList();

            return new PartnerBalanceItem(
                PartnerId: b.PartnerId,
                PartnerName: b.PartnerName,
                PaidPhysically: Math.Round(b.PaidPhysically, 2),
                OthersOweHim: othersOweHim,
                HeOwesOthers: heOwesOthers,
                SettlementsReceived: Math.Round(b.SettlementsReceived, 2),
                SettlementsPaid: Math.Round(b.SettlementsPaid, 2),
                NetBalance: Math.Round(netBalance, 2),
                CurrencyTotals: currencyTotals
            );
        }).ToList();

        var pairwiseItems = summary.Pairwise.Select(p => new PairwiseBalanceItem(
            PartnerAId: p.PartnerAId,
            PartnerAName: p.PartnerAName,
            PartnerBId: p.PartnerBId,
            PartnerBName: p.PartnerBName,
            AOwesB: p.AOwesB,
            BOwesA: p.BOwesA,
            SettlementsAToB: p.SettlementsAToB,
            SettlementsBToA: p.SettlementsBToA,
            NetBalance: p.NetBalance,
            CurrencyTotals: p.CurrencyTotals
                .Select(ct => new PairwiseCurrencyTotal(
                    CurrencyCode: ct.CurrencyCode,
                    AOwesB: ct.AOwesB,
                    BOwesA: ct.BOwesA,
                    SettlementsAToB: ct.SettlementsAToB,
                    SettlementsBToA: ct.SettlementsBToA,
                    NetBalance: Math.Round((ct.AOwesB - ct.SettlementsAToB) - (ct.BOwesA - ct.SettlementsBToA), 2)))
                .ToList()
        )).ToList();

        var warnings = summary.Warnings
            .Select(w => new MissingCurrencyExchangeWarning(
                TransactionType: w.TransactionType,
                TransactionId: w.TransactionId,
                Title: w.Title,
                Date: w.Date,
                ConvertedAmount: w.ConvertedAmount))
            .ToList();

        return new PartnerBalanceResponse(
            ProjectId: projectId,
            Currency: projectCurrency,
            Partners: items,
            PairwiseBalances: pairwiseItems,
            Warnings: warnings
        );
    }

    public async Task<SettlementSuggestionsResponse> GetSettlementSuggestionsAsync(
        Guid projectId, string projectCurrency, CancellationToken ct = default)
    {
        var summary = await _balanceRepo.GetBalancesAsync(projectId, ct);

        // Compute net balance per partner (same formula as GetBalancesAsync)
        var netBalances = summary.Individuals.Select(b =>
        {
            var othersOweHim = b.OthersOweHimExpenses + b.OthersOweHimIncomes;
            var heOwesOthers = b.HeOwesOthersExpenses + b.HeOwesOthersIncomes;
            var net = Math.Round(othersOweHim - heOwesOthers + b.SettlementsPaid - b.SettlementsReceived, 2);
            return (b.PartnerId, b.PartnerName, Net: net);
        }).ToList();

        // Greedy algorithm: pair largest creditor with largest debtor, repeat
        var creditorIds    = netBalances.Where(x => x.Net > 0.01m).OrderByDescending(x => x.Net).Select(x => x.PartnerId).ToArray();
        var creditorNames  = netBalances.Where(x => x.Net > 0.01m).OrderByDescending(x => x.Net).Select(x => x.PartnerName).ToArray();
        var creditorBals   = netBalances.Where(x => x.Net > 0.01m).OrderByDescending(x => x.Net).Select(x => x.Net).ToArray();

        var debtorIds      = netBalances.Where(x => x.Net < -0.01m).OrderByDescending(x => -x.Net).Select(x => x.PartnerId).ToArray();
        var debtorNames    = netBalances.Where(x => x.Net < -0.01m).OrderByDescending(x => -x.Net).Select(x => x.PartnerName).ToArray();
        var debtorBals     = netBalances.Where(x => x.Net < -0.01m).OrderByDescending(x => -x.Net).Select(x => -x.Net).ToArray();

        var suggestions = new List<SettlementSuggestionItem>();
        int ci = 0, di = 0;

        while (ci < creditorIds.Length && di < debtorIds.Length)
        {
            var amount = Math.Min(creditorBals[ci], debtorBals[di]);

            suggestions.Add(new SettlementSuggestionItem(
                FromPartnerId: debtorIds[di],
                FromPartnerName: debtorNames[di],
                ToPartnerId: creditorIds[ci],
                ToPartnerName: creditorNames[ci],
                Amount: Math.Round(amount, 2),
                Currency: projectCurrency
            ));

            creditorBals[ci] -= amount;
            debtorBals[di]   -= amount;

            if (creditorBals[ci] <= 0.01m) ci++;
            if (debtorBals[di]   <= 0.01m) di++;
        }

        return new SettlementSuggestionsResponse(
            ProjectId: projectId,
            Currency: projectCurrency,
            Suggestions: suggestions
        );
    }

    public async Task<PartnerHistoryResponse> GetPartnerHistoryAsync(
        Guid projectId, Guid partnerId,
        PagedRequest pagination,
        CancellationToken ct = default)
    {
        var history = await _balanceRepo.GetPartnerHistoryAsync(
            projectId, partnerId,
            pagination.Skip, pagination.PageSize,
            ct);

        var transactions = history.Transactions.Select(t => new PartnerTransactionItem(
            Type: t.Type,
            TransactionId: t.TransactionId,
            Title: t.Title,
            Date: t.Date,
            SplitAmount: t.SplitAmountConverted,
            SplitType: t.SplitType,
            SplitValue: t.SplitValue,
            PayingPartner: t.PayingPartnerName,
            CurrencyExchanges: t.CurrencyExchanges
                .Select(ce => new SplitCurrencyExchangeItem(ce.CurrencyCode, ce.ExchangeRate, ce.ConvertedAmount))
                .ToList()
        )).ToList();

        var pagedTransactions = PagedResponse<PartnerTransactionItem>.Create(
            transactions, history.TransactionsTotalCount, pagination);

        var settlements = history.Settlements.Select(s => new PartnerSettlementHistoryItem(
            Type: s.Type,
            Id: s.Id,
            Date: s.Date,
            Amount: s.Amount,
            Currency: s.Currency,
            ToPartner: s.ToPartnerName,
            FromPartner: s.FromPartnerName
        )).ToList();

        return new PartnerHistoryResponse(
            PartnerId: history.PartnerId,
            PartnerName: history.PartnerName,
            Transactions: pagedTransactions,
            Settlements: settlements
        );
    }
}
