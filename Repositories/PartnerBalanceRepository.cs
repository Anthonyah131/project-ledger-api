using Microsoft.EntityFrameworkCore;
using ProjectLedger.API.Data;
using ProjectLedger.API.Models;

namespace ProjectLedger.API.Repositories;

/// <summary>
/// Read-only repository for calculating balances and history per partner.
///
/// Balance logic (all in project base currency):
///   Expense component A = SUM(others' splits in expenses paid by A) - SUM(A's split in expenses paid by others)
///   Income component A = SUM(A's split in income received by others) - SUM(others' splits in income received by A)
///   Settlement component = received - paid
///   Net balance = expenses + incomes + settlements
/// </summary>
public class PartnerBalanceRepository : IPartnerBalanceRepository
{
    private readonly AppDbContext _context;

    public PartnerBalanceRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PartnerBalanceSummary> GetBalancesAsync(Guid projectId, CancellationToken ct = default)
    {
        var partners = await _context.ProjectPartners
            .Include(pp => pp.Partner)
            .Where(pp => pp.PtpProjectId == projectId && !pp.PtpIsDeleted)
            .OrderBy(pp => pp.Partner.PtrName)
            .ToListAsync(ct);

        if (partners.Count == 0)
            return new PartnerBalanceSummary([], [], []);

        var partnerIds = partners.Select(pp => pp.PtpPartnerId).ToList();

        // Load expense splits for expenses paid by any project partner (via their PM)
        var expenseSplits = await _context.ExpenseSplits
            .Include(s => s.CurrencyExchanges)
            .Include(s => s.Expense).ThenInclude(e => e.PaymentMethod)
            .Where(s =>
                s.Expense.ExpProjectId == projectId
                && !s.Expense.ExpIsDeleted
                && s.Expense.ExpIsActive
                && s.Expense.PaymentMethod!.PmtOwnerPartnerId.HasValue
                && partnerIds.Contains(s.Expense.PaymentMethod.PmtOwnerPartnerId!.Value))
            .ToListAsync(ct);

        // Load expenses paid by project partners (for paid_physically)
        var expensesPaidByPartners = await _context.Expenses
            .Where(e =>
                e.ExpProjectId == projectId
                && !e.ExpIsDeleted
                && e.ExpIsActive
                && e.PaymentMethod!.PmtOwnerPartnerId.HasValue
                && partnerIds.Contains(e.PaymentMethod.PmtOwnerPartnerId!.Value))
            .Select(e => new { e.PaymentMethod!.PmtOwnerPartnerId, e.ExpConvertedAmount })
            .ToListAsync(ct);

        // Load income splits for incomes received by any project partner (via their PM)
        var incomeSplits = await _context.IncomeSplits
            .Include(s => s.CurrencyExchanges)
            .Include(s => s.Income).ThenInclude(i => i.PaymentMethod)
            .Where(s =>
                s.Income.IncProjectId == projectId
                && !s.Income.IncIsDeleted
                && s.Income.IncIsActive
                && s.Income.PaymentMethod!.PmtOwnerPartnerId.HasValue
                && partnerIds.Contains(s.Income.PaymentMethod.PmtOwnerPartnerId!.Value))
            .ToListAsync(ct);

        // Load settlements (with currency exchanges)
        var settlements = await _context.PartnerSettlements
            .Include(ps => ps.CurrencyExchanges)
            .Where(ps => ps.PstProjectId == projectId && !ps.PstIsDeleted)
            .ToListAsync(ct);

        var results = new List<PartnerBalanceData>(partners.Count);

        foreach (var pp in partners)
        {
            var partnerId = pp.PtpPartnerId;

            // paid_physically: sum of full expense amounts where this partner's PM was used
            var paidPhysically = expensesPaidByPartners
                .Where(e => e.PmtOwnerPartnerId == partnerId)
                .Sum(e => e.ExpConvertedAmount);

            // Expenses paid by this partner → what others owe him
            var othersOweHimExpenses = expenseSplits
                .Where(s => s.Expense.PaymentMethod!.PmtOwnerPartnerId == partnerId
                         && s.ExsPartnerId != partnerId)
                .Sum(s => s.ExsResolvedAmount);

            // This partner's splits in expenses others paid → what he owes others
            var heOwesOthersExpenses = expenseSplits
                .Where(s => s.ExsPartnerId == partnerId
                         && s.Expense.PaymentMethod!.PmtOwnerPartnerId != partnerId)
                .Sum(s => s.ExsResolvedAmount);

            // This partner's splits in incomes received by others → others owe him (they received on his behalf)
            var othersOweHimIncomes = incomeSplits
                .Where(s => s.InsPartnerId == partnerId
                         && s.Income.PaymentMethod!.PmtOwnerPartnerId != partnerId)
                .Sum(s => s.InsResolvedAmount);

            // Other partners' splits in incomes received by this partner → he owes others (he received on their behalf)
            var heOwesOthersIncomes = incomeSplits
                .Where(s => s.Income.PaymentMethod!.PmtOwnerPartnerId == partnerId
                         && s.InsPartnerId != partnerId)
                .Sum(s => s.InsResolvedAmount);

            var settlementsReceived = settlements
                .Where(ps => ps.PstToPartnerId == partnerId)
                .Sum(ps => ps.PstConvertedAmount);

            var settlementsPaid = settlements
                .Where(ps => ps.PstFromPartnerId == partnerId)
                .Sum(ps => ps.PstConvertedAmount);

            // Per-currency breakdown (settlements excluded — they only affect base currency)
            var othersOweHimExpCx = SumByCurrency(expenseSplits
                .Where(s => s.Expense.PaymentMethod!.PmtOwnerPartnerId == partnerId && s.ExsPartnerId != partnerId)
                .SelectMany(s => s.CurrencyExchanges));

            var heOwesOthersExpCx = SumByCurrency(expenseSplits
                .Where(s => s.ExsPartnerId == partnerId && s.Expense.PaymentMethod!.PmtOwnerPartnerId != partnerId)
                .SelectMany(s => s.CurrencyExchanges));

            var othersOweHimIncCx = SumByCurrency(incomeSplits
                .Where(s => s.InsPartnerId == partnerId && s.Income.PaymentMethod!.PmtOwnerPartnerId != partnerId)
                .SelectMany(s => s.CurrencyExchanges));

            var heOwesOthersIncCx = SumByCurrency(incomeSplits
                .Where(s => s.Income.PaymentMethod!.PmtOwnerPartnerId == partnerId && s.InsPartnerId != partnerId)
                .SelectMany(s => s.CurrencyExchanges));

            var othersOweHimCx = MergeCurrency(othersOweHimExpCx, othersOweHimIncCx);
            var heOwesOthersCx = MergeCurrency(heOwesOthersExpCx, heOwesOthersIncCx);

            var settlementsPaidCx = SumByCurrency(settlements
                .Where(ps => ps.PstFromPartnerId == partnerId)
                .SelectMany(ps => ps.CurrencyExchanges));

            var settlementsReceivedCx = SumByCurrency(settlements
                .Where(ps => ps.PstToPartnerId == partnerId)
                .SelectMany(ps => ps.CurrencyExchanges));

            var allCurrencies = othersOweHimCx.Keys
                .Union(heOwesOthersCx.Keys)
                .Union(settlementsPaidCx.Keys)
                .Union(settlementsReceivedCx.Keys);

            var currencyTotals = allCurrencies
                .Select(code => new CurrencyTotalData(
                    CurrencyCode: code,
                    OthersOweHim: othersOweHimCx.TryGetValue(code, out var o) ? o : 0m,
                    HeOwesOthers: heOwesOthersCx.TryGetValue(code, out var h) ? h : 0m,
                    SettlementsPaid: settlementsPaidCx.TryGetValue(code, out var sp) ? sp : 0m,
                    SettlementsReceived: settlementsReceivedCx.TryGetValue(code, out var sr) ? sr : 0m))
                .ToList();

            results.Add(new PartnerBalanceData(
                PartnerId: partnerId,
                PartnerName: pp.Partner.PtrName,
                PaidPhysically: paidPhysically,
                OthersOweHimExpenses: othersOweHimExpenses,
                HeOwesOthersExpenses: heOwesOthersExpenses,
                OthersOweHimIncomes: othersOweHimIncomes,
                HeOwesOthersIncomes: heOwesOthersIncomes,
                SettlementsReceived: settlementsReceived,
                SettlementsPaid: settlementsPaid,
                CurrencyTotals: currencyTotals
            ));
        }

        // ── Pairwise balances ─────────────────────────────────
        var pairwise = new List<PairwiseBalanceData>();

        for (int i = 0; i < partnerIds.Count; i++)
        {
            for (int j = i + 1; j < partnerIds.Count; j++)
            {
                var idA = partnerIds[i];
                var idB = partnerIds[j];
                var nameA = partners.First(p => p.PtpPartnerId == idA).Partner.PtrName;
                var nameB = partners.First(p => p.PtpPartnerId == idB).Partner.PtrName;

                // A owes B from expenses: A has a split in an expense paid by B's PM
                var aOwesBExpenses = expenseSplits
                    .Where(s => s.ExsPartnerId == idA
                             && s.Expense.PaymentMethod!.PmtOwnerPartnerId == idB)
                    .Sum(s => s.ExsResolvedAmount);

                // B owes A from expenses: B has a split in an expense paid by A's PM
                var bOwesAExpenses = expenseSplits
                    .Where(s => s.ExsPartnerId == idB
                             && s.Expense.PaymentMethod!.PmtOwnerPartnerId == idA)
                    .Sum(s => s.ExsResolvedAmount);

                // A owes B from incomes: B has a split in income received by A's PM (A received B's share)
                var aOwesBIncomes = incomeSplits
                    .Where(s => s.InsPartnerId == idB
                             && s.Income.PaymentMethod!.PmtOwnerPartnerId == idA)
                    .Sum(s => s.InsResolvedAmount);

                // B owes A from incomes: A has a split in income received by B's PM (B received A's share)
                var bOwesAIncomes = incomeSplits
                    .Where(s => s.InsPartnerId == idA
                             && s.Income.PaymentMethod!.PmtOwnerPartnerId == idB)
                    .Sum(s => s.InsResolvedAmount);

                var aOwesB = Math.Round(aOwesBExpenses + aOwesBIncomes, 2);
                var bOwesA = Math.Round(bOwesAExpenses + bOwesAIncomes, 2);

                var settlementsAToB = Math.Round(settlements
                    .Where(ps => ps.PstFromPartnerId == idA && ps.PstToPartnerId == idB)
                    .Sum(ps => ps.PstConvertedAmount), 2);

                var settlementsBToA = Math.Round(settlements
                    .Where(ps => ps.PstFromPartnerId == idB && ps.PstToPartnerId == idA)
                    .Sum(ps => ps.PstConvertedAmount), 2);

                var netBalance = Math.Round((aOwesB - settlementsAToB) - (bOwesA - settlementsBToA), 2);

                // Per-currency breakdown for the pair
                var aOwesBCx = MergeCurrency(
                    SumByCurrency(expenseSplits
                        .Where(s => s.ExsPartnerId == idA && s.Expense.PaymentMethod!.PmtOwnerPartnerId == idB)
                        .SelectMany(s => s.CurrencyExchanges)),
                    SumByCurrency(incomeSplits
                        .Where(s => s.InsPartnerId == idB && s.Income.PaymentMethod!.PmtOwnerPartnerId == idA)
                        .SelectMany(s => s.CurrencyExchanges)));

                var bOwesACx = MergeCurrency(
                    SumByCurrency(expenseSplits
                        .Where(s => s.ExsPartnerId == idB && s.Expense.PaymentMethod!.PmtOwnerPartnerId == idA)
                        .SelectMany(s => s.CurrencyExchanges)),
                    SumByCurrency(incomeSplits
                        .Where(s => s.InsPartnerId == idA && s.Income.PaymentMethod!.PmtOwnerPartnerId == idB)
                        .SelectMany(s => s.CurrencyExchanges)));

                var settlementsAtoBCx = SumByCurrency(settlements
                    .Where(ps => ps.PstFromPartnerId == idA && ps.PstToPartnerId == idB)
                    .SelectMany(ps => ps.CurrencyExchanges));

                var settlementsBtoACx = SumByCurrency(settlements
                    .Where(ps => ps.PstFromPartnerId == idB && ps.PstToPartnerId == idA)
                    .SelectMany(ps => ps.CurrencyExchanges));

                var pairCurrencies = aOwesBCx.Keys
                    .Union(bOwesACx.Keys)
                    .Union(settlementsAtoBCx.Keys)
                    .Union(settlementsBtoACx.Keys);

                var pairCurrencyTotals = pairCurrencies
                    .Select(code => new PairwiseCurrencyData(
                        CurrencyCode: code,
                        AOwesB: aOwesBCx.TryGetValue(code, out var a) ? a : 0m,
                        BOwesA: bOwesACx.TryGetValue(code, out var b) ? b : 0m,
                        SettlementsAToB: settlementsAtoBCx.TryGetValue(code, out var sab) ? sab : 0m,
                        SettlementsBToA: settlementsBtoACx.TryGetValue(code, out var sba) ? sba : 0m))
                    .ToList();

                pairwise.Add(new PairwiseBalanceData(
                    PartnerAId: idA,
                    PartnerAName: nameA,
                    PartnerBId: idB,
                    PartnerBName: nameB,
                    AOwesB: aOwesB,
                    BOwesA: bOwesA,
                    SettlementsAToB: settlementsAToB,
                    SettlementsBToA: settlementsBToA,
                    NetBalance: netBalance,
                    CurrencyTotals: pairCurrencyTotals
                ));
            }
        }

        // ── Warnings: splits with no currency exchanges when others have them ─
        var anyExpenseCx  = expenseSplits.Any(s => s.CurrencyExchanges.Count > 0);
        var anyIncomeCx   = incomeSplits.Any(s => s.CurrencyExchanges.Count > 0);

        var warnings = new List<MissingExchangeWarning>();

        if (anyExpenseCx)
        {
            var missing = expenseSplits
                .Where(s => s.CurrencyExchanges.Count == 0)
                .GroupBy(s => s.ExsExpenseId)
                .Select(g =>
                {
                    var first = g.First();
                    return new MissingExchangeWarning(
                        TransactionType: "expense",
                        TransactionId: first.ExsExpenseId,
                        Title: first.Expense.ExpTitle,
                        Date: first.Expense.ExpExpenseDate,
                        ConvertedAmount: Math.Round(first.Expense.ExpConvertedAmount, 2));
                });
            warnings.AddRange(missing);
        }

        if (anyIncomeCx)
        {
            var missing = incomeSplits
                .Where(s => s.CurrencyExchanges.Count == 0)
                .GroupBy(s => s.InsIncomeId)
                .Select(g =>
                {
                    var first = g.First();
                    return new MissingExchangeWarning(
                        TransactionType: "income",
                        TransactionId: first.InsIncomeId,
                        Title: first.Income.IncTitle,
                        Date: first.Income.IncIncomeDate,
                        ConvertedAmount: Math.Round(first.Income.IncConvertedAmount, 2));
                });
            warnings.AddRange(missing);
        }

        return new PartnerBalanceSummary(results, pairwise, warnings);
    }

    public async Task<PartnerHistoryData> GetPartnerHistoryAsync(
        Guid projectId, Guid partnerId,
        int transactionsSkip, int transactionsTake,
        CancellationToken ct = default)
    {
        var projectPartner = await _context.ProjectPartners
            .Include(pp => pp.Partner)
            .FirstOrDefaultAsync(pp =>
                pp.PtpProjectId == projectId
                && pp.PtpPartnerId == partnerId
                && !pp.PtpIsDeleted, ct)
            ?? throw new KeyNotFoundException("PartnerNotAssignedToProject");

        // Build all transactions in memory (merge expense + income splits), then paginate.
        // This is straightforward since each partner typically has a manageable number of splits.
        var expenseSplits = await _context.ExpenseSplits
            .Include(s => s.CurrencyExchanges)
            .Include(s => s.Expense).ThenInclude(e => e.PaymentMethod!).ThenInclude(pm => pm.OwnerPartner)
            .Where(s =>
                s.ExsPartnerId == partnerId
                && s.Expense.ExpProjectId == projectId
                && !s.Expense.ExpIsDeleted
                && s.Expense.ExpIsActive)
            .ToListAsync(ct);

        var incomeSplits = await _context.IncomeSplits
            .Include(s => s.CurrencyExchanges)
            .Include(s => s.Income).ThenInclude(i => i.PaymentMethod!).ThenInclude(pm => pm.OwnerPartner)
            .Where(s =>
                s.InsPartnerId == partnerId
                && s.Income.IncProjectId == projectId
                && !s.Income.IncIsDeleted
                && s.Income.IncIsActive)
            .ToListAsync(ct);

        var allTransactions = new List<PartnerTransactionData>(expenseSplits.Count + incomeSplits.Count);

        foreach (var s in expenseSplits)
        {
            allTransactions.Add(new PartnerTransactionData(
                Type: "expense",
                TransactionId: s.ExsExpenseId,
                Title: s.Expense.ExpTitle,
                Date: s.Expense.ExpExpenseDate,
                SplitAmountConverted: Math.Round(s.ExsResolvedAmount, 2),
                SplitType: s.ExsSplitType,
                SplitValue: s.ExsSplitValue,
                PayingPartnerName: s.Expense.PaymentMethod?.OwnerPartner?.PtrName,
                CurrencyExchanges: s.CurrencyExchanges
                    .Select(ce => new SplitCurrencyExchangeData(ce.SceCurrencyCode, ce.SceExchangeRate, ce.SceConvertedAmount))
                    .ToList()
            ));
        }

        foreach (var s in incomeSplits)
        {
            allTransactions.Add(new PartnerTransactionData(
                Type: "income",
                TransactionId: s.InsIncomeId,
                Title: s.Income.IncTitle,
                Date: s.Income.IncIncomeDate,
                SplitAmountConverted: Math.Round(s.InsResolvedAmount, 2),
                SplitType: s.InsSplitType,
                SplitValue: s.InsSplitValue,
                PayingPartnerName: s.Income.PaymentMethod?.OwnerPartner?.PtrName,
                CurrencyExchanges: s.CurrencyExchanges
                    .Select(ce => new SplitCurrencyExchangeData(ce.SceCurrencyCode, ce.SceExchangeRate, ce.SceConvertedAmount))
                    .ToList()
            ));
        }

        allTransactions.Sort((a, b) => b.Date.CompareTo(a.Date));

        var totalTransactions = allTransactions.Count;
        var pagedTransactions = allTransactions.Skip(transactionsSkip).Take(transactionsTake).ToList();

        // Settlements
        var rawSettlements = await _context.PartnerSettlements
            .Include(ps => ps.FromPartner)
            .Include(ps => ps.ToPartner)
            .Where(ps =>
                ps.PstProjectId == projectId
                && !ps.PstIsDeleted
                && (ps.PstFromPartnerId == partnerId || ps.PstToPartnerId == partnerId))
            .OrderByDescending(ps => ps.PstSettlementDate)
            .ToListAsync(ct);

        var settlements = rawSettlements.Select(ps => new PartnerSettlementData(
            Type: ps.PstFromPartnerId == partnerId ? "settlement_paid" : "settlement_received",
            Id: ps.PstId,
            Date: ps.PstSettlementDate,
            Amount: ps.PstAmount,
            Currency: ps.PstCurrency,
            ToPartnerName: ps.PstFromPartnerId == partnerId ? ps.ToPartner.PtrName : null,
            FromPartnerName: ps.PstToPartnerId == partnerId ? ps.FromPartner.PtrName : null
        )).ToList();

        return new PartnerHistoryData(
            PartnerId: partnerId,
            PartnerName: projectPartner.Partner.PtrName,
            Transactions: pagedTransactions,
            TransactionsTotalCount: totalTransactions,
            Settlements: settlements
        );
    }

    // ── Helpers ───────────────────────────────────────────────

    private static Dictionary<string, decimal> SumByCurrency(IEnumerable<SplitCurrencyExchange> exchanges) =>
        exchanges
            .GroupBy(ce => ce.SceCurrencyCode)
            .ToDictionary(g => g.Key, g => Math.Round(g.Sum(ce => ce.SceConvertedAmount), 2));

    private static Dictionary<string, decimal> MergeCurrency(
        Dictionary<string, decimal> a, Dictionary<string, decimal> b)
    {
        var result = new Dictionary<string, decimal>(a);
        foreach (var (key, val) in b)
            result[key] = result.TryGetValue(key, out var existing) ? existing + val : val;
        return result;
    }
}
