using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.DTOs.Partner;
using ProjectLedger.API.DTOs.Report;
using ProjectLedger.API.Repositories;

namespace ProjectLedger.API.Services;

public class PartnerReportService : IPartnerReportService
{
    private readonly IPartnerRepository _partnerRepo;
    private readonly IPartnerBalanceService _partnerBalanceService;
    private readonly IExpenseRepository _expenseRepo;
    private readonly IIncomeRepository _incomeRepo;
    private readonly IPartnerSettlementRepository _settlementRepo;

    public PartnerReportService(
        IPartnerRepository partnerRepo,
        IPartnerBalanceService partnerBalanceService,
        IExpenseRepository expenseRepo,
        IIncomeRepository incomeRepo,
        IPartnerSettlementRepository settlementRepo)
    {
        _partnerRepo = partnerRepo;
        _partnerBalanceService = partnerBalanceService;
        _expenseRepo = expenseRepo;
        _incomeRepo = incomeRepo;
        _settlementRepo = settlementRepo;
    }

    public async Task<PartnerGeneralReportResponse> GetGeneralReportAsync(
        Guid partnerId, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var partner = await _partnerRepo.GetByIdAsync(partnerId, ct)
            ?? throw new KeyNotFoundException("Partner not found.");

        // ── Projects with activity ─────────────────────────
        var projects = await _partnerRepo.GetProjectsWithActivityAsync(partnerId, ct);

        var projectSummaries = new List<PartnerProjectSummary>();

        foreach (var project in projects)
        {
            var summary = await BuildProjectSummaryAsync(
                partnerId, project.PrjId, project.PrjName, project.PrjCurrencyCode, from, to, ct);
            projectSummaries.Add(summary);
        }

        // ── Payment method summaries ───────────────────────
        var paymentMethods = (await _partnerRepo.GetPaymentMethodsByPartnerIdAsync(partnerId, ct)).ToList();
        var pmSummaries = new List<PartnerPaymentMethodSummary>();

        if (paymentMethods.Count > 0)
        {
            var pmIds = paymentMethods.Select(pm => pm.PmtId).ToList();

            var pmExpenses = (await _expenseRepo.GetByPaymentMethodIdsWithDetailsAsync(pmIds, from, to, ct)).ToList();
            var pmIncomes = (await _incomeRepo.GetByPaymentMethodIdsWithDetailsAsync(pmIds, from, to, ct)).ToList();

            foreach (var pm in paymentMethods)
            {
                var exps = pmExpenses.Where(e => e.ExpPaymentMethodId == pm.PmtId).ToList();
                var incs = pmIncomes.Where(i => i.IncPaymentMethodId == pm.PmtId).ToList();

                // Use account amount (PM native currency) when available,
                // otherwise fall back to original amount if currencies match
                var totalExp = exps.Sum(e => e.ExpAccountAmount
                    ?? (e.ExpOriginalCurrency == pm.PmtCurrency ? e.ExpOriginalAmount : e.ExpConvertedAmount));
                var totalInc = incs.Sum(i => i.IncAccountAmount
                    ?? (i.IncOriginalCurrency == pm.PmtCurrency ? i.IncOriginalAmount : i.IncConvertedAmount));

                pmSummaries.Add(new PartnerPaymentMethodSummary
                {
                    PaymentMethodId = pm.PmtId,
                    PaymentMethodName = pm.PmtName,
                    Currency = pm.PmtCurrency,
                    BankName = pm.PmtBankName,
                    TotalExpenses = Math.Round(totalExp, 2),
                    TotalIncomes = Math.Round(totalInc, 2),
                    NetFlow = Math.Round(totalInc - totalExp, 2),
                    TransactionCount = exps.Count + incs.Count
                });
            }
        }

        return new PartnerGeneralReportResponse
        {
            PartnerId = partner.PtrId,
            PartnerName = partner.PtrName,
            PartnerEmail = partner.PtrEmail,
            DateFrom = from,
            DateTo = to,
            GeneratedAt = DateTime.UtcNow,
            Projects = projectSummaries,
            PaymentMethods = pmSummaries
        };
    }

    private async Task<PartnerProjectSummary> BuildProjectSummaryAsync(
        Guid partnerId, Guid projectId, string projectName, string currencyCode,
        DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        // ── Balances (same logic as /partners/balance) ─────
        var balances = await _partnerBalanceService.GetBalancesAsync(projectId, currencyCode, ct);
        var partnerBalance = balances.Partners.FirstOrDefault(p => p.PartnerId == partnerId);

        // ── Transactions with splits for this partner ──────
        var expenses = await _expenseRepo.GetByProjectIdForPartnerAsync(projectId, partnerId, from, to, ct);
        var incomes = await _incomeRepo.GetByProjectIdForPartnerAsync(projectId, partnerId, from, to, ct);

        var transactions = new List<PartnerProjectTransaction>();

        foreach (var expense in expenses)
        {
            var split = expense.Splits.FirstOrDefault(s => s.ExsPartnerId == partnerId);
            if (split is null) continue;

            transactions.Add(new PartnerProjectTransaction
            {
                TransactionId = expense.ExpId,
                Type = "expense",
                Title = expense.ExpTitle,
                Date = expense.ExpExpenseDate,
                Category = expense.Category?.CatName,
                PaymentMethodName = expense.PaymentMethod?.PmtName,
                PayingPartnerName = expense.PaymentMethod?.OwnerPartner?.PtrName,
                SplitAmount = Math.Round(split.ExsResolvedAmount, 2),
                SplitType = split.ExsSplitType,
                SplitValue = split.ExsSplitValue,
                CurrencyExchanges = split.CurrencyExchanges.Select(ce => new SplitCurrencyExchangeItem(
                    ce.SceCurrencyCode, ce.SceExchangeRate, ce.SceConvertedAmount)).ToList()
            });
        }

        foreach (var income in incomes)
        {
            var split = income.Splits.FirstOrDefault(s => s.InsPartnerId == partnerId);
            if (split is null) continue;

            transactions.Add(new PartnerProjectTransaction
            {
                TransactionId = income.IncId,
                Type = "income",
                Title = income.IncTitle,
                Date = income.IncIncomeDate,
                Category = income.Category?.CatName,
                PaymentMethodName = income.PaymentMethod?.PmtName,
                PayingPartnerName = income.PaymentMethod?.OwnerPartner?.PtrName,
                SplitAmount = Math.Round(split.InsResolvedAmount, 2),
                SplitType = split.InsSplitType,
                SplitValue = split.InsSplitValue,
                CurrencyExchanges = split.CurrencyExchanges.Select(ce => new SplitCurrencyExchangeItem(
                    ce.SceCurrencyCode, ce.SceExchangeRate, ce.SceConvertedAmount)).ToList()
            });
        }

        transactions = [.. transactions.OrderBy(t => t.Date)];

        // ── Settlements ────────────────────────────────────
        var allSettlements = await _settlementRepo.GetByProjectIdAsync(projectId, ct);
        var settlements = allSettlements
            .Where(s => s.PstFromPartnerId == partnerId || s.PstToPartnerId == partnerId)
            .Where(s => from is null || s.PstSettlementDate >= from.Value)
            .Where(s => to is null || s.PstSettlementDate <= to.Value)
            .Select(s => new PartnerProjectSettlement
            {
                SettlementId = s.PstId,
                Date = s.PstSettlementDate,
                Direction = s.PstFromPartnerId == partnerId ? "paid_to" : "received_from",
                OtherPartnerName = s.PstFromPartnerId == partnerId
                    ? (s.ToPartner?.PtrName ?? "Unknown")
                    : (s.FromPartner?.PtrName ?? "Unknown"),
                Amount = s.PstAmount,
                Currency = s.PstCurrency,
                ConvertedAmount = s.PstConvertedAmount,
                CurrencyExchanges = s.CurrencyExchanges.Select(ce => new CurrencyExchangeResponse
                {
                    Id = ce.SceId,
                    CurrencyCode = ce.SceCurrencyCode,
                    ExchangeRate = ce.SceExchangeRate,
                    ConvertedAmount = ce.SceConvertedAmount
                }).ToList()
            })
            .OrderBy(s => s.Date)
            .ToList();

        return new PartnerProjectSummary
        {
            ProjectId = projectId,
            ProjectName = projectName,
            CurrencyCode = currencyCode,
            PaidPhysically = partnerBalance?.PaidPhysically ?? 0,
            OthersOweHim = partnerBalance?.OthersOweHim ?? 0,
            HeOwesOthers = partnerBalance?.HeOwesOthers ?? 0,
            SettlementsReceived = partnerBalance?.SettlementsReceived ?? 0,
            SettlementsPaid = partnerBalance?.SettlementsPaid ?? 0,
            NetBalance = partnerBalance?.NetBalance ?? 0,
            CurrencyTotals = partnerBalance?.CurrencyTotals.ToList() ?? [],
            Transactions = transactions,
            Settlements = settlements
        };
    }
}
