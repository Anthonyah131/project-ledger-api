using ProjectLedger.API.DTOs.Report;
using ProjectLedger.API.Extensions.Mappings;

namespace ProjectLedger.API.Services;

/// <summary>
/// Partial implementation of ReportService focusing on detailed income reporting.
/// </summary>
public partial class ReportService
{
    public async Task<DetailedIncomeReportResponse> GetDetailedIncomesAsync(
        Guid projectId, Guid userId, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var project = await _projectService.GetByIdAsync(projectId, ct)
            ?? throw new KeyNotFoundException("ProjectNotFound");

        var incomes = (await _incomeRepo.GetDetailedByProjectIdAsync(projectId, from, to, ct)).ToList();

        var totalIncome = incomes.Sum(i => i.IncConvertedAmount);

        // Alternative currency totals (root level)
        var altCurrencies = BuildIncomeAlternativeCurrencyTotals(incomes);

        // Build monthly sections (oldest -> newest)
        var sections = incomes
            .GroupBy(i => new { i.IncIncomeDate.Year, i.IncIncomeDate.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g =>
            {
                var sectionTotal = g.Sum(i => i.IncConvertedAmount);
                var sectionCount = g.Count();
                var topInSection = g.OrderByDescending(i => i.IncConvertedAmount).First();

                // Alternative currency totals for this section
                var sectionAltCurrencies = BuildIncomeAlternativeCurrencyTotals(g.ToList());

                return new MonthlyIncomeSection
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    MonthLabel = $"{new DateTime(g.Key.Year, g.Key.Month, 1):MMMM yyyy}",
                    SectionTotal = sectionTotal,
                    SectionCount = sectionCount,
                    PercentageOfTotal = totalIncome > 0
                        ? Math.Round(sectionTotal / totalIncome * 100, 2)
                        : 0,
                    AverageIncomeAmount = sectionCount > 0
                        ? Math.Round(sectionTotal / sectionCount, 2)
                        : 0,
                    TopIncome = new SectionTopIncome
                    {
                        Title = topInSection.IncTitle,
                        Amount = topInSection.IncConvertedAmount
                    },
                    AlternativeCurrencies = sectionAltCurrencies.Count > 0 ? sectionAltCurrencies : null,
                    Incomes = g.OrderBy(i => i.IncIncomeDate).Select(i => new DetailedIncomeRow
                    {
                        Id = i.IncId,
                        Title = i.IncTitle,
                        IncomeDate = i.IncIncomeDate,
                        CategoryId = i.IncCategoryId,
                        CategoryName = i.Category?.CatName ?? "Unknown",
                        PaymentMethodId = i.IncPaymentMethodId,
                        PaymentMethodName = i.PaymentMethod?.PmtName ?? "Unknown",
                        PaymentMethodType = i.PaymentMethod?.PmtType ?? "unknown",
                        OriginalAmount = i.IncOriginalAmount,
                        OriginalCurrency = i.IncOriginalCurrency,
                        ExchangeRate = i.IncExchangeRate,
                        ConvertedAmount = i.IncConvertedAmount,
                        AccountAmount = i.IncAccountAmount,
                        AccountCurrency = i.IncAccountCurrency,
                        CurrencyExchanges = i.CurrencyExchanges?.Select(x => x.ToResponse()).ToList(),
                        Description = i.IncDescription,
                        ReceiptNumber = i.IncReceiptNumber,
                        Notes = i.IncNotes
                    }).ToList()
                };
            }).ToList();

        // Root-level stats
        var largestIncome = incomes.OrderByDescending(i => i.IncConvertedAmount).FirstOrDefault();
        var peakSection = sections.OrderByDescending(s => s.SectionTotal).FirstOrDefault();

        var report = new DetailedIncomeReportResponse
        {
            ProjectId = project.PrjId,
            ProjectName = project.PrjName,
            CurrencyCode = project.PrjCurrencyCode,
            DateFrom = from,
            DateTo = to,
            GeneratedAt = DateTime.UtcNow,
            TotalIncome = totalIncome,
            TotalIncomeCount = incomes.Count,
            AverageIncomeAmount = incomes.Count > 0 ? Math.Round(totalIncome / incomes.Count, 2) : 0,
            AverageMonthlyIncome = sections.Count > 0
                ? Math.Round(sections.Sum(s => s.SectionTotal) / sections.Count, 2)
                : 0,
            PeakMonth = peakSection is null ? null : new PeakMonthInfo
            {
                MonthLabel = peakSection.MonthLabel,
                Total = peakSection.SectionTotal
            },
            LargestIncome = largestIncome is null ? null : new LargestIncomeInfo
            {
                IncomeId = largestIncome.IncId,
                Title = largestIncome.IncTitle,
                Amount = largestIncome.IncConvertedAmount,
                IncomeDate = largestIncome.IncIncomeDate,
                CategoryName = largestIncome.Category?.CatName ?? "Unknown",
                PaymentMethodName = largestIncome.PaymentMethod?.PmtName ?? "Unknown"
            },
            AlternativeCurrencies = altCurrencies.Count > 0 ? altCurrencies : null,
            Sections = sections
        };

        // Advanced sections
        var hasAdvanced = await _planAuth.HasPermissionAsync(
            userId, PlanPermission.CanUseAdvancedReports, ct);

        if (hasAdvanced)
        {
            // Category analysis
            report.CategoryAnalysis = incomes
                .GroupBy(i => new { i.IncCategoryId, Name = i.Category?.CatName ?? "Unknown" })
                .Select(g =>
                {
                    var catTotal = g.Sum(i => i.IncConvertedAmount);
                    var count = g.Count();
                    return new CategoryIncomeAnalysisRow
                    {
                        CategoryId = g.Key.IncCategoryId,
                        CategoryName = g.Key.Name,
                        TotalAmount = catTotal,
                        IncomeCount = count,
                        Percentage = totalIncome > 0 ? Math.Round(catTotal / totalIncome * 100, 2) : 0,
                        AverageAmount = count > 0 ? Math.Round(catTotal / count, 2) : 0
                    };
                })
                .OrderByDescending(c => c.TotalAmount)
                .ToList();

            // Payment method analysis
            report.PaymentMethodAnalysis = incomes
                .GroupBy(i => new
                {
                    i.IncPaymentMethodId,
                    Name = i.PaymentMethod?.PmtName ?? "Unknown",
                    Type = i.PaymentMethod?.PmtType ?? "unknown"
                })
                .Select(g =>
                {
                    var pmTotal = g.Sum(i => i.IncConvertedAmount);
                    var count = g.Count();
                    return new PaymentMethodIncomeAnalysisRow
                    {
                        PaymentMethodId = g.Key.IncPaymentMethodId,
                        PaymentMethodName = g.Key.Name,
                        Type = g.Key.Type,
                        TotalAmount = pmTotal,
                        IncomeCount = count,
                        Percentage = totalIncome > 0 ? Math.Round(pmTotal / totalIncome * 100, 2) : 0,
                        AverageAmount = count > 0 ? Math.Round(pmTotal / count, 2) : 0
                    };
                })
                .OrderByDescending(p => p.TotalAmount)
                .ToList();
        }

        return report;
    }

    /// <summary>Aggregates alternative currency groupings for detailed income exports.</summary>
    private static List<AlternativeCurrencyTotals> BuildIncomeAlternativeCurrencyTotals(
        List<Models.Income> incomes)
    {
        var incByCurrency = incomes
            .SelectMany(i => i.CurrencyExchanges ?? [])
            .GroupBy(ce => ce.TceCurrencyCode)
            .ToDictionary(g => g.Key, g => g.Sum(ce => ce.TceConvertedAmount));

        var sectionCount = incomes
            .Select(i => (i.IncIncomeDate.Year, i.IncIncomeDate.Month))
            .Distinct()
            .Count();

        return incByCurrency.Select(kvp =>
        {
            var incCount = incomes.Count(i =>
                i.CurrencyExchanges?.Any(ce => ce.TceCurrencyCode == kvp.Key) == true);

            return new AlternativeCurrencyTotals
            {
                CurrencyCode = kvp.Key,
                TotalSpent = 0,
                TotalIncome = kvp.Value,
                NetBalance = kvp.Value,
                AverageExpenseAmount = 0,
                AverageMonthlySpend = sectionCount > 0 ? Math.Round(kvp.Value / sectionCount, 2) : 0
            };
        }).OrderBy(a => a.CurrencyCode).ToList();
    }
}
