using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.DTOs.Report;
using ProjectLedger.API.Extensions.Mappings;

namespace ProjectLedger.API.Services;

public partial class ReportService
{
    public async Task<DetailedExpenseReportResponse> GetDetailedExpensesAsync(
        Guid projectId, Guid userId, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var project = await _projectService.GetByIdAsync(projectId, ct)
            ?? throw new KeyNotFoundException("Project not found.");

        // Load detailed expenses and incomes
        var expenses = (await _expenseRepo.GetDetailedByProjectIdAsync(projectId, from, to, ct)).ToList();
        var incomes = (await _incomeRepo.GetByProjectIdAsync(projectId, ct))
            .Where(i => from is null || i.IncIncomeDate >= from.Value)
            .Where(i => to is null || i.IncIncomeDate <= to.Value)
            .ToList();

        var totalSpent = expenses.Sum(e => e.ExpConvertedAmount);
        var totalIncome = incomes.Sum(i => i.IncConvertedAmount);

        var incomeByMonth = incomes
            .GroupBy(i => new { i.IncIncomeDate.Year, i.IncIncomeDate.Month })
            .ToDictionary(
                g => (g.Key.Year, g.Key.Month),
                g => new
                {
                    Total = g.Sum(i => i.IncConvertedAmount),
                    Count = g.Count()
                });

        // Build monthly sections (oldest -> newest)
        var sections = expenses
            .GroupBy(e => new { e.ExpExpenseDate.Year, e.ExpExpenseDate.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g =>
            {
                var sectionTotal = g.Sum(e => e.ExpConvertedAmount);
                var sectionCount = g.Count();
                var topInSection = g.OrderByDescending(e => e.ExpConvertedAmount).First();
                var sectionIncome = incomeByMonth.GetValueOrDefault((g.Key.Year, g.Key.Month));
                return new MonthlyExpenseSection
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    MonthLabel = $"{new DateTime(g.Key.Year, g.Key.Month, 1):MMMM yyyy}",
                    SectionTotal = sectionTotal,
                    SectionCount = sectionCount,
                    SectionIncomeTotal = sectionIncome?.Total ?? 0,
                    SectionIncomeCount = sectionIncome?.Count ?? 0,
                    SectionNetBalance = (sectionIncome?.Total ?? 0) - sectionTotal,
                    PercentageOfTotal = totalSpent > 0
                        ? Math.Round(sectionTotal / totalSpent * 100, 2)
                        : 0,
                    AverageExpenseAmount = sectionCount > 0
                        ? Math.Round(sectionTotal / sectionCount, 2)
                        : 0,
                    TopExpense = new SectionTopExpense
                    {
                        Title = topInSection.ExpTitle,
                        Amount = topInSection.ExpConvertedAmount
                    },
                    Expenses = g.OrderBy(e => e.ExpExpenseDate).Select(e => new DetailedExpenseRow
                    {
                        Id = e.ExpId,
                        Title = e.ExpTitle,
                        ExpenseDate = e.ExpExpenseDate,
                        CategoryId = e.ExpCategoryId,
                        CategoryName = e.Category?.CatName ?? "Unknown",
                        PaymentMethodId = e.ExpPaymentMethodId,
                        PaymentMethodName = e.PaymentMethod?.PmtName ?? "Unknown",
                        PaymentMethodType = e.PaymentMethod?.PmtType ?? "unknown",
                        OriginalAmount = e.ExpOriginalAmount,
                        OriginalCurrency = e.ExpOriginalCurrency,
                        ExchangeRate = e.ExpExchangeRate,
                        ConvertedAmount = e.ExpConvertedAmount,
                        CurrencyExchanges = e.CurrencyExchanges?.Select(x => x.ToResponse()).ToList(),
                        AccountAmount = e.ExpAccountAmount,
                        AccountCurrency = e.ExpAccountCurrency,
                        Description = e.ExpDescription,
                        ReceiptNumber = e.ExpReceiptNumber,
                        Notes = e.ExpNotes,
                        IsObligationPayment = e.ExpObligationId.HasValue,
                        ObligationId = e.ExpObligationId,
                        ObligationTitle = e.Obligation?.OblTitle,
                        Splits = e.Splits?.Count > 0
                            ? e.Splits.Select(s => new ExpenseSplitRow
                            {
                                PartnerId = s.ExsPartnerId,
                                PartnerName = s.Partner?.PtrName ?? "Unknown",
                                SplitType = s.ExsSplitType,
                                SplitValue = s.ExsSplitValue,
                                ResolvedAmount = s.ExsResolvedAmount,
                                CurrencyExchanges = s.CurrencyExchanges?.Count > 0
                                    ? s.CurrencyExchanges.Select(ce => new CurrencyExchangeResponse
                                    {
                                        Id = ce.SceId,
                                        CurrencyCode = ce.SceCurrencyCode,
                                        ExchangeRate = ce.SceExchangeRate,
                                        ConvertedAmount = ce.SceConvertedAmount
                                    }).ToList()
                                    : null
                            }).ToList()
                            : null
                    }).ToList()
                };
            }).ToList();

        // Root-level stats
        var largestExpense = expenses.OrderByDescending(e => e.ExpConvertedAmount).FirstOrDefault();
        var peakSection = sections.OrderByDescending(s => s.SectionTotal).FirstOrDefault();

        var report = new DetailedExpenseReportResponse
        {
            ProjectId = project.PrjId,
            ProjectName = project.PrjName,
            CurrencyCode = project.PrjCurrencyCode,
            DateFrom = from,
            DateTo = to,
            GeneratedAt = DateTime.UtcNow,
            TotalSpent = totalSpent,
            TotalExpenseCount = expenses.Count,
            TotalIncome = totalIncome,
            TotalIncomeCount = incomes.Count,
            NetBalance = totalIncome - totalSpent,
            AverageExpenseAmount = expenses.Count > 0 ? Math.Round(totalSpent / expenses.Count, 2) : 0,
            AverageMonthlySpend = sections.Count > 0
                ? Math.Round(sections.Sum(s => s.SectionTotal) / sections.Count, 2)
                : 0,
            PeakMonth = peakSection is null ? null : new PeakMonthInfo
            {
                MonthLabel = peakSection.MonthLabel,
                Total = peakSection.SectionTotal
            },
            LargestExpense = largestExpense is null ? null : new LargestExpenseInfo
            {
                ExpenseId = largestExpense.ExpId,
                Title = largestExpense.ExpTitle,
                Amount = largestExpense.ExpConvertedAmount,
                ExpenseDate = largestExpense.ExpExpenseDate,
                CategoryName = largestExpense.Category?.CatName ?? "Unknown",
                PaymentMethodName = largestExpense.PaymentMethod?.PmtName ?? "Unknown"
            },
            Sections = sections
        };

        // Advanced sections only if the plan allows it
        var hasAdvanced = await _planAuth.HasPermissionAsync(
            userId, PlanPermission.CanUseAdvancedReports, ct);

        if (hasAdvanced)
        {
            // Category analysis with budget
            var categories = await _categoryRepo.GetByProjectIdAsync(projectId, ct);
            report.CategoryAnalysis = categories.Select(cat =>
            {
                var catExpenses = expenses.Where(e => e.ExpCategoryId == cat.CatId).ToList();
                var spent = catExpenses.Sum(e => e.ExpConvertedAmount);

                return new CategoryAnalysisRow
                {
                    CategoryId = cat.CatId,
                    CategoryName = cat.CatName,
                    IsDefault = cat.CatIsDefault,
                    BudgetAmount = cat.CatBudgetAmount,
                    SpentAmount = spent,
                    ExpenseCount = catExpenses.Count,
                    Percentage = totalSpent > 0 ? Math.Round(spent / totalSpent * 100, 2) : 0,
                    BudgetRemaining = cat.CatBudgetAmount.HasValue ? cat.CatBudgetAmount.Value - spent : null,
                    BudgetUsedPercentage = cat.CatBudgetAmount is > 0
                        ? Math.Round(spent / cat.CatBudgetAmount.Value * 100, 2)
                        : null,
                    BudgetExceeded = cat.CatBudgetAmount.HasValue ? spent > cat.CatBudgetAmount.Value : null
                };
            })
            .OrderByDescending(c => c.SpentAmount)
            .ToList();

            // Payment method analysis
            report.PaymentMethodAnalysis = expenses
                .GroupBy(e => new
                {
                    e.ExpPaymentMethodId,
                    Name = e.PaymentMethod?.PmtName ?? "Unknown",
                    Type = e.PaymentMethod?.PmtType ?? "unknown"
                })
                .Select(g =>
                {
                    var pmTotal = g.Sum(e => e.ExpConvertedAmount);
                    var pmCount = g.Count();
                    return new PaymentMethodAnalysisRow
                    {
                        PaymentMethodId = g.Key.ExpPaymentMethodId,
                        PaymentMethodName = g.Key.Name,
                        Type = g.Key.Type,
                        SpentAmount = pmTotal,
                        ExpenseCount = pmCount,
                        Percentage = totalSpent > 0 ? Math.Round(pmTotal / totalSpent * 100, 2) : 0,
                        AverageExpenseAmount = pmCount > 0 ? Math.Round(pmTotal / pmCount, 2) : 0
                    };
                })
                .OrderByDescending(r => r.SpentAmount)
                .ToList();

            // Obligation summary
            var obligations = (await _obligationRepo.GetByProjectIdWithPaymentsAsync(projectId, ct)).ToList();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var oblRows = obligations.Select(o =>
            {
                var activePayments = o.Payments
                    .Where(p => !p.ExpIsDeleted && p.ExpIsActive)
                    .ToList();

                var paid = activePayments.Sum(p =>
                    string.Equals(p.ExpOriginalCurrency, o.OblCurrency, StringComparison.OrdinalIgnoreCase)
                        ? p.ExpOriginalAmount
                        : p.ExpObligationEquivalentAmount ?? p.ExpConvertedAmount);

                var remaining = o.OblTotalAmount - paid;
                var status = ComputeObligationStatus(o, paid, today);

                return new ObligationReportRow
                {
                    OblId = o.OblId,
                    Title = o.OblTitle,
                    Description = o.OblDescription,
                    TotalAmount = o.OblTotalAmount,
                    PaidAmount = paid,
                    RemainingAmount = remaining,
                    Currency = o.OblCurrency,
                    DueDate = o.OblDueDate,
                    Status = status,
                    PaymentCount = activePayments.Count,
                    LastPaymentDate = activePayments
                        .OrderByDescending(p => p.ExpExpenseDate)
                        .Select(p => (DateOnly?)p.ExpExpenseDate)
                        .FirstOrDefault()
                };
            }).ToList();

            var byStatus = oblRows
                .GroupBy(o => o.Status)
                .Select(g => new ObligationStatusGroup
                {
                    Status = g.Key,
                    Count = g.Count(),
                    TotalAmount = g.Sum(o => o.TotalAmount),
                    TotalPaid = g.Sum(o => o.PaidAmount),
                    Obligations = g.OrderByDescending(o => o.TotalAmount).ToList()
                })
                .OrderBy(g => g.Status switch
                {
                    "overdue" => 0, "open" => 1, "partially_paid" => 2, "paid" => 3, _ => 4
                })
                .ToList();

            report.ObligationSummary = new ObligationSummarySection
            {
                TotalObligations = obligations.Count,
                TotalAmount = oblRows.Sum(o => o.TotalAmount),
                TotalPaid = oblRows.Sum(o => o.PaidAmount),
                TotalPending = oblRows.Sum(o => o.RemainingAmount),
                OverdueCount = oblRows.Count(o => o.Status == "overdue"),
                OverdueAmount = oblRows.Where(o => o.Status == "overdue").Sum(o => o.RemainingAmount),
                ByStatus = byStatus
            };

            // Partner expense summary (only if project has partners enabled)
            if (project.PrjPartnersEnabled)
            {
                var allSplits = expenses
                    .SelectMany(e => e.Splits ?? [])
                    .ToList();

                if (allSplits.Count > 0)
                {
                    var partnerRows = allSplits
                        .GroupBy(s => new { s.ExsPartnerId, Name = s.Partner?.PtrName ?? "Unknown" })
                        .Select(g =>
                        {
                            var splitTotal = g.Sum(s => s.ExsResolvedAmount);
                            return new PartnerExpenseRow
                            {
                                PartnerId = g.Key.ExsPartnerId,
                                PartnerName = g.Key.Name,
                                TotalSplitAmount = splitTotal,
                                ExpenseCount = g.Select(s => s.ExsExpenseId).Distinct().Count(),
                                Percentage = totalSpent > 0 ? Math.Round(splitTotal / totalSpent * 100, 2) : 0
                            };
                        })
                        .OrderByDescending(p => p.TotalSplitAmount)
                        .ToList();

                    report.PartnerSummary = new PartnerExpenseSummary { Partners = partnerRows };
                }
            }
        }

        return report;
    }

    private static string ComputeObligationStatus(Models.Obligation o, decimal paid, DateOnly today)
    {
        if (paid >= o.OblTotalAmount) return "paid";
        if (o.OblDueDate.HasValue && o.OblDueDate.Value < today && paid < o.OblTotalAmount) return "overdue";
        if (paid > 0) return "partially_paid";
        return "open";
    }
}
