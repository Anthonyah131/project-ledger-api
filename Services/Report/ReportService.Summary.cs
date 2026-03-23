using ProjectLedger.API.DTOs.Report;

namespace ProjectLedger.API.Services;

public partial class ReportService
{
    public async Task<ProjectReportResponse> GetSummaryAsync(
        Guid projectId, Guid ownerUserId, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var project = await _projectService.GetByIdAsync(projectId, ct)
            ?? throw new KeyNotFoundException("ProjectNotFound");

        var expenses = (await _expenseRepo.GetByProjectIdWithDetailsAsync(projectId, ct))
            .Where(e => !e.ExpIsTemplate)
            .Where(e => from is null || e.ExpExpenseDate >= from.Value)
            .Where(e => to is null || e.ExpExpenseDate <= to.Value)
            .ToList();

        var totalSpent = expenses.Sum(e => e.ExpConvertedAmount);

        var byCategory = expenses
            .GroupBy(e => new { e.ExpCategoryId, Name = e.Category?.CatName ?? "Unknown" })
            .Select(g =>
            {
                var groupTotal = g.Sum(e => e.ExpConvertedAmount);
                var count = g.Count();
                return new CategoryBreakdown
                {
                    CategoryId = g.Key.ExpCategoryId,
                    CategoryName = g.Key.Name,
                    TotalAmount = groupTotal,
                    ExpenseCount = count,
                    Percentage = totalSpent > 0 ? Math.Round(groupTotal / totalSpent * 100, 2) : 0,
                    AverageAmount = count > 0 ? Math.Round(groupTotal / count, 2) : 0
                };
            })
            .OrderByDescending(c => c.TotalAmount)
            .ToList();

        var byPaymentMethod = expenses
            .GroupBy(e => new { e.ExpPaymentMethodId, Name = e.PaymentMethod?.PmtName ?? "Unknown" })
            .Select(g =>
            {
                var groupTotal = g.Sum(e => e.ExpConvertedAmount);
                var count = g.Count();
                return new PaymentMethodBreakdown
                {
                    PaymentMethodId = g.Key.ExpPaymentMethodId,
                    PaymentMethodName = g.Key.Name,
                    TotalAmount = groupTotal,
                    ExpenseCount = count,
                    Percentage = totalSpent > 0 ? Math.Round(groupTotal / totalSpent * 100, 2) : 0,
                    AverageAmount = count > 0 ? Math.Round(groupTotal / count, 2) : 0
                };
            })
            .OrderByDescending(p => p.TotalAmount)
            .ToList();

        var topExpenseEntity = expenses
            .OrderByDescending(e => e.ExpConvertedAmount)
            .FirstOrDefault();

        var incomes = (await _incomeRepo.GetByProjectIdAsync(projectId, ct))
            .Where(i => from is null || i.IncIncomeDate >= from.Value)
            .Where(i => to is null || i.IncIncomeDate <= to.Value)
            .ToList();

        var totalIncome = incomes.Sum(i => i.IncConvertedAmount);

        var summaryResponse = new ProjectReportResponse
        {
            ProjectId = project.PrjId,
            ProjectName = project.PrjName,
            CurrencyCode = project.PrjCurrencyCode,
            DateFrom = from,
            DateTo = to,
            GeneratedAt = DateTime.UtcNow,
            TotalSpent = totalSpent,
            ExpenseCount = expenses.Count,
            AverageExpenseAmount = expenses.Count > 0 ? Math.Round(totalSpent / expenses.Count, 2) : 0,
            TotalIncome = totalIncome,
            IncomeCount = incomes.Count,
            NetBalance = totalIncome - totalSpent,
            TopExpense = topExpenseEntity is null ? null : new TopExpenseInfo
            {
                ExpenseId = topExpenseEntity.ExpId,
                Title = topExpenseEntity.ExpTitle,
                Amount = topExpenseEntity.ExpConvertedAmount,
                CategoryName = topExpenseEntity.Category?.CatName ?? "Unknown",
                ExpenseDate = topExpenseEntity.ExpExpenseDate
            },
            ByCategory = byCategory,
            ByPaymentMethod = byPaymentMethod
        };

        // Budget context (optional -- when project has an active budget)
        var budget = await _budgetRepo.GetActiveByProjectIdAsync(projectId, ct);
        if (budget is not null)
        {
            summaryResponse.Budget = budget.PjbTotalBudget;
            summaryResponse.BudgetUsedPercentage = budget.PjbTotalBudget > 0
                ? Math.Round(totalSpent / budget.PjbTotalBudget * 100, 2)
                : null;
        }

        // Advanced plan: obligation vs. regular split + partners + alt currencies
        var hasAdvanced = await _planAuth.HasPermissionAsync(
            ownerUserId, PlanPermission.CanUseAdvancedReports, ct);
        if (hasAdvanced)
        {
            summaryResponse.ObligationSpent = expenses
                .Where(e => e.ExpObligationId.HasValue)
                .Sum(e => e.ExpConvertedAmount);
            summaryResponse.RegularSpent = expenses
                .Where(e => !e.ExpObligationId.HasValue)
                .Sum(e => e.ExpConvertedAmount);

            // Partner breakdown (only if project has partners enabled)
            if (project.PrjPartnersEnabled)
            {
                var detailedExpenses = (await _expenseRepo.GetDetailedByProjectIdAsync(projectId, from, to, ct)).ToList();
                var detailedIncomes = (await _incomeRepo.GetDetailedByProjectIdAsync(projectId, from, to, ct)).ToList();
                var settlements = (await _settlementRepo.GetByProjectIdAsync(projectId, ct))
                    .Where(s => from is null || s.PstSettlementDate >= from.Value)
                    .Where(s => to is null || s.PstSettlementDate <= to.Value)
                    .ToList();

                var partnerIds = detailedExpenses.SelectMany(e => e.Splits ?? []).Select(s => s.ExsPartnerId)
                    .Union(detailedIncomes.SelectMany(i => i.Splits ?? []).Select(s => s.InsPartnerId))
                    .Union(settlements.Select(s => s.PstFromPartnerId))
                    .Union(settlements.Select(s => s.PstToPartnerId))
                    .Distinct()
                    .ToList();

                if (partnerIds.Count > 0)
                {
                    var partnerBreakdowns = new List<PartnerBreakdown>();

                    foreach (var partnerId in partnerIds)
                    {
                        var expSplits = detailedExpenses
                            .SelectMany(e => e.Splits ?? [])
                            .Where(s => s.ExsPartnerId == partnerId)
                            .ToList();
                        var incSplits = detailedIncomes
                            .SelectMany(i => i.Splits ?? [])
                            .Where(s => s.InsPartnerId == partnerId)
                            .ToList();
                        var paidSettlements = settlements.Where(s => s.PstFromPartnerId == partnerId).ToList();
                        var receivedSettlements = settlements.Where(s => s.PstToPartnerId == partnerId).ToList();

                        var totalExpSplits = expSplits.Sum(s => s.ExsResolvedAmount);
                        var totalIncSplits = incSplits.Sum(s => s.InsResolvedAmount);
                        var totalPaid = paidSettlements.Sum(s => s.PstConvertedAmount);
                        var totalReceived = receivedSettlements.Sum(s => s.PstConvertedAmount);

                        var partnerName = expSplits.FirstOrDefault()?.Partner?.PtrName
                            ?? incSplits.FirstOrDefault()?.Partner?.PtrName
                            ?? paidSettlements.FirstOrDefault()?.FromPartner?.PtrName
                            ?? receivedSettlements.FirstOrDefault()?.ToPartner?.PtrName
                            ?? "Unknown";

                        partnerBreakdowns.Add(new PartnerBreakdown
                        {
                            PartnerId = partnerId,
                            PartnerName = partnerName,
                            TotalExpenseSplits = totalExpSplits,
                            TotalIncomeSplits = totalIncSplits,
                            TotalSettlementsPaid = totalPaid,
                            TotalSettlementsReceived = totalReceived,
                            NetBalance = totalIncSplits - totalExpSplits + totalPaid - totalReceived,
                            ExpenseSplitCount = expSplits.Count,
                            IncomeSplitCount = incSplits.Count,
                            SettlementCount = paidSettlements.Count + receivedSettlements.Count
                        });
                    }

                    summaryResponse.ByPartner = partnerBreakdowns.OrderByDescending(p => p.TotalExpenseSplits).ToList();
                }
            }

            // Alternative currency totals
            var altCurrencies = (await _altCurrencyRepo.GetByProjectIdAsync(projectId, ct)).ToList();
            if (altCurrencies.Count > 0)
            {
                var expenseCurrencyExchanges = expenses
                    .SelectMany(e => e.CurrencyExchanges ?? [])
                    .ToList();
                var incomeCurrencyExchanges = incomes
                    .SelectMany(i => i.CurrencyExchanges ?? [])
                    .ToList();

                var altTotals = altCurrencies.Select(ac =>
                {
                    var altSpent = expenseCurrencyExchanges
                        .Where(ce => ce.TceCurrencyCode == ac.PacCurrencyCode)
                        .Sum(ce => ce.TceConvertedAmount);
                    var altIncome = incomeCurrencyExchanges
                        .Where(ce => ce.TceCurrencyCode == ac.PacCurrencyCode)
                        .Sum(ce => ce.TceConvertedAmount);

                    return new AlternativeCurrencyTotal
                    {
                        CurrencyCode = ac.PacCurrencyCode,
                        TotalSpent = altSpent,
                        TotalIncome = altIncome,
                        NetBalance = altIncome - altSpent
                    };
                })
                .Where(t => t.TotalSpent > 0 || t.TotalIncome > 0)
                .ToList();

                if (altTotals.Count > 0)
                    summaryResponse.AlternativeCurrencyTotals = altTotals;
            }
        }

        return summaryResponse;
    }
}
