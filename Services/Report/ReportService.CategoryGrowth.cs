using ProjectLedger.API.DTOs.Report;

namespace ProjectLedger.API.Services;

public partial class ReportService
{
    public async Task<CategoryGrowthEnvelopeResponse> GetCategoryGrowthAsync(
        Guid projectId, CancellationToken ct = default)
    {
        var project = await _projectService.GetByIdAsync(projectId, ct)
            ?? throw new KeyNotFoundException("ProjectNotFound");

        var now = DateTime.UtcNow;
        var currentMonth = new DateOnly(now.Year, now.Month, 1);
        var previousMonth = currentMonth.AddMonths(-1);

        var expenses = (await _expenseRepo.GetByProjectIdWithDetailsAsync(projectId, ct))
            .Where(e => !e.ExpIsTemplate)
            .ToList();

        var currentByCategory = expenses
            .Where(e => e.ExpExpenseDate.Year == currentMonth.Year
                     && e.ExpExpenseDate.Month == currentMonth.Month)
            .GroupBy(e => new { e.ExpCategoryId, Name = e.Category?.CatName ?? "Unknown" })
            .ToDictionary(g => g.Key, g => g.ToList());

        var previousByCategory = expenses
            .Where(e => e.ExpExpenseDate.Year == previousMonth.Year
                     && e.ExpExpenseDate.Month == previousMonth.Month)
            .GroupBy(e => new { e.ExpCategoryId, Name = e.Category?.CatName ?? "Unknown" })
            .ToDictionary(g => g.Key, g => g.ToList());

        // Merge all categories from both months
        var allCategories = currentByCategory.Keys
            .Union(previousByCategory.Keys)
            .ToList();

        var growth = allCategories
            .Select(cat =>
            {
                var currentItems = currentByCategory.GetValueOrDefault(cat) ?? [];
                var previousItems = previousByCategory.GetValueOrDefault(cat) ?? [];
                var current = currentItems.Sum(e => e.ExpConvertedAmount);
                var previous = previousItems.Sum(e => e.ExpConvertedAmount);
                var diff = current - previous;

                return new CategoryGrowthResponse
                {
                    CategoryId = cat.ExpCategoryId,
                    CategoryName = cat.Name,
                    CurrentMonthAmount = current,
                    PreviousMonthAmount = previous,
                    GrowthAmount = diff,
                    GrowthPercentage = previous > 0
                        ? Math.Round(diff / previous * 100, 2)
                        : null,
                    CurrentMonthCount = currentItems.Count,
                    PreviousMonthCount = previousItems.Count,
                    AverageAmountCurrent = currentItems.Count > 0
                        ? Math.Round(current / currentItems.Count, 2)
                        : 0,
                    AverageAmountPrevious = previousItems.Count > 0
                        ? Math.Round(previous / previousItems.Count, 2)
                        : 0,
                    IsNew = previous == 0 && current > 0,
                    IsDisappeared = current == 0 && previous > 0
                };
            })
            .OrderByDescending(g => g.GrowthAmount)
            .ToList();

        return new CategoryGrowthEnvelopeResponse
        {
            ProjectId = projectId,
            ProjectName = project.PrjName,
            CurrencyCode = project.PrjCurrencyCode,
            CurrentMonthLabel = $"{new DateTime(currentMonth.Year, currentMonth.Month, 1):MMMM yyyy}",
            PreviousMonthLabel = $"{new DateTime(previousMonth.Year, previousMonth.Month, 1):MMMM yyyy}",
            GeneratedAt = DateTime.UtcNow,
            Categories = growth
        };
    }
}
