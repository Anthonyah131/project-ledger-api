using ProjectLedger.API.DTOs.Common;
using ProjectLedger.API.DTOs.Report;
using ProjectLedger.API.Extensions.Mappings;

namespace ProjectLedger.API.Services;

public partial class ReportService
{
    public async Task<DetailedIncomeReportResponse> GetDetailedIncomesAsync(
        Guid projectId, Guid userId, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var project = await _projectService.GetByIdAsync(projectId, ct)
            ?? throw new KeyNotFoundException("Project not found.");

        var incomes = (await _incomeRepo.GetDetailedByProjectIdAsync(projectId, from, to, ct)).ToList();

        var totalIncome = incomes.Sum(i => i.IncConvertedAmount);

        // Build monthly sections (oldest -> newest)
        var sections = incomes
            .GroupBy(i => new { i.IncIncomeDate.Year, i.IncIncomeDate.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g =>
            {
                var sectionTotal = g.Sum(i => i.IncConvertedAmount);
                var sectionCount = g.Count();
                var topInSection = g.OrderByDescending(i => i.IncConvertedAmount).First();

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
                        Notes = i.IncNotes,
                        Splits = i.Splits?.Count > 0
                            ? i.Splits.Select(s => new IncomeSplitRow
                            {
                                PartnerId = s.InsPartnerId,
                                PartnerName = s.Partner?.PtrName ?? "Unknown",
                                SplitType = s.InsSplitType,
                                SplitValue = s.InsSplitValue,
                                ResolvedAmount = s.InsResolvedAmount,
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

            // Partner income summary
            if (project.PrjPartnersEnabled)
            {
                var allSplits = incomes
                    .SelectMany(i => i.Splits ?? [])
                    .ToList();

                if (allSplits.Count > 0)
                {
                    var partnerRows = allSplits
                        .GroupBy(s => new { s.InsPartnerId, Name = s.Partner?.PtrName ?? "Unknown" })
                        .Select(g =>
                        {
                            var splitTotal = g.Sum(s => s.InsResolvedAmount);
                            return new PartnerIncomeRow
                            {
                                PartnerId = g.Key.InsPartnerId,
                                PartnerName = g.Key.Name,
                                TotalSplitAmount = splitTotal,
                                IncomeCount = g.Select(s => s.InsIncomeId).Distinct().Count(),
                                Percentage = totalIncome > 0 ? Math.Round(splitTotal / totalIncome * 100, 2) : 0
                            };
                        })
                        .OrderByDescending(p => p.TotalSplitAmount)
                        .ToList();

                    report.PartnerSummary = new PartnerIncomeSummary { Partners = partnerRows };
                }
            }
        }

        return report;
    }
}
